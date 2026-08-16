using TeachHub.Domain.Common;

namespace TeachHub.Domain.Submissions;

/// <summary>
/// Điểm riêng của MỘT thành viên trong bài nộp nhóm (SRS UC-10b).
///
/// Quy ước: CÓ hàng nghĩa là có điểm riêng. Không có hàng nghĩa là thành viên đó nhận điểm
/// chung của nhóm. Bỏ điểm riêng = xóa hàng, không phải đặt <c>Score = null</c>.
/// Cho phép <c>Score</c> null thì tồn tại hai cách biểu diễn cùng một trạng thái "nhận điểm
/// chung", và mọi truy vấn sau đó phải nhớ kiểm cả hai — chỗ nào quên là hiển thị sai điểm.
/// </summary>
public class SubmissionMemberScore : Entity
{
    public required Guid SubmissionId { get; set; }

    /// <summary>Trỏ tới enrollment, KHÔNG phải user (Gap #2).</summary>
    public required Guid EnrollmentId { get; set; }

    public decimal Score { get; set; }

    /// <summary>
    /// Lý do bắt buộc phải có. Hạ điểm một thành viên so với cả nhóm là quyết định sẽ bị chất
    /// vấn, và "vì sao em thấp hơn các bạn" không trả lời được sau ba tháng nếu không ghi lại.
    /// </summary>
    public required string Reason { get; set; }

    public Guid? SetBy { get; set; }
    public DateTimeOffset? SetAt { get; set; }
}

/// <param name="IsOverridden">
/// true nghĩa là điểm này đến từ điểm riêng, false nghĩa là kế thừa điểm chung của nhóm.
/// Giao diện phải phân biệt được: một con số 5.0 vì bị hạ riêng khác hẳn 5.0 vì cả nhóm được 5.0.
/// </param>
public sealed record MemberEffectiveScore(
    Guid EnrollmentId, decimal? Score, bool IsOverridden, string? Reason);

/// <summary>
/// Quy tắc "điểm riêng thắng thế điểm chung" (UC-10b). Hàm thuần, không I/O — sổ điểm, bảng
/// chấm và file Excel đều phải gọi đúng hàm này. Mỗi bản chép lại quy tắc ở một chỗ khác là
/// một chỗ sẽ lệch, và sinh viên sẽ thấy hai con số khác nhau cho cùng một bài.
/// </summary>
public static class MemberScoring
{
    public static IReadOnlyList<MemberEffectiveScore> Resolve(
        decimal? groupScore,
        IReadOnlyCollection<Guid> memberEnrollmentIds,
        IReadOnlyCollection<SubmissionMemberScore> overrides)
    {
        // Chỉ số hóa theo enrollment và duyệt theo DANH SÁCH THÀNH VIÊN, không duyệt theo
        // danh sách điểm riêng: điểm riêng của người đã bị chuyển sang nhóm khác vẫn còn nằm
        // lại trong bảng, và nhận bừa nó sẽ hiện một dòng của người không thuộc nhóm này.
        var map = overrides
            .GroupBy(o => o.EnrollmentId)
            .ToDictionary(g => g.Key, g => g.Last());

        return
        [
            .. memberEnrollmentIds.Select(id => map.TryGetValue(id, out var rieng)
                ? new MemberEffectiveScore(id, rieng.Score, true, rieng.Reason)
                : new MemberEffectiveScore(id, groupScore, false, null))
        ];
    }
}
