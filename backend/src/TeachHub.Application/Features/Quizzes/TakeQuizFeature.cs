using MediatR;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Learning;
using TeachHub.Application.Features.Review;
using TeachHub.Domain.Content;
using TeachHub.Domain.Quizzes;

namespace TeachHub.Application.Features.Quizzes;

/// <summary>Quyền của sinh viên với một quiz, và enrollment tương ứng.</summary>
public static class QuizAccess
{
    public static Task<Guid?> EnrollmentForQuizAsync(
        IAppDbContext db, Guid quizId, Guid userId, CancellationToken ct) =>
        (from q in db.Quizzes.AsNoTracking()
         where q.Id == quizId
         join o in db.CourseOfferings
             on true equals true
         where (q.OfferingId != null && o.Id == q.OfferingId)
            || (q.SubjectVersionId != null && o.SubjectVersionId == q.SubjectVersionId)
         join e in db.Enrollments on o.Id equals e.OfferingId
         where e.UserId == userId
         select (Guid?)e.Id).FirstOrDefaultAsync(ct);
}

// ── Bắt đầu (hoặc nối lại) một lượt làm ─────────────────────────────────────────────────

public sealed record StartQuizAttemptCommand(Guid QuizId, Guid UserId) : ICommand<Result<TakeAttemptDto>>;

public sealed class StartQuizAttemptHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<StartQuizAttemptCommand, Result<TakeAttemptDto>>
{
    public async Task<Result<TakeAttemptDto>> Handle(
        StartQuizAttemptCommand request, CancellationToken ct)
    {
        var ghiDanh = await QuizAccess.EnrollmentForQuizAsync(db, request.QuizId, request.UserId, ct);
        if (ghiDanh is null) return Result<TakeAttemptDto>.NotFound("Không tìm thấy bài trắc nghiệm.");

        // Lớp đã kết thúc ⇒ không mở lượt mới VÀ không nối lại lượt dở dang. Cho nối lại thì một
        // lượt mở trước lúc đóng lớp vẫn nộp được sau đó, tức là cái khoá có một cửa sau.
        if (await OfferingLock.IsClosedForEnrollmentAsync(db, ghiDanh.Value, ct))
            return Result<TakeAttemptDto>.Conflict(OfferingLock.ClosedMessage("làm bài trắc nghiệm"));

        var quiz = await db.Quizzes.AsNoTracking().SingleAsync(q => q.Id == request.QuizId, ct);
        var cau = await SaveQuizHandler.QuestionsOfQuizAsync(db, quiz.Id, ct);

        if (cau.Count == 0)
            return Result<TakeAttemptDto>.Invalid("Bài trắc nghiệm này chưa có câu hỏi nào.");

        var now = clock.GetUtcNow();

        // Đang có lượt dở dang thì NỐI LẠI chứ không mở lượt mới. Đây chính là ca "tắt trình
        // duyệt giữa chừng rồi vào lại" — mở lượt mới ở đây là xóa sạch bài đang làm.
        var dangLam = await db.QuizAttempts
            .SingleOrDefaultAsync(a => a.QuizId == quiz.Id && a.EnrollmentId == ghiDanh
                                    && a.Status == QuizAttemptStatus.InProgress, ct);

        if (dangLam is not null)
        {
            // Vào lại sau khi đã quá giờ: chốt sổ bằng phần đã tự lưu thay vì cho làm tiếp.
            if (QuizTiming.DaHetGio(dangLam.StartedAt, quiz.Config.TimeLimitMinutes, now))
            {
                ChotSo(dangLam, cau, quiz, now, QuizAttemptStatus.AutoSubmitted);
                await db.SaveChangesAsync(ct);
            }

            return Result<TakeAttemptDto>.Ok(Dung(dangLam, quiz, cau, now));
        }

        var daLam = await db.QuizAttempts
            .CountAsync(a => a.QuizId == quiz.Id && a.EnrollmentId == ghiDanh, ct);

        if (quiz.Config.MaxAttempts is { } toiDa && daLam >= toiDa)
            return Result<TakeAttemptDto>.Forbidden(
                $"Bạn đã làm hết {toiDa} lượt cho phép của bài này.");

        var luot = new QuizAttempt
        {
            QuizId = quiz.Id,
            EnrollmentId = ghiDanh.Value,
            AttemptNo = daLam + 1,
            StartedAt = now,
            Status = QuizAttemptStatus.InProgress,

            // Xáo trộn CHỈ ở tầng hiển thị và LƯU LẠI cho riêng lượt này: xáo lại mỗi lần mở
            // là sinh viên tưởng mình bị đổi đề giữa chừng.
            QuestionOrder = quiz.Config.ShuffleQuestions
                ? [.. cau.OrderBy(_ => Guid.CreateVersion7()).Select(c => c.Id)]
                : [.. cau.Select(c => c.Id)],
        };

        db.QuizAttempts.Add(luot);
        await db.SaveChangesAsync(ct);

        return Result<TakeAttemptDto>.Ok(Dung(luot, quiz, cau, now));
    }

