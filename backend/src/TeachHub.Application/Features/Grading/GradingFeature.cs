using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Submissions;
using TeachHub.Domain.Grading;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Grading;

public sealed record GradedSubmissionDto(
    Guid Id, decimal? Score, string? Feedback, SubmissionStatus Status,
    Dictionary<Guid, decimal> RubricScores);

/// <summary>
/// <c>ICommand</c> chứ không phải <c>IQuery</c>: TransactionBehavior nhận diện qua marker
/// <c>IBaseCommand</c> và bọc nguyên lời gọi trong một transaction. Ghi điểm và ghi
/// <see cref="GradeAuditLog"/> phải cùng sống hoặc cùng chết — một bản ghi điểm không có
/// dòng nhật ký là thứ không giải thích được sáu tháng sau.
/// </summary>
public sealed record GradeSubmissionCommand(
    Dictionary<Guid, decimal> RubricScores, decimal? DirectScore, string? Feedback)
    : ICommand<Result<GradedSubmissionDto>>
{
    public Guid SubmissionId { get; init; }
    public Guid GraderId { get; init; }
}

public sealed class GradeSubmissionValidator : AbstractValidator<GradeSubmissionCommand>
{
    public GradeSubmissionValidator()
    {
        // Trần của TỪNG tiêu chí không kiểm được ở đây: muốn biết tiêu chí tối đa mấy điểm
        // thì phải đọc rubric từ database, mà validator là hàm thuần chạy TRƯỚC transaction.
        // Handler đọc rubric rồi trả Result.Invalid → 400.
        RuleForEach(x => x.RubricScores).Must(kv => kv.Value >= 0m)
            .WithMessage("Điểm tiêu chí không được âm.");

        RuleFor(x => x.DirectScore).InclusiveBetween(0m, 10m)
            .When(x => x.DirectScore is not null)
            .WithMessage("Điểm phải nằm trong khoảng 0 đến 10.");

        RuleFor(x => x.Feedback).MaximumLength(4000);

        RuleFor(x => x)
            .Must(x => x.RubricScores.Count > 0 || x.DirectScore is not null)
            .WithMessage("Phải chấm theo rubric hoặc nhập điểm trực tiếp.");
    }
}

public sealed class GradeSubmissionHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<GradeSubmissionCommand, Result<GradedSubmissionDto>>
{
    public async Task<Result<GradedSubmissionDto>> Handle(
        GradeSubmissionCommand request, CancellationToken ct)
    {
        var bai = await db.Submissions.SingleOrDefaultAsync(s => s.Id == request.SubmissionId, ct);

        // Chỉ giảng viên dạy lớp mới chấm được. Không phải lớp mình ⇒ 404, không bao giờ 403.
        if (bai is null || !await db.CourseOfferings.AnyAsync(o => o.Id == bai.OfferingId &&
                db.Subjects.Any(s => s.Id == o.SubjectId && s.InstructorId == request.GraderId), ct))
            return Result<GradedSubmissionDto>.NotFound("Không tìm thấy bài nộp.");

        if (bai.Status == SubmissionStatus.Draft)
            return Result<GradedSubmissionDto>.Invalid("Bài này chưa nộp chính thức nên chưa chấm được.");

        decimal diem;

        if (request.RubricScores.Count > 0)
        {
            var rubricId = await LayRubricIdAsync(bai, ct);
            if (rubricId is null)
                return Result<GradedSubmissionDto>.Invalid(
                    "Bài tập này chưa gắn rubric. Hãy gắn rubric hoặc nhập điểm trực tiếp.");

            var rubric = await db.Rubrics.AsNoTracking()
                .Include(r => r.Criteria)
                .SingleOrDefaultAsync(r => r.Id == rubricId, ct);

            if (rubric is null)
                return Result<GradedSubmissionDto>.Invalid("Không tìm thấy rubric của bài tập này.");

            var la = request.RubricScores.Keys.Where(k => rubric.Criteria.All(c => c.Id != k)).ToList();
            if (la.Count > 0)
                return Result<GradedSubmissionDto>.Invalid("Có tiêu chí không thuộc rubric của bài tập này.");

            try
            {
                // Rubric.Score tự chặn điểm âm và điểm vượt trần từng tiêu chí, dùng decimal
                // và làm tròn AwayFromZero — cùng một phép tính với mọi chỗ khác trong hệ thống.
                diem = rubric.Score(request.RubricScores);
            }
            catch (ArgumentOutOfRangeException e)
            {
                return Result<GradedSubmissionDto>.Invalid(e.Message.Split(" (Parameter")[0]);
            }
        }
        else
        {
            diem = Math.Round(request.DirectScore!.Value, 2, MidpointRounding.AwayFromZero);
        }

        var truoc = bai.Score;
        var now = clock.GetUtcNow();

        // RawScore = điểm giảng viên vừa chấm, TRƯỚC mọi mức phạt (Phase 6b). Ghi ở đây chứ
        // không đợi lúc áp phạt: thiếu nó thì lệnh áp phạt lần thứ hai sẽ trừ chồng lên điểm
        // đã trừ, và mỗi lần sửa hạn nộp rồi bấm tính lại là điểm sinh viên tụt thêm một nấc.
        bai.RawScore = diem;
        bai.Score = diem;
        bai.RubricScores = new Dictionary<Guid, decimal>(request.RubricScores);
        bai.Feedback = request.Feedback;
        bai.GradedBy = request.GraderId;
        bai.GradedAt = now;

        // graded ≠ returned là chủ ý: giảng viên chấm rải rác nhiều ngày, và nếu điểm hiện
        // ngay khi vừa chấm thì sinh viên được chấm trước sẽ so bì với người chưa được chấm.
        if (bai.Status != SubmissionStatus.Returned) bai.Status = SubmissionStatus.Graded;

        db.GradeAuditLogs.Add(new GradeAuditLog
        {
            OfferingId = bai.OfferingId,
            EnrollmentId = bai.EnrollmentId ?? Guid.Empty,
            TargetType = GradeTargetType.Submission,
            TargetId = bai.Id,
            OldValue = truoc,
            NewValue = diem,
            ChangedBy = request.GraderId,
            ChangedAt = now,
        });

        // Bài nhóm: mọi thành viên nhận cùng điểm và cùng nhận xét (mặc định MVP —
        // điểm riêng từng thành viên thuộc Phase 6). Không cần ghi nhiều bản: bài nộp
        // thuộc về nhóm, và sổ điểm quy chiếu qua group_id.
        await db.SaveChangesAsync(ct);

        return Result<GradedSubmissionDto>.Ok(new GradedSubmissionDto(
            bai.Id, bai.Score, bai.Feedback, bai.Status, bai.RubricScores));
    }

