using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Learning.Commands.CompleteComponent;
using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Learning.Commands.CheckInlineQuestions;

/// <param name="Index">
/// Số thứ tự do MÁY CHỦ đánh cho câu hỏi, lấy nguyên từ <c>inline_questions[].index</c> của
/// payload bài học — KHÔNG phải vị trí trong mảng mà giao diện đang hiển thị. Giao diện có thể
/// lọc, gộp hay đảo thứ tự bất cứ lúc nào; vị trí phía client không có nghĩa gì ở phía máy chủ.
/// </param>
public sealed record InlineAnswerDto(int Index, List<int> SelectedIndexes);

/// <param name="CorrectIndexes">
/// Đáp án đúng. Chỉ xuất hiện Ở ĐÂY — trong phản hồi SAU khi sinh viên đã trả lời. Payload lúc
/// tải bài cố ý không mang nó (xem <c>InlineQuestionDto</c>).
/// </param>
/// <param name="Explanation">
/// Lời giải. Domain đã có sẵn trường này từ Phase 1 nhưng chưa từng có đường ra tới người học;
/// đây là chỗ của nó.
/// </param>
public sealed record InlineQuestionResultDto(
    int Index, bool MultiSelect, bool IsCorrect, List<int> CorrectIndexes, string? Explanation);

/// <param name="Completed">
/// Phần học đã được đánh dấu hoàn thành hay chưa. Trả về ngay trong phản hồi chấm để giao diện
/// không phải đoán: trả lời đúng hết mà thanh tiến độ không nhúc nhích là đúng loại ngõ cụt đã
/// gặp ở màn hình bài học.
/// </param>
public sealed record CheckInlineQuestionsResultDto(
    Guid ComponentId, int Total, int CorrectCount, bool AllCorrect, bool Completed,
    List<InlineQuestionResultDto> Results);

/// <summary>
/// Chấm câu hỏi nhúng Ở MÁY CHỦ. Là <c>ICommand</c> chứ không phải <c>IQuery</c> vì trả lời
/// đúng hết sẽ ghi <c>ComponentProgress</c> — khai nhầm thành query là mất tính nguyên tử giữa
/// việc chấm và việc đánh dấu hoàn thành.
///
/// KHÔNG vào sổ điểm và KHÔNG lưu bài làm: đây là câu hỏi tự kiểm tra. Không có bảng mới nào.
/// </summary>
public sealed record CheckInlineQuestionsCommand(List<InlineAnswerDto> Answers)
    : ICommand<Result<CheckInlineQuestionsResultDto>>
{
    public Guid ComponentId { get; init; }
    public Guid UserId { get; init; }
}

public sealed class CheckInlineQuestionsCommandValidator : AbstractValidator<CheckInlineQuestionsCommand>
{
    public CheckInlineQuestionsCommandValidator()
    {
        // Chỉ luật THUẦN HÌNH DẠNG ở đây. "Index có trỏ tới câu hỏi có thật không" phụ thuộc dữ
        // liệu đang lưu nên thuộc handler — kiểm ở đây là mở một round-trip database TRƯỚC cả
        // TransactionBehavior, cho mỗi lần người dùng gõ sai.
        RuleFor(x => x.Answers).NotEmpty().WithMessage("Chưa chọn đáp án nào.");

        RuleForEach(x => x.Answers).ChildRules(a =>
        {
            a.RuleFor(y => y.Index).GreaterThanOrEqualTo(0)
                .WithMessage("Số thứ tự câu hỏi không hợp lệ.");
            a.RuleFor(y => y.SelectedIndexes).NotNull()
                .WithMessage("Danh sách lựa chọn không được để trống (gửi mảng rỗng nếu bỏ qua câu này).");
        });

        // Hai bản trả lời cho cùng một câu là payload tự mâu thuẫn; nhận bừa bản đầu hay bản
        // cuối đều là đoán ý người gửi.
        RuleFor(x => x.Answers)
            .Must(ds => ds.Select(a => a.Index).Distinct().Count() == ds.Count)
            .When(x => x.Answers is { Count: > 0 })
            .WithMessage("Mỗi câu hỏi chỉ được trả lời một lần trong cùng một request.");
    }
}

