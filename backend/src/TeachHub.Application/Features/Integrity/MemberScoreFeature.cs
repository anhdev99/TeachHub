using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Grading;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Integrity;

public sealed record MemberEffectiveScoreDto(
    Guid EnrollmentId, string StudentName, decimal? Score, bool IsOverridden, string? Reason);

/// <param name="Score">null ⇒ XÓA điểm riêng, thành viên đó quay về nhận điểm chung của nhóm.</param>
public sealed record MemberScoreInput(Guid EnrollmentId, decimal? Score, string? Reason);

/// <summary>
/// Chấm điểm riêng cho từng thành viên của một bài nộp nhóm (SRS UC-10b).
///
/// <c>ICommand</c> nên <c>TransactionBehavior</c> bọc sẵn: đặt điểm cho bốn người và ghi bốn
/// dòng nhật ký phải cùng sống hoặc cùng chết. Nửa chừng thất bại thì hai người có điểm riêng,
/// hai người không, và không có dòng nhật ký nào giải thích vì sao.
/// </summary>
public sealed record SetMemberScoresCommand(List<MemberScoreInput> Scores)
    : ICommand<Result<List<MemberEffectiveScoreDto>>>
{
    public Guid SubmissionId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class SetMemberScoresValidator : AbstractValidator<SetMemberScoresCommand>
{
    public SetMemberScoresValidator()
    {
        // Luật thuần hình dạng trên chính payload ⇒ validator. "Enrollment này có thuộc nhóm
        // đó không" phải đọc database nên nằm ở handler và trả Result.
        RuleFor(x => x.Scores)
            .Must(s => s.Select(i => i.EnrollmentId).Distinct().Count() == s.Count)
            .WithMessage("Mỗi thành viên chỉ được có một điểm riêng.");

        RuleForEach(x => x.Scores).ChildRules(i =>
        {
            i.RuleFor(x => x.Score).InclusiveBetween(0m, 10m)
                .When(x => x.Score is not null)
                .WithMessage("Điểm phải nằm trong khoảng 0 đến 10.");

            // Hạ điểm một thành viên so với cả nhóm là quyết định sẽ bị chất vấn. Không bắt
            // nhập lý do thì ba tháng sau chính giảng viên cũng không nhớ vì sao.
            i.RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000)
                .When(x => x.Score is not null)
                .WithMessage("Phải nhập lý do khi cho điểm riêng khác điểm chung.");
        });
    }
}

public sealed class SetMemberScoresHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<SetMemberScoresCommand, Result<List<MemberEffectiveScoreDto>>>
{
    public async Task<Result<List<MemberEffectiveScoreDto>>> Handle(
        SetMemberScoresCommand request, CancellationToken ct)
    {
        var bai = await db.Submissions
            .SingleOrDefaultAsync(s => s.Id == request.SubmissionId, ct);

        // Điều kiện sở hữu nằm trong WHERE, không nạp rồi mới kiểm. Không phải lớp mình ⇒ 404.
        if (bai is null || !await db.CourseOfferings.AnyAsync(o => o.Id == bai.OfferingId &&
                db.Subjects.Any(s => s.Id == o.SubjectId && s.InstructorId == request.InstructorId), ct))
            return Result<List<MemberEffectiveScoreDto>>.NotFound("Không tìm thấy bài nộp.");

        if (bai.GroupId is not { } nhomId)
            return Result<List<MemberEffectiveScoreDto>>.Invalid(
                "Chỉ bài nộp nhóm mới có điểm riêng từng thành viên.");

        var thanhVien = await db.GroupMembers.AsNoTracking()
            .Where(m => m.GroupId == nhomId)
            .Select(m => m.EnrollmentId)
            .ToListAsync(ct);

        var la = request.Scores.Select(s => s.EnrollmentId).Except(thanhVien).ToList();
        if (la.Count > 0)
            return Result<List<MemberEffectiveScoreDto>>.Invalid(
                "Có thành viên không thuộc nhóm của bài nộp này.");

        var hienCo = await db.SubmissionMemberScores
            .Where(m => m.SubmissionId == bai.Id)
            .ToListAsync(ct);

        var now = clock.GetUtcNow();

        foreach (var vao in request.Scores)
        {
            var cu = hienCo.SingleOrDefault(m => m.EnrollmentId == vao.EnrollmentId);

            // Score null ⇒ XÓA hàng, không đặt Score = null. Một trạng thái "nhận điểm chung"
            // chỉ được có MỘT cách biểu diễn; hai cách thì mọi truy vấn sau đó phải nhớ kiểm
            // cả hai, và chỗ nào quên là hiển thị sai điểm.
            if (vao.Score is not { } diem)
            {
                if (cu is not null)
                {
                    db.SubmissionMemberScores.Remove(cu);
                    GhiNhatKy(bai, vao.EnrollmentId, cu.Score, bai.Score, request.InstructorId, now,
                        "Bỏ điểm riêng, quay về điểm chung của nhóm.");
                }

                continue;
            }

            if (cu is null)
            {
                // Add tường minh qua DbSet: Entity.Id tự sinh UUIDv7 nên thêm vào
                // bai.MemberScores (parent đang tracked) sẽ làm EF phát UPDATE thay vì INSERT.
                db.SubmissionMemberScores.Add(new SubmissionMemberScore
                {
                    SubmissionId = bai.Id,
                    EnrollmentId = vao.EnrollmentId,
                    Score = Math.Round(diem, 2, MidpointRounding.AwayFromZero),
                    Reason = vao.Reason!.Trim(),
                    SetBy = request.InstructorId,
                    SetAt = now,
                });

                GhiNhatKy(bai, vao.EnrollmentId, bai.Score, diem, request.InstructorId, now, vao.Reason);
            }
            else
            {
                var truoc = cu.Score;

                cu.Score = Math.Round(diem, 2, MidpointRounding.AwayFromZero);
                cu.Reason = vao.Reason!.Trim();
                cu.SetBy = request.InstructorId;
                cu.SetAt = now;

                if (truoc != cu.Score)
                    GhiNhatKy(bai, vao.EnrollmentId, truoc, cu.Score, request.InstructorId, now, vao.Reason);
            }
        }

        await db.SaveChangesAsync(ct);

        return await DocLaiAsync(bai.Id, nhomId, ct);
    }

