using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Files;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Submissions.Queries.GetSubmission;

public sealed record GetSubmissionQuery(Guid SubmissionId, Guid UserId) : IQuery<Result<SubmissionDto>>;

public sealed class GetSubmissionHandler(IAppDbContext db)
    : IQueryHandler<GetSubmissionQuery, Result<SubmissionDto>>
{
    public async Task<Result<SubmissionDto>> Handle(GetSubmissionQuery request, CancellationToken ct)
    {
        if (!await SubmissionAccess.CanReadAsync(db, request.SubmissionId, request.UserId, ct))
            return Result<SubmissionDto>.NotFound("Không tìm thấy bài nộp.");

        var s = await db.Submissions.AsNoTracking()
            .Include(x => x.Files)
            .SingleAsync(x => x.Id == request.SubmissionId, ct);

        return Result<SubmissionDto>.Ok(SubmissionDto.From(s));
    }
}

// ── Bài nộp hiện hành + lịch sử của chính sinh viên cho một bài tập ─────────────────────

public sealed record MyAssignmentSubmissionDto(
    SubmissionDto? Current, List<SubmissionDto> History);

public sealed record GetMySubmissionQuery(Guid AssignmentId, Guid StudentUserId)
    : IQuery<Result<MyAssignmentSubmissionDto>>;

public sealed class GetMySubmissionHandler(IAppDbContext db)
    : IQueryHandler<GetMySubmissionQuery, Result<MyAssignmentSubmissionDto>>
{
    public async Task<Result<MyAssignmentSubmissionDto>> Handle(
        GetMySubmissionQuery request, CancellationToken ct)
    {
        var ghiDanh = await SubmissionAccess.EnrollmentForAssignmentAsync(
            db, request.AssignmentId, request.StudentUserId, ct);

        if (ghiDanh is null)
            return Result<MyAssignmentSubmissionDto>.NotFound("Không tìm thấy bài tập.");

        var tatCa = await db.Submissions.AsNoTracking()
            .Include(s => s.Files)
            .Where(s => s.AssignmentId == request.AssignmentId
                     && s.EnrollmentId == ghiDanh.EnrollmentId)
            .OrderByDescending(s => s.AttemptNo)
            .ToListAsync(ct);

        return Result<MyAssignmentSubmissionDto>.Ok(new MyAssignmentSubmissionDto(
            tatCa.Where(s => s.IsCurrent).Select(SubmissionDto.From).FirstOrDefault(),
            [.. tatCa.Where(s => !s.IsCurrent).Select(SubmissionDto.From)]));
    }
}

// ── Cây thư mục bên trong một tệp nén ───────────────────────────────────────────────────

public sealed record FileTreeEntryDto(string Path, long SizeBytes);

public sealed record FileTreeDto(
    Guid FileId, string FileName, FileProcessingStatus ProcessingStatus,
    string? ProcessingError, bool? StructureCheckPassed,
    List<string> MissingStructureItems, List<FileTreeEntryDto> Entries);

public sealed record GetFileTreeQuery(Guid SubmissionId, Guid FileId, Guid UserId)
    : IQuery<Result<FileTreeDto>>;

public sealed class GetFileTreeHandler(IAppDbContext db)
    : IQueryHandler<GetFileTreeQuery, Result<FileTreeDto>>
{
    public async Task<Result<FileTreeDto>> Handle(GetFileTreeQuery request, CancellationToken ct)
    {
        if (!await SubmissionAccess.CanReadAsync(db, request.SubmissionId, request.UserId, ct))
            return Result<FileTreeDto>.NotFound("Không tìm thấy bài nộp.");

        var f = await db.SubmissionFiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.FileId && x.SubmissionId == request.SubmissionId, ct);

        if (f is null) return Result<FileTreeDto>.NotFound("Không tìm thấy tệp.");

        return Result<FileTreeDto>.Ok(new FileTreeDto(
            f.Id, f.FileName, f.ProcessingStatus, f.ProcessingError,
            f.StructureCheckPassed, f.MissingStructureItems,
            [.. (f.FileTree?.Files ?? []).Select(n => new FileTreeEntryDto(n.Path, n.SizeBytes))]));
    }
}

// ── Nội dung một entry trong tệp nén ────────────────────────────────────────────────────

public sealed record ZipEntryContent(string FileName, byte[] Content);

public sealed record GetZipEntryQuery(Guid SubmissionId, Guid FileId, string Path, Guid UserId)
    : IQuery<Result<ZipEntryContent>>;

public sealed class GetZipEntryHandler(IAppDbContext db, IFileStorage storage)
    : IQueryHandler<GetZipEntryQuery, Result<ZipEntryContent>>
{
    /// <summary>Chỉ xem trực tiếp tệp nhỏ. Tệp lớn tải về, không nạp vào bộ nhớ máy chủ.</summary>
    private const long ToiDaXemTrucTiep = 1024 * 1024;

    public async Task<Result<ZipEntryContent>> Handle(GetZipEntryQuery request, CancellationToken ct)
    {
        if (!await SubmissionAccess.CanReadAsync(db, request.SubmissionId, request.UserId, ct))
            return Result<ZipEntryContent>.NotFound("Không tìm thấy bài nộp.");

        var f = await db.SubmissionFiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.FileId && x.SubmissionId == request.SubmissionId, ct);

        if (f?.FilePath is null) return Result<ZipEntryContent>.NotFound("Không tìm thấy tệp.");

        try
        {
            await using var stream = await storage.OpenReadAsync(f.FilePath, ct);

            // SafeZipReader.ReadEntry tự chạy lại NormalizeAndGuard trên tham số path.
            // Cây thư mục đã an toàn, nhưng path này đến từ query string và người dùng sửa được.
            var noiDung = SafeZipReader.ReadEntry(
                stream, request.Path, ZipLimits.Default, ToiDaXemTrucTiep);

            return Result<ZipEntryContent>.Ok(
                new ZipEntryContent(System.IO.Path.GetFileName(request.Path), noiDung));
        }
        catch (UnsafeArchiveException e)
        {
            return Result<ZipEntryContent>.Invalid(e.Message);
        }
    }
}
