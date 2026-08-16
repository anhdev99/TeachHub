using System.Text;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Files;
using TeachHub.Domain.Integrity;

namespace TeachHub.Application.Features.Integrity;

/// <summary>
/// Computes the Winnowing fingerprints of one submitted file (Phase 6a, Gap #14).
///
/// An <c>ICommand</c> and not an <c>IQuery</c>: it writes. Thanks to the <c>IBaseCommand</c>
/// marker, <c>TransactionBehavior</c> already wraps the whole "delete the old fingerprints +
/// write the new ones" pair in a single transaction — fail halfway through and the file is left
/// with no fingerprints at all and disappears from every similarity report with nothing
/// reporting it.
///
/// Safe to run many times over (Hangfire retries, the instructor clicking "recompute"): always
/// delete every old fingerprint of that exact file before writing. Skip the delete and each
/// rerun doubles the number of rows, and because the comparison works on SETS (duplicates
/// dropped) the percentages still come out right — it breaks silently, and only surfaces once
/// the table has swollen fivefold.
/// </summary>
public sealed record ComputeFingerprintsCommand(Guid SubmissionFileId) : ICommand<Result>;

public sealed class ComputeFingerprintsHandler(IAppDbContext db, IFileStorage storage)
    : ICommandHandler<ComputeFingerprintsCommand, Result>
{
    /// <summary>
    /// Loads the whole archive into memory so that many entries can be read without reopening
    /// storage dozens of times (on S3 that means dozens of GETs against the same object). The
    /// 32 MB ceiling: a real source-code submission never comes anywhere near it, and anything
    /// heavier than that is almost certainly images, video or a build directory — hashing those
    /// only wastes space.
    /// </summary>
    private const long MaxArchiveBytes = 32L * 1024 * 1024;

    private const long MaxEntryBytes = 2L * 1024 * 1024;

    /// <summary>Ceiling on the source files taken out of one archive — stops submissions that ship a whole node_modules.</summary>
    private const int MaxSourceEntries = 200;

    /// <summary>Ceiling on the fingerprints written for ONE submitted file; stops generated files hundreds of thousands of lines long.</summary>
    private const int MaxFingerprintsPerFile = 20_000;

    public async Task<Result> Handle(ComputeFingerprintsCommand request, CancellationToken ct)
    {
        var file = await db.SubmissionFiles
            .SingleOrDefaultAsync(f => f.Id == request.SubmissionFileId, ct);

        if (file is null) return Result.NotFound("Không tìm thấy tệp nộp.");

        // Clean up first, always — even when nothing ends up being written afterwards. When a
        // file has just been resubmitted as a version that carries no source code, the old
        // version's fingerprints have to disappear with it; otherwise the report compares the
        // current submission against content the student has already replaced.
        var existing = await db.SourceFingerprints
            .Where(f => f.SubmissionFileId == file.Id).ToListAsync(ct);

        if (existing.Count > 0) db.SourceFingerprints.RemoveRange(existing);

        if (file.FilePath is null)                        // link submission, nothing to hash
        {
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }

        var computed = new List<SourceFingerprint>();

        if (SourceFileTypes.IsSourceCode(file.FileName))
        {
            await using var s = await storage.OpenReadAsync(file.FilePath, ct);
            computed.AddRange(FingerprintOneFile(file.Id, null, await ReadTextAsync(s, ct)));
        }
        else if (file.FileTree is { Files.Count: > 0 } tree && file.SizeBytes <= MaxArchiveBytes)
        {
            var sources = tree.Files
                .Where(f => SourceFileTypes.IsSourceCode(f.Path))
                .Where(f => f.SizeBytes is > 0 and <= MaxEntryBytes)
                .OrderBy(f => f.Path)
                .Take(MaxSourceEntries)
                .ToList();

            if (sources.Count > 0)
            {
                await using var archive = await storage.OpenReadAsync(file.FilePath, ct);
                using var buffer = new MemoryStream();
                await archive.CopyToAsync(buffer, ct);

                foreach (var e in sources)
                {
                    if (computed.Count >= MaxFingerprintsPerFile) break;

                    buffer.Position = 0;

                    try
                    {
                        // Go through SafeZipReader instead of opening a ZipArchive here: entry
                        // paths are data the student controls, and the rule set that blocks
                        // zip-slip is only allowed to live in ONE place (ADR-005).
                        var byteEntry = SafeZipReader.ReadEntry(buffer, e.Path, ZipLimits.Default, MaxEntryBytes);
                        computed.AddRange(FingerprintOneFile(file.Id, e.Path, ReadText(byteEntry)));
                    }
                    catch (UnsafeArchiveException)
                    {
                        // Corrupt or malicious entry: skip THAT entry and carry on, do not throw.
                        // Throwing here makes Hangfire retry the whole file over one entry that
                        // was never going to be readable, and the entire submission loses its
                        // fingerprints because of a single junk file.
                    }
                }
            }
        }

        if (computed.Count > MaxFingerprintsPerFile) computed = [.. computed.Take(MaxFingerprintsPerFile)];

        // Add explicitly through the DbSet, not by adding to the collection of a tracked entity:
        // Entity.Id generates its UUIDv7 at construction time, so the Id is already non-default
        // and EF would issue an UPDATE instead of an INSERT.
        db.SourceFingerprints.AddRange(computed);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }

    /// <summary>
    /// Normalize → k-grams → winnowing, then map token indexes back to character offsets in the
    /// original source so the comparison screen can highlight exactly the right span.
    /// </summary>
    private static IEnumerable<SourceFingerprint> FingerprintOneFile(
        Guid submissionFileId, string? entryPath, string content)
    {
        var token = SourceNormalizer.Tokenize(content);
        if (token.Count == 0) yield break;

        var words = token.Select(t => t.Text).ToList();

        foreach (var f in Winnowing.Compute(words))
        {
            var start = token[f.Position];
            var end = token[Math.Min(f.Position + Winnowing.DefaultK - 1, token.Count - 1)];

            yield return new SourceFingerprint
            {
                SubmissionFileId = submissionFileId,
                EntryPath = entryPath,
                Hash = f.Hash,
                Position = start.Offset,
                Length = end.Offset + end.Length - start.Offset,
            };
        }
    }

    private static async Task<string> ReadTextAsync(Stream s, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await s.CopyToAsync(buffer, ct);
        return ReadText(buffer.ToArray());
    }

    /// <summary>
    /// detectEncodingFromByteOrderMarks: submissions coming from Windows often carry a UTF-8
    /// BOM, and quite a few are saved as UTF-16. Read them straight as UTF-8 and a UTF-16 file
    /// comes out as a string with NUL characters interleaved — a meaningless token stream, and
    /// that submission never matches anybody.
    /// </summary>
    private static string ReadText(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var reader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
