using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Offerings;
using TeachHub.Domain.Integrity;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Integrity;

/// <param name="SimilarityPercent">Containment — tỉ lệ bài NHỎ hơn nằm trong bài kia.</param>
/// <param name="IsWarning">
/// Vượt ngưỡng ⇒ ĐÁNG XEM LẠI. KHÔNG phải kết luận gian lận, và tuyệt đối không kéo theo bất
/// kỳ thay đổi điểm nào (Rủi ro #3 của SRS mục 11).
/// </param>
public sealed record SimilarityPairDto(
    Guid SubmissionAId, string OwnerA,
    Guid SubmissionBId, string OwnerB,
    decimal SimilarityPercent, decimal JaccardPercent,
    int SharedFingerprints, int TotalA, int TotalB,
    bool IsWarning);

/// <param name="SubmissionsWithoutFingerprints">
/// Bài không có dấu vân tay nào: nộp bằng link, nộp file Word, hoặc job băm chưa chạy xong.
/// Phải hiện con số này ra — không thì giảng viên thấy "0 cặp cảnh báo" và tưởng cả lớp trong
/// sạch, trong khi thật ra hệ thống chưa đọc được bài nào.
/// </param>
public sealed record SimilarityReportDto(
    Guid AssignmentId,
    Guid OfferingId,
    decimal ThresholdPercent,
    int SubmissionsCompared,
    int SubmissionsWithoutFingerprints,
    int WarningCount,
    List<SimilarityPairDto> Pairs);

/// <summary>
/// Báo cáo trùng lặp trong phạm vi MỘT bài tập của MỘT lớp — đúng phạm vi plan chốt.
///
/// Không so chéo giữa các lớp và các học kỳ ở MVP: mỗi giảng viên độc lập (ADR-012), nên so
/// chéo lớp của người khác là để lộ mã nguồn sinh viên lớp khác qua chính màn hình so sánh.
/// </summary>
public sealed record GetSimilarityReportQuery(
    Guid AssignmentId, Guid OfferingId, Guid InstructorId, decimal? ThresholdPercent)
    : IQuery<Result<SimilarityReportDto>>;

