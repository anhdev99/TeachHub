using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Learning;
using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Review;

/// <summary>
/// Component <c>recap</c>: 3–5 câu ôn đầu giờ, chọn theo <c>foundation_tags</c> của chương TRƯỚC.
/// </summary>
public sealed record GetRecapQuestionsQuery(Guid ComponentId, Guid UserId) : IQuery<Result<RecapDto>>;

public sealed class GetRecapQuestionsHandler(IAppDbContext db)
    : IQueryHandler<GetRecapQuestionsQuery, Result<RecapDto>>
{
    /// <summary>Plan chốt 3–5 câu. Ít hơn thì không ôn được gì, nhiều hơn thì thành một bài quiz nữa.</summary>
    public const int MinQuestions = 3;
    public const int MaxQuestions = 5;

    public async Task<Result<RecapDto>> Handle(GetRecapQuestionsQuery request, CancellationToken ct)
    {
        // Sinh viên chỉ chạm được nội dung của phiên bản mà lớp mình học đang ghim.
        // Không có quyền ⇒ 404, không bao giờ 403.
        var ghiDanh = await LearningAccess.EnrollmentForComponentAsync(
            db, request.ComponentId, request.UserId, ct);

        if (ghiDanh is null) return Result<RecapDto>.NotFound("Không tìm thấy phần ôn tập đầu giờ.");

        // Nạp cả entity thay vì chiếu từng trường trong Config: Config là cột jsonb, và một
        // hàng đọc về là rẻ hơn hẳn so với việc phụ thuộc vào khả năng dịch biểu thức xuống jsonb.
        var thanhPhan = await db.LessonComponents.AsNoTracking()
            .SingleOrDefaultAsync(cp => cp.Id == request.ComponentId, ct);

        if (thanhPhan is null) return Result<RecapDto>.NotFound("Không tìm thấy phần ôn tập đầu giờ.");

        // Người gọi đã có quyền xem phần này, nên nói thẳng nó sai loại là hợp lý và không lộ gì.
        if (thanhPhan.Type != LessonComponentType.Recap)
            return Result<RecapDto>.Invalid("Phần này không phải phần ôn tập đầu giờ.");

        var viTri = await (from l in db.Lessons.AsNoTracking()
                           where l.Id == thanhPhan.LessonId
                           join c in db.Chapters on l.ChapterId equals c.Id
                           select new { c.SubjectVersionId, c.OrderIndex })
                          .SingleOrDefaultAsync(ct);

        if (viTri is null) return Result<RecapDto>.NotFound("Không tìm thấy phần ôn tập đầu giờ.");

        // Chương liền trước theo OrderIndex. Query filter xóa mềm đã loại chương đã xóa, nên
        // "chương trước" là chương trước còn SỐNG chứ không phải chỗ trống nó để lại.
        var chuongTruoc = await db.Chapters.AsNoTracking()
            .Where(c => c.SubjectVersionId == viTri.SubjectVersionId && c.OrderIndex < viTri.OrderIndex)
            .OrderByDescending(c => c.OrderIndex)
            .FirstOrDefaultAsync(ct);

        // Giảng viên ghi đè nhãn trên chính component thì lấy nhãn đó; không thì lấy nhãn của
        // chương trước.
        var nhan = thanhPhan.Config.RecapFoundationTags.Count > 0
            ? thanhPhan.Config.RecapFoundationTags
            : chuongTruoc?.FoundationTags ?? [];

        // Chương đầu tiên không có gì phía trước để ôn. Đây là trạng thái BÌNH THƯỜNG, không
        // phải lỗi: trả danh sách rỗng để giao diện ẩn khối recap đi.
        if (nhan.Count == 0)
            return Result<RecapDto>.Ok(new RecapDto(thanhPhan.Id, chuongTruoc?.Title, [], []));

        // ADR-012: chỉ lấy câu trong ngân hàng của ĐÚNG giảng viên dạy môn này. Bỏ điều kiện
        // này là kéo ngân hàng câu hỏi của giảng viên khác vào bài học của lớp mình.
        var giangVien = await (from v in db.SubjectVersions.AsNoTracking()
                               where v.Id == viTri.SubjectVersionId
                               join s in db.Subjects on v.SubjectId equals s.Id
                               select s.InstructorId)
                              .SingleOrDefaultAsync(ct);

        var ungVien = await db.QuizQuestions.AsNoTracking()
            .Include(q => q.Options)
            .Where(q => q.InstructorId == giangVien)
            .Where(q => q.FoundationTags.Any(t => nhan.Contains(t)))
            .ToListAsync(ct);

        var ids = ungVien.Select(q => q.Id).ToList();

        // Ưu tiên đúng những câu sinh viên NÀY đã từng sai — đó là điểm gặp nhau giữa recap và
        // hàng đợi ôn tập, và cũng là khác biệt duy nhất giữa "ôn 3 câu" và "ôn 3 câu có ích".
        var daTungSai = ids.Count == 0
            ? []
            : await db.ReviewQueueItems.AsNoTracking()
                .Where(i => i.UserId == request.UserId && ids.Contains(i.QuestionId))
                .Select(i => i.QuestionId)
                .ToListAsync(ct);

        var soCau = Math.Clamp(
            thanhPhan.Config.RecapQuestionCount ?? MinQuestions, MinQuestions, MaxQuestions);

        var chon = ungVien
            .OrderByDescending(q => daTungSai.Contains(q.Id))
            .ThenBy(q => ThuTuOnDinh(q.Id, request.UserId))
            .Take(soCau)
            .Select(ReviewQuestionMapper.ToTakeDto)
            .ToList();

        return Result<RecapDto>.Ok(new RecapDto(thanhPhan.Id, chuongTruoc?.Title, [.. nhan], chon));
    }

    /// <summary>
    /// Khóa sắp xếp ổn định theo cặp (câu hỏi, người học): mỗi sinh viên nhận một bộ câu khác
    /// nhau, nhưng TẢI LẠI TRANG KHÔNG đổi bộ. Xáo lại mỗi lần mở là sinh viên đang trả lời dở
    /// thì thấy đề khác — đúng cái bẫy mà Phase 5 đã phải lưu <c>QuestionOrder</c> để tránh.
    /// Không dùng <c>GetHashCode</c> của chuỗi: nó được gieo ngẫu nhiên theo từng tiến trình,
    /// nên thứ tự sẽ đổi sau mỗi lần khởi động lại máy chủ.
    /// </summary>
    private static ulong ThuTuOnDinh(Guid questionId, Guid userId)
    {
        Span<byte> a = stackalloc byte[16];
        Span<byte> b = stackalloc byte[16];
        questionId.TryWriteBytes(a);
        userId.TryWriteBytes(b);

        ulong khoa = 0;
        for (var i = 0; i < 8; i++) khoa = (khoa << 8) | (byte)(a[i] ^ b[i]);
        return khoa;
    }
}
