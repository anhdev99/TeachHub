using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Review;

namespace TeachHub.Application.Features.Review;

/// <summary>
/// "Ôn tập hôm nay" cho trang chủ sinh viên.
///
/// Đến hạn hay chưa được tính NGAY TRONG câu truy vấn này bằng <c>TimeProvider</c>, không phải
/// do job nền đánh dấu. Nếu job là nguồn chân lý thì một đêm Hangfire chết là cả hệ thống ôn
/// tập ngừng chạy mà không ai biết — hàng đợi rỗng trông y hệt "bạn đã ôn hết rồi".
/// </summary>
public sealed record GetTodayReviewQuery(int? Limit) : IQuery<Result<TodayReviewDto>>
{
    public Guid UserId { get; init; }
}

public sealed class GetTodayReviewHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetTodayReviewQuery, Result<TodayReviewDto>>
{
    /// <summary>
    /// Trang chủ chỉ hiện một nắm mục. Đổ 200 câu ra màn hình là cách chắc chắn nhất khiến
    /// sinh viên không ôn câu nào.
    /// </summary>
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    public async Task<Result<TodayReviewDto>> Handle(GetTodayReviewQuery request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var soLuong = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaxLimit);

        // Điều kiện đến hạn lặp đúng nguyên văn ReviewQueueItem.IsDue — sửa một chỗ phải sửa
        // cả hai. Không gọi thẳng IsDue được vì EF phải dịch được biểu thức xuống SQL.
        var denHan = await db.ReviewQueueItems.AsNoTracking()
            .Where(i => i.UserId == request.UserId
                     && i.Status == ReviewItemStatus.Pending
                     && i.NextReviewAt != null && i.NextReviewAt <= now)
            .OrderBy(i => i.NextReviewAt)          // quá hạn lâu nhất lên trước
            .Take(soLuong)
            .ToListAsync(ct);

        // Một truy vấn gộp cho cả ba con số thay vì ba lần COUNT: đây là widget trang chủ, mỗi
        // sinh viên mở một lần mỗi lần vào hệ thống.
        var thongKe = await db.ReviewQueueItems.AsNoTracking()
            .Where(i => i.UserId == request.UserId)
            .GroupBy(i => i.Status)
            .Select(g => new
            {
                Status = g.Key,
                Tong = g.Count(),
                DenHan = g.Count(x => x.NextReviewAt != null && x.NextReviewAt <= now),
            })
            .ToListAsync(ct);

        var choOn = thongKe.FirstOrDefault(t => t.Status == ReviewItemStatus.Pending);
        var daThuoc = thongKe.FirstOrDefault(t => t.Status == ReviewItemStatus.Mastered)?.Tong ?? 0;

        var ids = denHan.Select(i => i.QuestionId).ToList();

        var cau = ids.Count == 0
            ? []
            : await db.QuizQuestions.AsNoTracking()
                .Include(q => q.Options)
                .Where(q => ids.Contains(q.Id))
                .ToListAsync(ct);

        var muc = denHan
            .Select(i => (i, q: cau.FirstOrDefault(x => x.Id == i.QuestionId)))
            .Where(x => x.q is not null)           // câu đã bị gỡ khỏi ngân hàng thì bỏ qua
            .Select(x => new ReviewItemDto(
                x.i.Id, x.i.QuestionId, x.i.Stage, x.i.NextReviewAt,
                x.i.CorrectCount, x.i.WrongCount, ReviewQuestionMapper.ToTakeDto(x.q!)))
            .ToList();

        // Hàng đợi rỗng KHÔNG phải 404: "hôm nay không có gì để ôn" là một câu trả lời hợp lệ,
        // và trả 404 ở đây sẽ hiện ra như một lỗi trên trang chủ của sinh viên chăm nhất.
        return Result<TodayReviewDto>.Ok(new TodayReviewDto(
            choOn?.DenHan ?? 0, choOn?.Tong ?? 0, daThuoc, muc));
    }
}
