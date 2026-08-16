using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Grading;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Integrity;

public sealed record ScoreBreakdownDto(
    Guid SubmissionId,
    decimal RawScore,
    int DaysLate,
    decimal LatePenaltyPercent,
    int HintsUnlocked,
    decimal HintPenaltyPercent,
    decimal PenaltyPoints,
    decimal FinalScore);

/// <summary>
/// Áp phạt nộp trễ và phạt mở gợi ý lên điểm đã chấm (Phase 6b).
///
/// Tách khỏi lệnh chấm điểm thay vì gộp vào là chủ ý: phạt phụ thuộc vào hạn nộp và nhật ký
/// mở gợi ý — hai thứ đổi được SAU khi đã chấm. Giảng viên gia hạn nộp bài cho cả lớp rồi
/// phải chấm lại từng bài thì không ai làm, nên phải có một lệnh chạy lại được.
///
/// CHẠY LẠI ĐƯỢC NHIỀU LẦN: luôn tính từ <c>RawScore</c>, không bao giờ tính chồng lên điểm
/// đã phạt. Tính chồng thì mỗi lần bấm "tính lại" là điểm sinh viên tụt thêm một nấc, âm thầm,
/// và không có đường quay lại vì điểm gốc đã bị ghi đè.
/// </summary>
public sealed record ApplySubmissionPenaltiesCommand : ICommand<Result<ScoreBreakdownDto>>
{
    public Guid SubmissionId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class ApplySubmissionPenaltiesHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<ApplySubmissionPenaltiesCommand, Result<ScoreBreakdownDto>>
{
    public async Task<Result<ScoreBreakdownDto>> Handle(
        ApplySubmissionPenaltiesCommand request, CancellationToken ct)
    {
        var bai = await db.Submissions.SingleOrDefaultAsync(s => s.Id == request.SubmissionId, ct);

        if (bai is null || !await db.CourseOfferings.AnyAsync(o => o.Id == bai.OfferingId &&
                db.Subjects.Any(s => s.Id == o.SubjectId && s.InstructorId == request.InstructorId), ct))
            return Result<ScoreBreakdownDto>.NotFound("Không tìm thấy bài nộp.");

        // RawScore ?? Score: các bài chấm TRƯỚC Phase 6 chưa có cột RawScore, và lúc đó Score
        // chính là điểm gốc. Lấp cột một lần ở đây thay vì viết một migration dữ liệu — mọi
        // bài chưa từng bị phạt đều thỏa RawScore == Score.
        if ((bai.RawScore ?? bai.Score) is not { } diemGoc)
            return Result<ScoreBreakdownDto>.Invalid("Bài này chưa chấm nên chưa áp mức phạt được.");

        bai.RawScore = diemGoc;

        var (han, phatMoiNgay) = await LayLuatPhatAsync(bai, ct);
        var tyLeGoiY = await LayTyLeGoiYAsync(bai, ct);

        var phan = SubmissionScoring.Apply(diemGoc, phatMoiNgay, han, bai.SubmittedAt, tyLeGoiY);

        var truoc = bai.Score;
        var now = clock.GetUtcNow();

        bai.Score = phan.FinalScore;
        bai.DaysLate = phan.DaysLate;
        bai.LatePenaltyPercent = phan.LatePenaltyPercent;
        bai.HintPenaltyPercent = phan.HintPenaltyPercent;
        bai.IsLate = phan.DaysLate > 0;

        // Ghi nhật ký kể cả khi con số không đổi thì bảng phình vì mỗi lần bấm "tính lại".
        // Chỉ ghi khi điểm THẬT SỰ đổi — đó mới là thứ cần dựng lại được khi bị chất vấn.
        if (truoc != phan.FinalScore)
            db.GradeAuditLogs.Add(new GradeAuditLog
            {
                OfferingId = bai.OfferingId,
                EnrollmentId = bai.EnrollmentId ?? Guid.Empty,
                TargetType = GradeTargetType.Submission,
                TargetId = bai.Id,
                OldValue = truoc,
                NewValue = phan.FinalScore,
                ChangedBy = request.InstructorId,
                ChangedAt = now,
                Reason = $"Áp mức phạt: trễ {phan.DaysLate} ngày (−{phan.LatePenaltyPercent}%), " +
                         $"mở {phan.HintsUnlocked} gợi ý (−{phan.HintPenaltyPercent}%).",
            });

        await db.SaveChangesAsync(ct);

        return Result<ScoreBreakdownDto>.Ok(new ScoreBreakdownDto(
            bai.Id, phan.RawScore, phan.DaysLate, phan.LatePenaltyPercent,
            phan.HintsUnlocked, phan.HintPenaltyPercent, phan.PenaltyPoints, phan.FinalScore));
    }

    /// <summary>
    /// Hạn nộp và mức phạt mỗi ngày. <c>FinalAssessment</c> có hạn nộp nhưng KHÔNG có
    /// <c>LatePenaltyPercentPerDay</c> trong mô hình hiện tại, nên đợt cuối môn ghi nhận số
    /// ngày trễ mà không trừ điểm. Bịa ra một mức mặc định ở đây là trừ điểm theo một luật
    /// giảng viên chưa từng khai và chưa từng thấy trên màn hình nào.
    /// </summary>
    private async Task<(DateTimeOffset? DueAt, decimal PercentPerDay)> LayLuatPhatAsync(
        Submission bai, CancellationToken ct)
    {
        if (bai.AssignmentId is { } baiTapId)
        {
            var a = await db.Assignments.AsNoTracking()
                .Where(x => x.Id == baiTapId)
                .Select(x => new { x.DueAt, x.LatePenaltyPercentPerDay })
                .SingleOrDefaultAsync(ct);

            return a is null ? (null, 0m) : (a.DueAt, a.LatePenaltyPercentPerDay);
        }

        var f = await db.FinalAssessments.AsNoTracking()
            .Where(x => x.Id == bai.FinalAssessmentId)
            .Select(x => x.DueAt)
            .SingleOrDefaultAsync(ct);

        return (f, 0m);
    }

    /// <summary>
    /// Tỉ lệ phạt của các gợi ý đã mở, lấy từ <c>HintUnlock.PenaltyPercentApplied</c> chứ
    /// không từ mức phạt hiện tại của gợi ý — sinh viên phải được giữ đúng điều kiện họ đã
    /// thấy lúc bấm mở.
    ///
    /// Bài NHÓM lấy HỢP các gợi ý mọi thành viên đã mở: cả nhóm nộp chung một bài, nên gợi ý
    /// bất kỳ ai mở cũng đã giúp làm ra chính bài đó. Chỉ tính người đứng tên nộp thì cả nhóm
    /// cử một bạn "hy sinh" đi mở hết gợi ý là né sạch mức phạt.
    /// </summary>
    private async Task<List<decimal>> LayTyLeGoiYAsync(Submission bai, CancellationToken ct)
    {
        if (bai.AssignmentId is not { } baiTapId) return [];

        var ghiDanh = bai.GroupId is { } nhomId
            ? await db.GroupMembers.AsNoTracking()
                .Where(m => m.GroupId == nhomId).Select(m => m.EnrollmentId).ToListAsync(ct)
            : bai.EnrollmentId is { } mot ? [mot] : [];

        if (ghiDanh.Count == 0) return [];

        // Gộp theo gợi ý: hai thành viên cùng mở một gợi ý thì cả nhóm chỉ bị trừ MỘT lần.
        return await (from u in db.HintUnlocks.AsNoTracking()
                      join h in db.AssignmentHints.AsNoTracking() on u.AssignmentHintId equals h.Id
                      where h.AssignmentId == baiTapId && ghiDanh.Contains(u.EnrollmentId)
                      group u by u.AssignmentHintId into g
                      select g.Max(x => x.PenaltyPercentApplied))
                     .ToListAsync(ct);
    }
}
