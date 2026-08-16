using TeachHub.Domain.Common;

namespace TeachHub.Domain.Integrity;

/// <summary>
/// One Winnowing fingerprint of one submitted source file (Gap #14).
///
/// This table holds only numbers, never source code. That is intentional: a fingerprint cannot be
/// reversed back into code, so even if the table leaks, the students' submissions do not.
///
/// It deliberately does not also store <c>SubmissionId</c> or <c>AssignmentId</c>, even though the
/// comparison query always needs both: every piece of copied data is a place that can drift out of
/// sync when a student resubmits. The query joins through
/// <c>submission_files → submissions</c> — two joins on primary keys, far cheaper than a day spent
/// working out why the similarity report is showing submissions from a different assignment.
/// </summary>
public class SourceFingerprint : Entity
{
    public required Guid SubmissionFileId { get; set; }

    /// <summary>
    /// Path of the file inside the archive, null when the student submitted a single source file
    /// directly. Without this field the comparison screen knows "these two submissions match 80%"
    /// but cannot point out which file inside the zip matches which — meaning the instructor still
    /// has to hunt for it by hand.
    /// </summary>
    public string? EntryPath { get; set; }

    /// <summary>The k-gram hash value. <c>bigint</c> in the DB, indexed (Gap #14).</summary>
    public long Hash { get; set; }

    /// <summary>
    /// Position (character offset) of the k-gram in the ORIGINAL source, not the token index.
    ///
    /// Winnowing works on the normalized token sequence, so it returns token indexes; the
    /// application tier converts them back to character offsets before writing them down. Store
    /// token indexes instead and the side-by-side comparison screen can highlight nothing at all:
    /// it holds the real source code, while nobody stores the token table — rebuilding it would
    /// mean re-normalizing with exactly the same version of the algorithm, which locks the display
    /// screen hard to the internals of the tokenizer.
    /// </summary>
    public int Position { get; set; }

    /// <summary>How many characters this k-gram covers in the original source — the other end of the stretch to highlight.</summary>
    public int Length { get; set; }
}