public sealed class GetSimilarityReportHandler(IAppDbContext db)
    : IQueryHandler<GetSimilarityReportQuery, Result<SimilarityReportDto>>
{
    /// <summary>Trần số cặp trả về. Lớp 40 người có 780 cặp; trả hết là một bảng không ai đọc.</summary>
    private const int MaxPairs = 200;

    public async Task<Result<SimilarityReportDto>> Handle(
        GetSimilarityReportQuery request, CancellationToken ct)
    {
        // Lọc quyền sở hữu NGAY trong câu truy vấn. Không phải lớp của mình ⇒ 404, không bao
        // giờ 403: 403 xác nhận id đó có thật, và dò id tuần tự sẽ đếm được lớp của đồng nghiệp.
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.InstructorId, ct))
            return Result<SimilarityReportDto>.NotFound("Không tìm thấy lớp học phần.");

        var thuocLop = await db.Assignments.AsNoTracking()
            .AnyAsync(a => a.Id == request.AssignmentId
                        && db.CourseOfferings.Any(o => o.Id == request.OfferingId
                                                    && o.SubjectVersionId == a.SubjectVersionId), ct);

        if (!thuocLop) return Result<SimilarityReportDto>.NotFound("Không tìm thấy bài tập trong lớp này.");

        var nguong = request.ThresholdPercent ?? SimilarityThresholds.DefaultWarnPercent;

        // Một câu truy vấn cho toàn bộ dấu vân tay của bài tập này trong lớp này. Distinct ở
        // phía database: so sánh dùng TẬP, nên kéo bản trùng về chỉ tốn băng thông.
        var van = await (from f in db.SourceFingerprints.AsNoTracking()
                         join sf in db.SubmissionFiles.AsNoTracking() on f.SubmissionFileId equals sf.Id
                         join s in db.Submissions.AsNoTracking() on sf.SubmissionId equals s.Id
                         where s.AssignmentId == request.AssignmentId
                            && s.OfferingId == request.OfferingId
                            && s.IsCurrent
                            && s.Status != SubmissionStatus.Draft
                         select new { SubmissionId = s.Id, f.Hash })
                        .Distinct()
                        .ToListAsync(ct);

        var theoBai = van.GroupBy(x => x.SubmissionId)
                         .ToDictionary(g => g.Key, g => g.Select(x => x.Hash).ToHashSet());

        var baiNop = await db.Submissions.AsNoTracking()
            .Where(s => s.AssignmentId == request.AssignmentId
                     && s.OfferingId == request.OfferingId
                     && s.IsCurrent
                     && s.Status != SubmissionStatus.Draft)
            .Select(s => new
            {
                s.Id,
                TenNhom = s.GroupId != null
                    ? db.StudentGroups.Where(g => g.Id == s.GroupId).Select(g => g.Name).FirstOrDefault()
                    : null,
                TenCaNhan = db.Enrollments.Where(e => e.Id == s.EnrollmentId)
                                          .Select(e => e.FullName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var ten = baiNop.ToDictionary(s => s.Id, s => s.TenNhom ?? s.TenCaNhan ?? "(không rõ)");

        // Chỉ so những bài THỰC SỰ có đủ dấu vân tay. Tệp mười dòng chỉ sinh vài dấu vân tay,
        // và hai tệp mười dòng cùng in "Hello World" sẽ trùng 100% một cách vô nghĩa — mỗi
        // cảnh báo vô nghĩa là một bước tiến tới việc giảng viên ngừng đọc danh sách này.
        var duLieu = theoBai
            .Where(kv => kv.Value.Count >= SimilarityThresholds.MinFingerprints)
            .ToList();

        var cap = new List<SimilarityPairDto>();

        for (var i = 0; i < duLieu.Count; i++)
        {
            for (var j = i + 1; j < duLieu.Count; j++)
            {
                var kq = SimilarityScorer.Compare(duLieu[i].Value, duLieu[j].Value);
                if (kq.SharedHashes == 0) continue;

                cap.Add(new SimilarityPairDto(
                    duLieu[i].Key, ten.GetValueOrDefault(duLieu[i].Key, "(không rõ)"),
                    duLieu[j].Key, ten.GetValueOrDefault(duLieu[j].Key, "(không rõ)"),
                    kq.ContainmentPercent, kq.JaccardPercent,
                    kq.SharedHashes, kq.TotalA, kq.TotalB,
                    SimilarityThresholds.ShouldWarn(kq.ContainmentPercent, nguong)));
            }
        }

        var sapXep = cap.OrderByDescending(p => p.SimilarityPercent)
                        .ThenByDescending(p => p.SharedFingerprints)
                        .ToList();

        return Result<SimilarityReportDto>.Ok(new SimilarityReportDto(
            request.AssignmentId,
            request.OfferingId,
            nguong,
            duLieu.Count,
            baiNop.Count - duLieu.Count,
            sapXep.Count(p => p.IsWarning),
            [.. sapXep.Take(MaxPairs)]));
    }
}

// ── So sánh cạnh nhau hai bài cụ thể ────────────────────────────────────────────────────

/// <param name="Ranges">
/// Các đoạn (offset ký tự, độ dài) trong tệp này cũng xuất hiện ở bài kia — dữ liệu để tô sáng.
/// </param>
public sealed record MatchedFileDto(
    Guid SubmissionFileId, string FileName, string? EntryPath,
    List<MatchedRangeDto> Ranges);

public sealed record MatchedRangeDto(int Position, int Length);

public sealed record SubmissionComparisonDto(
    Guid SubmissionAId, Guid SubmissionBId,
    decimal SimilarityPercent, decimal JaccardPercent, int SharedFingerprints,
    List<MatchedFileDto> FilesA, List<MatchedFileDto> FilesB);

public sealed record CompareSubmissionsQuery(Guid SubmissionAId, Guid SubmissionBId, Guid InstructorId)
    : IQuery<Result<SubmissionComparisonDto>>;

public sealed class CompareSubmissionsHandler(IAppDbContext db)
    : IQueryHandler<CompareSubmissionsQuery, Result<SubmissionComparisonDto>>
{
    public async Task<Result<SubmissionComparisonDto>> Handle(
        CompareSubmissionsQuery request, CancellationToken ct)
    {
        if (request.SubmissionAId == request.SubmissionBId)
            return Result<SubmissionComparisonDto>.Invalid("Không so sánh một bài với chính nó.");

        // Cả HAI bài phải thuộc lớp của người gọi, và phải thuộc CÙNG một bài tập. Thiếu vế
        // thứ hai thì một giảng viên so bài của hai lớp khác nhau của chính mình cũng được —
        // vô hại — nhưng thiếu vế thứ nhất là để lộ mã nguồn sinh viên của đồng nghiệp.
        var ds = await db.Submissions.AsNoTracking()
            .Where(s => (s.Id == request.SubmissionAId || s.Id == request.SubmissionBId)
                     && db.CourseOfferings.Any(o => o.Id == s.OfferingId
                            && db.Subjects.Any(su => su.Id == o.SubjectId
                                                  && su.InstructorId == request.InstructorId)))
            .Select(s => new { s.Id, s.AssignmentId, s.FinalAssessmentId })
            .ToListAsync(ct);

        if (ds.Count != 2)
            return Result<SubmissionComparisonDto>.NotFound("Không tìm thấy bài nộp.");

        if (ds[0].AssignmentId != ds[1].AssignmentId ||
            ds[0].FinalAssessmentId != ds[1].FinalAssessmentId)
            return Result<SubmissionComparisonDto>.Invalid(
                "Hai bài nộp không cùng một bài tập nên không so sánh được.");

        var a = await LayVanAsync(request.SubmissionAId, ct);
        var b = await LayVanAsync(request.SubmissionBId, ct);

        var kq = SimilarityScorer.Compare(a.SelectMany(x => x.Value).Select(f => f.Hash),
                                          b.SelectMany(x => x.Value).Select(f => f.Hash));

        var hashA = a.SelectMany(x => x.Value).Select(f => f.Hash).ToHashSet();
        var hashB = b.SelectMany(x => x.Value).Select(f => f.Hash).ToHashSet();

        return Result<SubmissionComparisonDto>.Ok(new SubmissionComparisonDto(
            request.SubmissionAId, request.SubmissionBId,
            kq.ContainmentPercent, kq.JaccardPercent, kq.SharedHashes,
            DungDto(a, hashB), DungDto(b, hashA)));
    }

    private async Task<Dictionary<(Guid FileId, string FileName, string? EntryPath), List<SourceFingerprint>>>
        LayVanAsync(Guid submissionId, CancellationToken ct)
    {
        var ds = await (from f in db.SourceFingerprints.AsNoTracking()
                        join sf in db.SubmissionFiles.AsNoTracking() on f.SubmissionFileId equals sf.Id
                        where sf.SubmissionId == submissionId
                        select new { sf.FileName, Van = f })
                       .ToListAsync(ct);

        return ds.GroupBy(x => (x.Van.SubmissionFileId, x.FileName, x.Van.EntryPath))
                 .ToDictionary(g => g.Key, g => g.Select(x => x.Van).ToList());
    }

    private static List<MatchedFileDto> DungDto(
        Dictionary<(Guid FileId, string FileName, string? EntryPath), List<SourceFingerprint>> van,
        HashSet<long> hashKia) =>
        [.. van
            .Select(kv => new MatchedFileDto(
                kv.Key.FileId, kv.Key.FileName, kv.Key.EntryPath,
                [.. kv.Value.Where(f => hashKia.Contains(f.Hash))
                            .OrderBy(f => f.Position)
                            .Select(f => new MatchedRangeDto(f.Position, f.Length))]))
            .Where(f => f.Ranges.Count > 0)
            .OrderByDescending(f => f.Ranges.Count)];
}
