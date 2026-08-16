using System.Text;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Files;
using TeachHub.Domain.Integrity;

namespace TeachHub.Application.Features.Integrity;

/// <summary>
/// Tính dấu vân tay Winnowing cho một tệp đã nộp (Phase 6a, Gap #14).
///
/// Là <c>ICommand</c> chứ không phải <c>IQuery</c>: nó ghi. Nhờ marker <c>IBaseCommand</c>,
/// <c>TransactionBehavior</c> bọc sẵn cả cụm "xóa dấu vân tay cũ + ghi dấu vân tay mới" trong
/// một transaction — nửa chừng thất bại thì tệp mất sạch dấu vân tay và biến mất khỏi mọi báo
/// cáo trùng lặp mà không có gì báo.
///
/// Chạy lại được nhiều lần (Hangfire retry, giảng viên bấm "tính lại"): luôn xóa hết dấu vân
/// tay cũ của đúng tệp đó trước khi ghi. Không xóa thì mỗi lần chạy lại nhân đôi số dòng, và
/// vì so sánh dùng TẬP (bỏ trùng) nên con số phần trăm vẫn đúng — hỏng âm thầm, chỉ lộ ra khi
/// bảng phình gấp năm lần.
/// </summary>
public sealed record ComputeFingerprintsCommand(Guid SubmissionFileId) : ICommand<Result>;

public sealed class ComputeFingerprintsHandler(IAppDbContext db, IFileStorage storage)
    : ICommandHandler<ComputeFingerprintsCommand, Result>
{
    /// <summary>
    /// Nạp cả file nén vào bộ nhớ để đọc nhiều entry mà không mở lại storage hàng chục lần
    /// (với S3 là hàng chục lượt GET cùng một đối tượng). Trần 32 MB: bài nộp mã nguồn thật
    /// không bao giờ chạm tới, còn file nặng hơn thế gần như chắc chắn là ảnh, video hoặc
    /// thư mục build — băm chúng chỉ tốn chỗ.
    /// </summary>
    private const long MaxArchiveBytes = 32L * 1024 * 1024;

    private const long MaxEntryBytes = 2L * 1024 * 1024;

    /// <summary>Trần số tệp mã nguồn lấy trong một file nén — chặn bài nộp kèm cả node_modules.</summary>
    private const int MaxSourceEntries = 200;

    /// <summary>Trần dấu vân tay ghi cho MỘT tệp nộp, chặn tệp sinh tự động hàng trăm nghìn dòng.</summary>
    private const int MaxFingerprintsPerFile = 20_000;

    public async Task<Result> Handle(ComputeFingerprintsCommand request, CancellationToken ct)
    {
        var file = await db.SubmissionFiles
            .SingleOrDefaultAsync(f => f.Id == request.SubmissionFileId, ct);

        if (file is null) return Result.NotFound("Không tìm thấy tệp nộp.");

        // Dọn trước, luôn luôn — kể cả khi lát nữa không ghi được gì. Tệp vừa được nộp lại
        // bằng một bản không có mã nguồn thì dấu vân tay của bản cũ phải biến mất theo, nếu
        // không báo cáo sẽ so bài hiện tại với nội dung sinh viên đã thay thế.
        var cu = await db.SourceFingerprints
            .Where(f => f.SubmissionFileId == file.Id).ToListAsync(ct);

        if (cu.Count > 0) db.SourceFingerprints.RemoveRange(cu);

        if (file.FilePath is null)                        // nộp bằng link, không có gì để băm
        {
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }

        var moi = new List<SourceFingerprint>();

        if (SourceFileTypes.IsSourceCode(file.FileName))
        {
            await using var s = await storage.OpenReadAsync(file.FilePath, ct);
            moi.AddRange(BamMotTep(file.Id, null, await DocVanBanAsync(s, ct)));
        }
        else if (file.FileTree is { Files.Count: > 0 } cay && file.SizeBytes <= MaxArchiveBytes)
        {
            var nguon = cay.Files
                .Where(f => SourceFileTypes.IsSourceCode(f.Path))
                .Where(f => f.SizeBytes is > 0 and <= MaxEntryBytes)
                .OrderBy(f => f.Path)
                .Take(MaxSourceEntries)
                .ToList();

            if (nguon.Count > 0)
            {
                await using var goc = await storage.OpenReadAsync(file.FilePath, ct);
                using var bo = new MemoryStream();
                await goc.CopyToAsync(bo, ct);

                foreach (var e in nguon)
                {
                    if (moi.Count >= MaxFingerprintsPerFile) break;

                    bo.Position = 0;

                    try
                    {
                        // Đi qua SafeZipReader chứ không tự mở ZipArchive: đường dẫn entry là
                        // dữ liệu do sinh viên kiểm soát, và bộ luật chặn zip-slip chỉ được
                        // phép tồn tại ở MỘT chỗ (ADR-005).
                        var byteEntry = SafeZipReader.ReadEntry(bo, e.Path, ZipLimits.Default, MaxEntryBytes);
                        moi.AddRange(BamMotTep(file.Id, e.Path, DocVanBan(byteEntry)));
                    }
                    catch (UnsafeArchiveException)
                    {
                        // Entry hỏng hoặc độc hại: bỏ qua ĐÚNG entry đó rồi đi tiếp, không ném.
                        // Ném ở đây làm Hangfire retry cả file vì một tệp vốn dĩ không bao giờ
                        // đọc được, và cả bài nộp mất dấu vân tay vì một tệp rác.
                    }
                }
            }
        }

        if (moi.Count > MaxFingerprintsPerFile) moi = [.. moi.Take(MaxFingerprintsPerFile)];

        // Add tường minh qua DbSet, không thêm vào collection của entity đang tracked:
        // Entity.Id tự sinh UUIDv7 lúc khởi tạo nên Id đã khác default, và EF sẽ phát UPDATE
        // thay vì INSERT.
        db.SourceFingerprints.AddRange(moi);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }

    /// <summary>
    /// Chuẩn hóa → k-gram → winnowing, rồi quy chỉ số token về offset ký tự trong mã nguồn gốc
    /// để màn hình so sánh tô sáng được đúng đoạn.
    /// </summary>
    private static IEnumerable<SourceFingerprint> BamMotTep(
        Guid submissionFileId, string? entryPath, string noiDung)
    {
        var token = SourceNormalizer.Tokenize(noiDung);
        if (token.Count == 0) yield break;

        var chu = token.Select(t => t.Text).ToList();

        foreach (var f in Winnowing.Compute(chu))
        {
            var dau = token[f.Position];
            var cuoi = token[Math.Min(f.Position + Winnowing.DefaultK - 1, token.Count - 1)];

            yield return new SourceFingerprint
            {
                SubmissionFileId = submissionFileId,
                EntryPath = entryPath,
                Hash = f.Hash,
                Position = dau.Offset,
                Length = cuoi.Offset + cuoi.Length - dau.Offset,
            };
        }
    }

    private static async Task<string> DocVanBanAsync(Stream s, CancellationToken ct)
    {
        using var bo = new MemoryStream();
        await s.CopyToAsync(bo, ct);
        return DocVanBan(bo.ToArray());
    }

    /// <summary>
    /// detectEncodingFromByteOrderMarks: bài nộp từ Windows hay có BOM UTF-8, và không ít bài
    /// lưu UTF-16. Đọc thẳng bằng UTF-8 thì file UTF-16 ra một chuỗi ký tự NUL xen kẽ — chuỗi
    /// token vô nghĩa, và bài đó không bao giờ trùng với ai.
    /// </summary>
    private static string DocVanBan(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var reader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
