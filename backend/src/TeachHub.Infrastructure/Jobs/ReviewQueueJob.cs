using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeachHub.Application.Features.Review;
using TeachHub.Domain.Quizzes;
using TeachHub.Domain.Review;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Infrastructure.Jobs;

/// <summary>
/// Job định kỳ hằng ngày của hàng đợi ôn tập.
///
/// ⚠️ Job này KHÔNG phải nguồn chân lý về việc một mục đã đến hạn hay chưa. Truy vấn
/// "Ôn tập hôm nay" (<see cref="GetTodayReviewHandler"/>) tự so <c>next_review_at</c> với đồng
/// hồ ngay lúc sinh viên mở trang. Nếu để job đánh dấu "đến hạn" thì một đêm Hangfire chết là
/// cả hệ thống ôn tập ngừng chạy — mà triệu chứng lại là "hôm nay không có gì để ôn", tức là
/// trông y hệt trạng thái tốt. Việc thật của job chỉ có hai:
///
/// 1. NẠP hàng đợi từ các lượt quiz tính điểm đã nộp mà chưa được nạp.
/// 2. Đếm và ghi log số mục đến hạn, để còn biết cơ chế có đang chạy hay không.
/// </summary>
public sealed class ReviewQueueJob(
    AppDbContext db, ISender sender, TimeProvider clock, ILogger<ReviewQueueJob> log)
{
    public const string RecurringJobId = "review-queue-daily";

    /// <summary>
    /// Chỉ quét lượt nộp trong 30 ngày gần nhất. Quét toàn bộ lịch sử làm mỗi đêm là một lần
    /// đọc lớn dần theo từng học kỳ để tìm ra gần như không có gì.
    /// </summary>
    public static readonly TimeSpan ScanWindow = TimeSpan.FromDays(30);

    /// <summary>Trần mỗi lần chạy, để một đợt tồn đọng không giữ worker suốt đêm.</summary>
    public const int MaxAttemptsPerRun = 500;

    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var tuMoc = now - ScanWindow;

        var ungVien = await (from a in db.QuizAttempts.AsNoTracking()
                             where a.Status != QuizAttemptStatus.InProgress
                                && a.SubmittedAt != null
                                && a.SubmittedAt >= tuMoc
                                && !db.ReviewGenerationMarks.Any(m => m.QuizAttemptId == a.Id)
                             orderby a.SubmittedAt
                             select new { a.Id, a.QuizId })
                            .Take(MaxAttemptsPerRun)
                            .ToListAsync(ct);

        // Lọc "quiz có tính điểm" trong bộ nhớ chứ không đặt vào WHERE: IsGraded nằm trong cột
        // jsonb, và một điều kiện dịch xuống jsonb sai cách sẽ lặng lẽ trả về rỗng — job vẫn
        // báo thành công mỗi đêm trong khi không nạp mục nào. Số quiz phân biệt ở đây bị chặn
        // trên bởi MaxAttemptsPerRun nên đây là một lần đọc nhỏ.
        var quizIds = ungVien.Select(x => x.QuizId).Distinct().ToList();

        var coTinhDiem = quizIds.Count == 0
            ? []
            : await db.Quizzes.AsNoTracking()
                .Where(q => quizIds.Contains(q.Id))
                .ToListAsync(ct);

        var idTinhDiem = coTinhDiem.Where(q => q.Config.IsGraded).Select(q => q.Id).ToHashSet();

        var taoMoi = 0;
        var datLai = 0;
        var boQua = 0;

        foreach (var luot in ungVien.Where(x => idTinhDiem.Contains(x.QuizId)))
        {
            // Gửi qua MediatR để mỗi lượt được TransactionBehavior bọc riêng: một lượt hỏng
            // không kéo theo những lượt đã nạp xong trước đó. Vì thế job KHÔNG tự mở transaction.
            var kq = await sender.Send(new GenerateReviewItemsCommand(luot.Id), ct);

            if (!kq.IsSuccess)
            {
                // Lỗi MONG ĐỢI: lượt làm hoặc quiz vừa bị xóa trong lúc job đang chạy. Ghi nhận
                // rồi đi tiếp — ném lại ở đây khiến Hangfire retry cả lần chạy vì một hàng đã
                // biến mất, và nó sẽ biến mất y như thế ở lần retry.
                // Mọi exception khác cứ để ném ra: đó có thể là sự cố tạm thời của database,
                // và retry mới là việc đúng phải làm.
                log.LogWarning("Bỏ qua lượt quiz {Id} khi nạp hàng đợi ôn tập: {Ly do}",
                    luot.Id, kq.Error);
                boQua++;
                continue;
            }

            taoMoi += kq.Value.ItemsCreated;
            datLai += kq.Value.ItemsReset;
        }

        var denHan = await db.ReviewQueueItems.AsNoTracking()
            .CountAsync(i => i.Status == ReviewItemStatus.Pending
                          && i.NextReviewAt != null && i.NextReviewAt <= now, ct);

        log.LogInformation(
            "Hàng đợi ôn tập: quét {SoLuot} lượt, tạo {TaoMoi} mục mới, đặt lại {DatLai} mục, " +
            "bỏ qua {BoQua}; hiện có {DenHan} mục đến hạn.",
            ungVien.Count, taoMoi, datLai, boQua, denHan);

        // Chạm trần nghĩa là còn tồn đọng và lần chạy sau mới xử lý tiếp. Không cảnh báo thì
        // một đợt nhập điểm cuối kỳ có thể tồn đọng nhiều ngày mà không ai biết.
        if (ungVien.Count == MaxAttemptsPerRun)
            log.LogWarning(
                "Chạm trần {Tran} lượt mỗi lần chạy — vẫn còn lượt chưa nạp vào hàng đợi ôn tập.",
                MaxAttemptsPerRun);
    }
}