public sealed class CheckInlineQuestionsCommandHandler(IAppDbContext db, ISender sender)
    : ICommandHandler<CheckInlineQuestionsCommand, Result<CheckInlineQuestionsResultDto>>
{
    public async Task<Result<CheckInlineQuestionsResultDto>> Handle(
        CheckInlineQuestionsCommand request, CancellationToken ct)
    {
        // Không học lớp chứa bài này ⇒ 404, KHÔNG BAO GIỜ 403: 403 xác nhận id đó có thật, và
        // dò id tuần tự là đọc được cấu trúc nội dung của lớp người khác.
        var enrollmentId = await LearningAccess.EnrollmentForComponentAsync(
            db, request.ComponentId, request.UserId, ct);

        if (enrollmentId is null)
            return Result<CheckInlineQuestionsResultDto>.NotFound("Không tìm thấy phần học.");

        // AsNoTracking: chỉ đọc. Sửa một owned entity đang được theo dõi để phục vụ một lần đọc
        // là cách mất dữ liệu im lặng ở cuối request.
        var phan = await db.LessonComponents.AsNoTracking()
            .Where(c => c.Id == request.ComponentId)
            .Select(c => new { c.Type, c.Config.InlineQuestions })
            .SingleAsync(ct);

        if (phan.Type != LessonComponentType.CheckpointQuiz || phan.InlineQuestions.Count == 0)
            return Result<CheckInlineQuestionsResultDto>.Invalid(
                "Phần học này không có câu hỏi nhúng để chấm.");

        // Index ngoài khoảng là dữ liệu client tự bịa hoặc payload đã cũ so với nội dung hiện
        // tại. Bỏ qua lặng lẽ thì sinh viên trả lời một câu và không câu nào được chấm.
        var lac = request.Answers
            .Where(a => a.Index >= phan.InlineQuestions.Count)
            .Select(a => a.Index)
            .ToList();

        if (lac.Count > 0)
            return Result<CheckInlineQuestionsResultDto>.Invalid(
                $"Phần học này chỉ có {phan.InlineQuestions.Count} câu hỏi, không có câu số {string.Join(", ", lac)}.");

        var ketQua = phan.InlineQuestions
            .Select((q, i) =>
            {
                // Câu không gửi đáp án tính là sai, không phải là bỏ qua: "đúng hết" phải có
                // nghĩa là đúng CẢ BÀI, nếu không thì gửi đúng một câu là hoàn thành phần học.
                var chon = request.Answers.FirstOrDefault(a => a.Index == i)?.SelectedIndexes;

                return new InlineQuestionResultDto(
                    i,
                    InlineQuizGrader.IsMultiSelect(q.CorrectIndexes),
                    InlineQuizGrader.IsCorrect(q.CorrectIndexes, chon),
                    [.. q.CorrectIndexes],
                    q.Explanation);
            })
            .ToList();

        var soDung = ketQua.Count(r => r.IsCorrect);
        var dungHet = soDung == ketQua.Count;

        // Đúng hết ⇒ đánh dấu phần học hoàn thành. Đi qua CompleteComponentCommand thay vì tự
        // ghi: handler đó đã nuốt đúng lỗi trùng khoá khi hai request chạy song song, và một
        // đường ghi thứ hai sẽ quên mất chi tiết đó. TransactionBehavior thấy đã có transaction
        // ngoài nên không mở transaction lồng.
        var daXong = dungHet;
        if (dungHet)
        {
            var danhDau = await sender.Send(
                new CompleteComponentCommand(request.ComponentId, request.UserId), ct);

            daXong = danhDau.IsSuccess;
        }

        return Result<CheckInlineQuestionsResultDto>.Ok(new CheckInlineQuestionsResultDto(
            request.ComponentId, ketQua.Count, soDung, dungHet, daXong, ketQua));
    }
}