    private void GhiNhatKy(
        Submission bai, Guid enrollmentId, decimal? cu, decimal? moi,
        Guid boi, DateTimeOffset luc, string? lyDo) =>
        db.GradeAuditLogs.Add(new GradeAuditLog
        {
            OfferingId = bai.OfferingId,
            EnrollmentId = enrollmentId,
            TargetType = GradeTargetType.Submission,
            TargetId = bai.Id,
            OldValue = cu,
            NewValue = moi,
            ChangedBy = boi,
            ChangedAt = luc,
            Reason = lyDo,
        });

    private async Task<Result<List<MemberEffectiveScoreDto>>> DocLaiAsync(
        Guid submissionId, Guid groupId, CancellationToken ct)
    {
        var handler = new GetMemberScoresHandler(db);
        return await handler.DocAsync(submissionId, groupId, ct);
    }
}

// ── Đọc bảng điểm thành viên ────────────────────────────────────────────────────────────

/// <summary>
/// Chỉ giảng viên. Sinh viên KHÔNG được xem điểm riêng của bạn cùng nhóm: đó là dữ liệu cá
/// nhân của người khác, và một màn hình "nhóm mình ai bị hạ điểm" là cách nhanh nhất biến
/// một quyết định sư phạm thành mâu thuẫn trong nhóm.
/// </summary>
public sealed record GetMemberScoresQuery(Guid SubmissionId, Guid InstructorId)
    : IQuery<Result<List<MemberEffectiveScoreDto>>>;

public sealed class GetMemberScoresHandler(IAppDbContext db)
    : IQueryHandler<GetMemberScoresQuery, Result<List<MemberEffectiveScoreDto>>>
{
    public async Task<Result<List<MemberEffectiveScoreDto>>> Handle(
        GetMemberScoresQuery request, CancellationToken ct)
    {
        var bai = await db.Submissions.AsNoTracking()
            .Where(s => s.Id == request.SubmissionId
                     && db.CourseOfferings.Any(o => o.Id == s.OfferingId
                            && db.Subjects.Any(su => su.Id == o.SubjectId
                                                  && su.InstructorId == request.InstructorId)))
            .Select(s => new { s.Id, s.GroupId })
            .SingleOrDefaultAsync(ct);

        if (bai is null) return Result<List<MemberEffectiveScoreDto>>.NotFound("Không tìm thấy bài nộp.");

        if (bai.GroupId is not { } nhomId)
            return Result<List<MemberEffectiveScoreDto>>.Invalid(
                "Chỉ bài nộp nhóm mới có điểm riêng từng thành viên.");

        return await DocAsync(bai.Id, nhomId, ct);
    }

    internal async Task<Result<List<MemberEffectiveScoreDto>>> DocAsync(
        Guid submissionId, Guid groupId, CancellationToken ct)
    {
        var diemChung = await db.Submissions.AsNoTracking()
            .Where(s => s.Id == submissionId).Select(s => s.Score).SingleAsync(ct);

        var thanhVien = await (from m in db.GroupMembers.AsNoTracking()
                               join e in db.Enrollments.AsNoTracking() on m.EnrollmentId equals e.Id
                               where m.GroupId == groupId
                               orderby e.FullName
                               select new { e.Id, e.FullName })
                              .ToListAsync(ct);

        var rieng = await db.SubmissionMemberScores.AsNoTracking()
            .Where(m => m.SubmissionId == submissionId)
            .ToListAsync(ct);

        // Quy tắc "điểm riêng thắng thế" sống ở Domain và CHỈ ở đó. Chép lại phép ?? vào từng
        // handler là tạo nhiều bản của cùng một luật, và bản trong sổ điểm sẽ lệch với bản
        // trong file Excel sau lần sửa đầu tiên.
        var hieuLuc = MemberScoring.Resolve(diemChung, [.. thanhVien.Select(t => t.Id)], rieng);

        var ten = thanhVien.ToDictionary(t => t.Id, t => t.FullName);

        return Result<List<MemberEffectiveScoreDto>>.Ok(
        [
            .. hieuLuc.Select(h => new MemberEffectiveScoreDto(
                h.EnrollmentId, ten.GetValueOrDefault(h.EnrollmentId, "(không rõ)"),
                h.Score, h.IsOverridden, h.Reason))
        ]);
    }
}
