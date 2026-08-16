namespace TeachHub.Domain.Integrity;

/// <param name="JaccardPercent">
/// |A ∩ B| / |A ∪ B|. Con số "hai bài giống nhau bao nhiêu" theo nghĩa cân xứng.
/// </param>
/// <param name="ContainmentPercent">
/// |A ∩ B| / min(|A|, |B|). Con số "bài nhỏ hơn nằm gọn trong bài kia bao nhiêu".
/// </param>
public sealed record SimilarityScore(
    int SharedHashes, int TotalA, int TotalB, decimal JaccardPercent, decimal ContainmentPercent);

/// <summary>
/// Chấm mức trùng lặp giữa hai tập dấu vân tay. Hàm thuần, không I/O.
///
/// Báo cáo CẢ HAI con số chứ không chọn một, vì chúng bắt hai hành vi khác nhau:
/// Jaccard loãng đi khi một bài dài hơn hẳn, nên "chép nguyên một hàm rồi tự viết thêm 300
/// dòng" sẽ trượt qua nếu chỉ nhìn Jaccard. Containment chia cho tập nhỏ hơn nên bắt được ca
/// đó, nhưng lại dễ báo động giả với tệp rất ngắn. Chỉ hiển thị một trong hai là hoặc bỏ sót,
/// hoặc gắn cờ oan — và cả hai kiểu sai đều làm giảng viên mất niềm tin vào danh sách cảnh báo.
///
/// Dùng TẬP (bỏ trùng lặp) chứ không dùng đa tập: một bài lặp lại đúng một đoạn mười lần
/// không được phép tự đẩy điểm trùng của chính mình lên.
/// </summary>
public static class SimilarityScorer
{
    public static SimilarityScore Compare(IEnumerable<long> a, IEnumerable<long> b)
    {
        var ta = new HashSet<long>(a);
        var tb = new HashSet<long>(b);

        // Tệp rỗng, tệp chỉ có chú thích, hoặc tệp ngắn hơn một k-gram đều cho tập rỗng.
        // Trả 0 ngay tại đây thay vì chia cho 0 ở dưới.
        if (ta.Count == 0 || tb.Count == 0)
            return new SimilarityScore(0, ta.Count, tb.Count, 0m, 0m);

        var nho = ta.Count <= tb.Count ? ta : tb;
        var lon = ReferenceEquals(nho, ta) ? tb : ta;

        var chung = nho.Count(h => lon.Contains(h));
        var hop = ta.Count + tb.Count - chung;

        return new SimilarityScore(
            chung, ta.Count, tb.Count,
            PhanTram(chung, hop),
            PhanTram(chung, nho.Count));
    }

    /// <summary>
    /// Vị trí các k-gram của <paramref name="a"/> cũng xuất hiện ở bài kia — đầu vào cho màn
    /// hình so sánh cạnh nhau. Trả về vị trí token chứ không trả về giá trị băm: giá trị băm
    /// không nói được đoạn nào trên màn hình phải tô sáng.
    /// </summary>
    public static IReadOnlyList<int> MatchedPositions(
        IReadOnlyList<Fingerprint> a, IEnumerable<long> hashesOfOther)
    {
        var kia = new HashSet<long>(hashesOfOther);
        return [.. a.Where(f => kia.Contains(f.Hash)).Select(f => f.Position)];
    }

    private static decimal PhanTram(int tu, int mau) =>
        mau <= 0 ? 0m : Math.Round(tu * 100m / mau, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Ngưỡng cảnh báo. KHÔNG hard-code trong handler: bài tập nhập môn dùng chung starter code
/// trùng 70–90% một cách hoàn toàn chính đáng, còn bài đồ án cuối kỳ thì 40% đã đáng xem lại.
/// Ngưỡng cứng chung cho mọi bài sẽ cho ra một danh sách cảnh báo dài tới mức giảng viên ngừng
/// đọc nó — mất tính năng theo cách khó nhận ra nhất.
///
/// Giá trị dưới đây chỉ là điểm khởi đầu. Plan yêu cầu hiệu chỉnh bằng dữ liệu thật: chạy trên
/// bài của học kỳ trước, xem phân bố, rồi mới chọn ngưỡng cho từng loại bài.
/// </summary>
public static class SimilarityThresholds
{
    public const decimal DefaultWarnPercent = 60m;

    /// <summary>
    /// Dưới ngần này dấu vân tay thì KHÔNG báo cáo. Một tệp mười dòng chỉ sinh vài dấu vân
    /// tay, và hai tệp mười dòng cùng in "Hello World" sẽ trùng 100% một cách vô nghĩa.
    /// </summary>
    public const int MinFingerprints = 8;

    public static bool ShouldWarn(decimal percent, decimal threshold) => percent >= threshold;
}