    internal static void ChotSo(
        QuizAttempt luot, List<QuizQuestion> cau, Quiz quiz,
        DateTimeOffset now, QuizAttemptStatus trangThai)
    {
        var kq = QuizGrader.Grade(cau, [.. luot.Answers.Select(a => a.ToAnswer())]);

        luot.RawScore = kq.RawScore;
        luot.MaxScore = kq.MaxScore;
        luot.NormalizedScore = kq.NormalizedScore;
        luot.WrongQuestionIds = kq.WrongQuestionIds;
        luot.SubmittedAt = now;
        luot.Status = trangThai;
    }

    internal static TakeAttemptDto Dung(
        QuizAttempt luot, Quiz quiz, List<QuizQuestion> cau, DateTimeOffset now)
    {
        var thuTu = luot.QuestionOrder.Count > 0
            ? luot.QuestionOrder
            : [.. cau.Select(c => c.Id)];

        var theoThuTu = thuTu
            .Select(id => cau.FirstOrDefault(c => c.Id == id))
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();

        var daNop = luot.Status != QuizAttemptStatus.InProgress;

        // Chỉ trả đáp án đúng SAU khi đã nộp, và chỉ khi quiz bật hiện giải thích. Trước đó
        // mọi trường liên quan tới đáp án đều không có mặt trong payload — không phải bị ẩn
        // trên giao diện, mà là không tồn tại trong JSON.
        var xemLai = daNop && quiz.Config.ShowExplanationAfterSubmit
            ? QuizGrader.Grade(theoThuTu, [.. luot.Answers.Select(a => a.ToAnswer())])
                .PerQuestion
                .Select(r =>
                {
                    var c = theoThuTu.First(x => x.Id == r.QuestionId);
                    return new QuestionReviewDto(
                        r.QuestionId, r.IsCorrect, r.Earned, r.Max, c.Explanation,
                        [.. c.Options.Where(o => o.IsCorrect).Select(o => o.Id)],
                        c.FillBlank?.AcceptedAnswers ?? []);
                })
                .ToList()
            : [];

        return new TakeAttemptDto(
            luot.Id, quiz.Id, quiz.Title, quiz.Description, luot.AttemptNo, luot.Status,
            luot.StartedAt,
            QuizTiming.HanNop(luot.StartedAt, quiz.Config.TimeLimitMinutes),
            QuizTiming.ConLai(luot.StartedAt, quiz.Config.TimeLimitMinutes, now) is { } con
                ? (int)con.TotalSeconds
                : null,
            quiz.Config.IsGraded,
            [.. theoThuTu.Select(c => new TakeQuestionDto(
                c.Id, c.Type, c.Text, c.Points,
                [.. (quiz.Config.ShuffleOptions
                        ? c.Options.OrderBy(_ => Guid.CreateVersion7())
                        : c.Options.OrderBy(o => o.OrderIndex))
                     .Select(o => new TakeOptionDto(o.Id, o.Text))]))],
            [.. luot.Answers.Select(a => new TakeAnswerDto(a.QuestionId, a.SelectedOptionIds, a.Text))],
            daNop ? luot.RawScore : null,
            daNop ? luot.MaxScore : null,
            daNop ? luot.NormalizedScore : null,
            xemLai);
    }
}

// ── Tự lưu đáp án ───────────────────────────────────────────────────────────────────────

public sealed record SaveQuizAnswersCommand(List<TakeAnswerDto> Answers) : ICommand<Result>
{
    public Guid AttemptId { get; init; }
    public Guid UserId { get; init; }
}

