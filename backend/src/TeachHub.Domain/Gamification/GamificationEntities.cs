using TeachHub.Domain.Common;

namespace TeachHub.Domain.Gamification;

/// <summary>
/// MỘT dòng bảng quy tắc XP của MỘT lớp học phần. Con số nằm ở đây chứ không nằm trong mã:
/// hard-code "hoàn thành bài = 10 XP" nghĩa là mỗi lần giảng viên muốn đổi động lực của lớp
/// đều phải deploy lại, nên thực tế sẽ không ai đổi và cơ chế chết dần.
/// </summary>
public class XpRule : Entity, IAuditable
{
    public required Guid OfferingId { get; set; }
    public required XpEventType EventType { get; set; }
    public required int Points { get; set; }

    /// <summary>Ngưỡng "từ … trở lên" của giá trị đi kèm sự kiện. null = không xét.</summary>
    public decimal? MinValue { get; set; }

    /// <summary>Số lần tối đa được cộng trong MỘT ngày địa phương. null = không trần.</summary>
    public int? DailyCap { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public XpRuleSpec ToSpec() => new()
    {
        EventType = EventType, Points = Points, MinValue = MinValue,
        DailyCap = DailyCap, IsEnabled = IsEnabled,
    };
}

/// <summary>
/// Sổ cái XP: mỗi hàng là MỘT lần cộng đã xảy ra. Tổng XP của một người học luôn là tổng của
/// sổ cái này, không bao giờ là một con số ghi đè — có sổ cái thì trả lời được câu hỏi
/// "vì sao tôi có 240 XP", không có thì không.
///
/// Đây là dữ liệu học tập của sinh viên: KHÔNG BAO GIỜ xóa, kể cả xóa mềm.
/// </summary>
public class XpEvent : Entity
{
    /// <summary>
    /// Gap #2: trỏ tới enrollment chứ không tới user. Cùng một người học lại môn này kỳ sau là
    /// một enrollment khác — trỏ vào user thì XP hai kỳ nhập vào nhau và bảng xếp hạng sai.
    /// </summary>
    public required Guid EnrollmentId { get; set; }

    public required XpEventType Type { get; set; }
    public required int Points { get; set; }

    /// <summary>
    /// Khóa chống cộng trùng, ví dụ <c>"lesson:{lessonId}"</c>, <c>"quiz_attempt:{attemptId}"</c>,
    /// <c>"streak:7"</c>. Có unique index <c>(enrollment_id, dedupe_key)</c> ở DB canh giữ.
    ///
    /// Không có nó thì mỗi lần đồng bộ lại là một lần cộng thêm, và trò "đánh dấu xong rồi bỏ
    /// đánh dấu" thành máy in XP vô hạn — lỗ hổng số một của mọi hệ thống gamification.
    /// </summary>
    public required string DedupeKey { get; set; }

    /// <summary>
    /// Thời điểm HOẠT ĐỘNG thật sự xảy ra (nộp bài, hoàn thành bài học), KHÔNG phải thời điểm
    /// ghi nhận. Streak đọc chính cột này: lấy thời điểm ghi nhận thì một sinh viên học đều cả
    /// tuần rồi mở trang một lần sẽ chỉ có streak = 1.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; set; }

    /// <summary>Id nguồn (bài học, bài nộp, lượt quiz) — chỉ để truy vết, cố ý không khai FK.</summary>
    public Guid? SourceId { get; set; }
}

/// <summary>
/// Huy hiệu thuộc GIẢNG VIÊN chứ không thuộc một lớp — cùng khuôn với ngân hàng câu hỏi:
/// soạn một lần, dùng cho mọi lớp và mọi học kỳ.
/// </summary>
public class Badge : Entity, IAuditable
{
    public required Guid InstructorId { get; set; }

    /// <summary>Mã slug ổn định (<c>[a-z0-9_]+</c>) — giao diện chọn biểu tượng theo mã này.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>Một emoji làm biểu tượng. Ảnh thật là việc của Phase sau, emoji đủ dùng và rẻ.</summary>
    public string? Icon { get; set; }

    public BadgeLogic Logic { get; set; } = BadgeLogic.All;

    /// <summary>
    /// Điều kiện khai báo dạng DỮ LIỆU, lưu jsonb. Rỗng = chưa khai xong ⇒ không bao giờ được
    /// trao (<see cref="BadgeEvaluator.IsEarned"/> đóng theo hướng an toàn).
    /// </summary>
    public List<BadgeCondition> Conditions { get; set; } = [];

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public BadgeSpec ToSpec() => new(Id, Code, Logic, Conditions);
}

/// <summary>Một lần trao huy hiệu. Đã trao thì không thu lại — có unique (enrollment, badge).</summary>
public class EarnedBadge : Entity
{
    public required Guid BadgeId { get; set; }
    public required Guid EnrollmentId { get; set; }
    public required DateTimeOffset EarnedAt { get; set; }
}

/// <summary>
/// MẶC ĐỊNH LÀ ẨN DANH, và đó là một quyết định nghiệp vụ chứ không phải mặc định cho tiện:
/// bảng xếp hạng công khai tên thật tạo động lực cho nhóm dẫn đầu nhưng làm nản lòng nhóm
/// cuối — vốn là nhóm cần động lực nhất. Đổi giá trị mặc định ở đây là đổi chính sách.
/// </summary>
public enum LeaderboardVisibility { Anonymous, RealNames }

/// <summary>
/// Cấu hình động lực học tập của MỘT lớp. Tách thành entity riêng thay vì thêm cột vào
/// <c>CourseOffering</c>: lớp học phần là bảng nóng nhất hệ thống, mỗi cột thêm vào đó là một
/// cột mọi truy vấn khác phải kéo theo.
/// </summary>
public class OfferingGamificationSettings : Entity, IAuditable
{
    public required Guid OfferingId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public LeaderboardVisibility LeaderboardVisibility { get; set; } = LeaderboardVisibility.Anonymous;

    /// <summary>Số hạng hiển thị trên bảng xếp hạng. Không phải toàn lớp — top ngắn tạo động lực hơn.</summary>
    public int LeaderboardSize { get; set; } = 10;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Cấu hình mặc định của một lớp chưa từng được cấu hình — ẩn danh, đang bật.</summary>
    public static OfferingGamificationSettings DefaultFor(Guid offeringId) =>
        new() { OfferingId = offeringId };
}