    private async Task<Guid?> LayRubricIdAsync(Submission bai, CancellationToken ct) =>
        bai.AssignmentId is not null
            ? await db.Assignments.Where(a => a.Id == bai.AssignmentId)
                                  .Select(a => a.RubricId).SingleOrDefaultAsync(ct)
            : await db.FinalAssessments.Where(f => f.Id == bai.FinalAssessmentId)
                                       .Select(f => f.RubricId).SingleOrDefaultAsync(ct);
}

// ── Trả bài ─────────────────────────────────────────────────────────────────────────────

public sealed record ReturnSubmissionCommand(Guid SubmissionId, Guid ActorId) : ICommand<Result>;

public sealed class ReturnSubmissionHandler(IAppDbContext db)
    : ICommandHandler<ReturnSubmissionCommand, Result>
{
    public async Task<Result> Handle(ReturnSubmissionCommand request, CancellationToken ct)
    {
        var bai = await db.Submissions.SingleOrDefaultAsync(s => s.Id == request.SubmissionId, ct);

        if (bai is null || !await db.CourseOfferings.AnyAsync(o => o.Id == bai.OfferingId &&
                db.Subjects.Any(s => s.Id == o.SubjectId && s.InstructorId == request.ActorId), ct))
            return Result.NotFound("Không tìm thấy bài nộp.");

        if (bai.Score is null)
            return Result.Invalid("Bài chưa chấm nên chưa trả được.");

        bai.Status = SubmissionStatus.Returned;
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}

// ── Danh sách bài cần chấm của một lớp ──────────────────────────────────────────────────

public sealed record ToGradeItemDto(
    Guid SubmissionId, Guid? AssignmentId, Guid? FinalAssessmentId, string Title,
    string? GroupName, string StudentNames, DateTimeOffset? SubmittedAt, bool IsLate,
    SubmissionStatus Status, decimal? Score);

public sealed record GetSubmissionsToGradeQuery(Guid OfferingId, Guid InstructorId)
    : IQuery<Result<List<ToGradeItemDto>>>;

public sealed class GetSubmissionsToGradeHandler(IAppDbContext db)
    : IQueryHandler<GetSubmissionsToGradeQuery, Result<List<ToGradeItemDto>>>
{
    public async Task<Result<List<ToGradeItemDto>>> Handle(
        GetSubmissionsToGradeQuery request, CancellationToken ct)
    {
        if (!await Offerings.OfferingOwnership.OwnsOfferingAsync(
                db, request.OfferingId, request.InstructorId, ct))
            return Result<List<ToGradeItemDto>>.NotFound("Không tìm thấy lớp học phần.");

        // Một truy vấn có projection thay vì nạp rồi lặp: số câu truy vấn phải là hằng số,
        // không phụ thuộc sĩ số. Với lớp 40 người, N+1 ở đây là 40 vòng gọi database.
        var ds = await db.Submissions.AsNoTracking()
            .Where(s => s.OfferingId == request.OfferingId
                     && s.IsCurrent
                     && s.Status != SubmissionStatus.Draft)
            .Select(s => new
            {
                s.Id, s.AssignmentId, s.FinalAssessmentId, s.GroupId,
                s.SubmittedAt, s.IsLate, s.Status, s.Score,
                TenBaiTap = s.AssignmentId != null
                    ? db.Assignments.Where(a => a.Id == s.AssignmentId).Select(a => a.Title).FirstOrDefault()
                    : db.FinalAssessments.Where(f => f.Id == s.FinalAssessmentId).Select(f => f.Title).FirstOrDefault(),
                TenNhom = s.GroupId != null
                    ? db.StudentGroups.Where(g => g.Id == s.GroupId).Select(g => g.Name).FirstOrDefault()
                    : null,
                TenCaNhan = db.Enrollments.Where(e => e.Id == s.EnrollmentId)
                                          .Select(e => e.FullName).FirstOrDefault(),
                TenThanhVien = s.GroupId == null
                    ? new List<string>()
                    : db.GroupMembers.Where(m => m.GroupId == s.GroupId)
                        .Join(db.Enrollments, m => m.EnrollmentId, e => e.Id, (m, e) => e.FullName)
                        .ToList(),
            })
            .OrderBy(s => s.Status).ThenByDescending(s => s.SubmittedAt)
            .ToListAsync(ct);

        return Result<List<ToGradeItemDto>>.Ok([.. ds.Select(s => new ToGradeItemDto(
            s.Id, s.AssignmentId, s.FinalAssessmentId, s.TenBaiTap ?? "(không rõ)",
            s.TenNhom,
            s.TenNhom is null ? s.TenCaNhan ?? "" : string.Join(", ", s.TenThanhVien),
            s.SubmittedAt, s.IsLate, s.Status, s.Score))]);
    }
}