public sealed class SaveQuizAnswersHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<SaveQuizAnswersCommand, Result>
{
    public async Task<Result> Handle(SaveQuizAnswersCommand request, CancellationToken ct)
    {
        var luot = await db.QuizAttempts.SingleOrDefaultAsync(a => a.Id == request.AttemptId, ct);

        if (luot is null ||
            !await db.Enrollments.AnyAsync(e => e.Id == luot.EnrollmentId && e.UserId == request.UserId, ct))
            return Result.NotFound("Không tìm thấy lượt làm bài.");

        if (await OfferingLock.IsClosedForEnrollmentAsync(db, luot.EnrollmentId, ct))
            return Result.Conflict(OfferingLock.ClosedMessage("lưu bài làm"));

        if (luot.Status != QuizAttemptStatus.InProgress)
            return Result.Conflict("Lượt làm này đã nộp rồi.");

        var quiz = await db.Quizzes.AsNoTracking().SingleAsync(q => q.Id == luot.QuizId, ct);
        var now = clock.GetUtcNow();

        // Hết giờ thì KHÔNG nhận thêm đáp án nữa, nhưng cũng không vứt đi phần đã lưu:
        // chốt sổ bằng đúng những gì đã tự lưu trước đó.
        if (QuizTiming.DaHetGio(luot.StartedAt, quiz.Config.TimeLimitMinutes, now))
        {
            var cau = await SaveQuizHandler.QuestionsOfQuizAsync(db, quiz.Id, ct);
            StartQuizAttemptHandler.ChotSo(luot, cau, quiz, now, QuizAttemptStatus.AutoSubmitted);
            await db.SaveChangesAsync(ct);

            return Result.Conflict("Đã hết giờ làm bài. Phần đã lưu được chấm tự động.");
        }

        luot.Answers = [.. request.Answers.Select(a => new StoredAnswer
        {
            QuestionId = a.QuestionId,
            SelectedOptionIds = a.SelectedOptionIds,
            Text = a.Text,
        })];

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

// ── Nộp bài ─────────────────────────────────────────────────────────────────────────────

public sealed record SubmitQuizAttemptCommand(Guid AttemptId, Guid UserId)
    : ICommand<Result<TakeAttemptDto>>;

public sealed class SubmitQuizAttemptHandler(IAppDbContext db, TimeProvider clock, ISender sender)
    : ICommandHandler<SubmitQuizAttemptCommand, Result<TakeAttemptDto>>
{
    public async Task<Result<TakeAttemptDto>> Handle(
        SubmitQuizAttemptCommand request, CancellationToken ct)
    {
        var luot = await db.QuizAttempts.SingleOrDefaultAsync(a => a.Id == request.AttemptId, ct);

        if (luot is null ||
            !await db.Enrollments.AnyAsync(e => e.Id == luot.EnrollmentId && e.UserId == request.UserId, ct))
            return Result<TakeAttemptDto>.NotFound("Không tìm thấy lượt làm bài.");

        // Xem lại một lượt ĐÃ nộp có đường riêng: GET /api/quiz-attempts/{id}. Endpoint này là
        // đường GHI thuần nên khoá cả khi lượt đã nộp — không có ca đọc nào phải đi qua đây.
        if (await OfferingLock.IsClosedForEnrollmentAsync(db, luot.EnrollmentId, ct))
            return Result<TakeAttemptDto>.Conflict(OfferingLock.ClosedMessage("nộp bài trắc nghiệm"));

        var quiz = await db.Quizzes.AsNoTracking().SingleAsync(q => q.Id == luot.QuizId, ct);
        var cau = await SaveQuizHandler.QuestionsOfQuizAsync(db, quiz.Id, ct);
        var now = clock.GetUtcNow();

        if (luot.Status == QuizAttemptStatus.InProgress)
        {
            var quaGio = QuizTiming.DaHetGio(luot.StartedAt, quiz.Config.TimeLimitMinutes, now);

            StartQuizAttemptHandler.ChotSo(luot, cau, quiz, now,
                quaGio ? QuizAttemptStatus.AutoSubmitted : QuizAttemptStatus.Submitted);

            await db.SaveChangesAsync(ct);

            // Nạp câu sai vào hàng đợi ôn tập NGAY (Phase 8), không đợi job đêm. Job đêm vẫn
            // giữ làm lưới quét sót; lệnh này idempotent (có ReviewGenerationMark + unique
            // index) nên gọi hai lần vô hại. Chỉ dựa vào job đêm thì sinh viên nộp bài xong
            // phải chờ tới 24 giờ mới thấy mục ôn tập, và họ sẽ nghĩ tính năng bị hỏng.
            await sender.Send(new GenerateReviewItemsCommand(luot.Id), ct);
        }

        return Result<TakeAttemptDto>.Ok(StartQuizAttemptHandler.Dung(luot, quiz, cau, now));
    }
}

// ── Xem lại lượt làm ────────────────────────────────────────────────────────────────────

public sealed record GetQuizAttemptQuery(Guid AttemptId, Guid UserId) : IQuery<Result<TakeAttemptDto>>;

public sealed class GetQuizAttemptHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetQuizAttemptQuery, Result<TakeAttemptDto>>
{
    public async Task<Result<TakeAttemptDto>> Handle(GetQuizAttemptQuery request, CancellationToken ct)
    {
        var luot = await db.QuizAttempts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == request.AttemptId, ct);

        if (luot is null ||
            !await db.Enrollments.AnyAsync(e => e.Id == luot.EnrollmentId && e.UserId == request.UserId, ct))
            return Result<TakeAttemptDto>.NotFound("Không tìm thấy lượt làm bài.");

        var quiz = await db.Quizzes.AsNoTracking().SingleAsync(q => q.Id == luot.QuizId, ct);
        var cau = await SaveQuizHandler.QuestionsOfQuizAsync(db, quiz.Id, ct);

        return Result<TakeAttemptDto>.Ok(
            StartQuizAttemptHandler.Dung(luot, quiz, cau, clock.GetUtcNow()));
    }
}

// ── Danh sách quiz của sinh viên trong một lớp ──────────────────────────────────────────

public sealed record MyQuizDto(
    Guid Id, string Title, string? Description, bool IsGraded,
    int? TimeLimitMinutes, int? MaxAttempts, int QuestionCount,
    int AttemptsUsed, decimal? BestNormalizedScore, Guid? InProgressAttemptId);

public sealed record GetMyQuizzesQuery(Guid OfferingId, Guid UserId) : IQuery<Result<List<MyQuizDto>>>;

public sealed class GetMyQuizzesHandler(IAppDbContext db)
    : IQueryHandler<GetMyQuizzesQuery, Result<List<MyQuizDto>>>
{
    public async Task<Result<List<MyQuizDto>>> Handle(GetMyQuizzesQuery request, CancellationToken ct)
    {
        var lop = await (from e in db.Enrollments.AsNoTracking()
                         where e.OfferingId == request.OfferingId && e.UserId == request.UserId
                         join o in db.CourseOfferings on e.OfferingId equals o.Id
                         select new { EnrollmentId = e.Id, o.SubjectVersionId })
                        .SingleOrDefaultAsync(ct);

        if (lop is null) return Result<List<MyQuizDto>>.NotFound("Không tìm thấy lớp học phần.");

        // Quiz của lớp VÀ quiz của phiên bản môn mà lớp đang ghim.
        var ds = await db.Quizzes.AsNoTracking()
            .Where(q => q.OfferingId == request.OfferingId || q.SubjectVersionId == lop.SubjectVersionId)
            .OrderBy(q => q.Title)
            .ToListAsync(ct);

        var ids = ds.Select(q => q.Id).ToList();

        var luot = await db.QuizAttempts.AsNoTracking()
            .Where(a => ids.Contains(a.QuizId) && a.EnrollmentId == lop.EnrollmentId)
            .Select(a => new { a.Id, a.QuizId, a.Status, a.NormalizedScore })
            .ToListAsync(ct);

        var soCau = await db.QuizItems.AsNoTracking()
            .Where(i => ids.Contains(i.QuizId))
            .GroupBy(i => i.QuizId)
            .Select(g => new { QuizId = g.Key, So = g.Count() })
            .ToListAsync(ct);

        return Result<List<MyQuizDto>>.Ok([.. ds.Select(q =>
        {
            var cua = luot.Where(a => a.QuizId == q.Id).ToList();

            return new MyQuizDto(
                q.Id, q.Title, q.Description, q.Config.IsGraded,
                q.Config.TimeLimitMinutes, q.Config.MaxAttempts,
                soCau.FirstOrDefault(s => s.QuizId == q.Id)?.So ?? 0,
                cua.Count,
                cua.Where(a => a.NormalizedScore != null).Select(a => a.NormalizedScore).Max(),
                cua.FirstOrDefault(a => a.Status == QuizAttemptStatus.InProgress)?.Id);
        })]);
    }
}
