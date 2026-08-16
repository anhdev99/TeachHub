namespace TeachHub.Domain.Gamification;

/// <param name="Current">
/// Số ngày liên tiếp tính tới HÔM NAY (theo giờ của lớp). Còn "sống" nếu hoạt động gần nhất
/// là hôm nay hoặc hôm qua — xem chú thích ân hạn trong <see cref="StreakCalculator.Compute"/>.
/// </param>
/// <param name="Longest">
/// Kỷ lục dài nhất trong toàn bộ lịch sử. KHÔNG bị xóa khi chuỗi hiện tại đứt: huy hiệu đã
/// trao theo kỷ lục thì không được phép biến mất chỉ vì sinh viên nghỉ một ngày vì ốm.
/// </param>
public sealed record StreakOutcome(int Current, int Longest, DateOnly? LastActiveLocalDate)
{
    public static StreakOutcome None { get; } = new(0, 0, null);
}

/// <summary>
/// Toàn bộ định nghĩa "ngày" của cơ chế streak nằm ở đây, thuần túy, không I/O.
///
/// QUYẾT ĐỊNH CỐT LÕI: một "ngày" là một NGÀY LỊCH ĐỊA PHƯƠNG theo múi giờ của lớp
/// (<c>CourseOffering.TimeZoneId</c>, mặc định <c>Asia/Ho_Chi_Minh</c>), KHÔNG phải ngày UTC và
/// cũng không phải "24 giờ kể từ lần trước". Hệ quả cụ thể, đã khóa bằng test:
/// học lúc 23:50 rồi 00:10 hôm sau là HAI ngày (đúng như sinh viên tự đếm), trong khi tính theo
/// UTC chỉ ra MỘT vì cả hai mốc đều rơi vào 16:50Z và 17:10Z cùng ngày.
/// Chọn nhầm múi giờ không làm lệch một chút — nó xóa hẳn một ngày khỏi chuỗi, và sinh viên
/// phát hiện ngay lập tức.
/// </summary>
public static class StreakCalculator
{
    public const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";

    /// <summary>
    /// <c>TimeZoneId</c> là chuỗi tự do trong DB nên có thể sai (gõ nhầm, import, đổi tên vùng
    /// giữa các bản ICU). Ném ngoại lệ ở đây là để MỘT dòng dữ liệu hỏng làm sập trang tiến độ
    /// của cả lớp; lùi về giờ Việt Nam thì sai lệch nhiều nhất là vài giờ với một lớp ở múi khác.
    /// </summary>
    public static TimeZoneInfo ResolveZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Rơi xuống mặc định bên dưới.
            }
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Máy chủ không có CSDL múi giờ (InvariantGlobalization bật nhầm ở đâu đó).
            // UTC vẫn cho ra kết quả dùng được thay vì làm hỏng cả tính năng.
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>Ngày lịch tại địa phương của một mốc thời gian tuyệt đối.</summary>
    public static DateOnly LocalDateOf(DateTimeOffset moment, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, zone).DateTime);

    /// <summary>
    /// <paramref name="activityMoments"/> không cần sắp xếp sẵn: thứ tự do câu truy vấn quyết
    /// định, mà kết quả streak thì không được phép phụ thuộc vào nó.
    ///
    /// ÂN HẠN TỚI HẾT NGÀY: nếu hôm nay chưa có hoạt động nhưng hôm qua có, chuỗi vẫn được tính
    /// là còn sống. Không có ân hạn này thì mọi sinh viên mở trang lúc 7 giờ sáng đều thấy
    /// streak = 0, tưởng mình vừa mất chuỗi, và không ai còn tin con số đó nữa.
    /// </summary>
    public static StreakOutcome Compute(
        IEnumerable<DateTimeOffset> activityMoments, TimeZoneInfo zone, DateTimeOffset now)
    {
        // Gom về NGÀY ĐỊA PHƯƠNG trước khi so sánh bất cứ thứ gì. Nhiều hoạt động trong cùng
        // một ngày chỉ đáng một ngày streak — học chăm gấp đôi không mua thêm được ngày nào.
        var ngay = new HashSet<DateOnly>();
        foreach (var thoiDiem in activityMoments) ngay.Add(LocalDateOf(thoiDiem, zone));

        if (ngay.Count == 0) return StreakOutcome.None;

        var theoThuTu = ngay.Order().ToList();
        var homNay = LocalDateOf(now, zone);

        // ── Kỷ lục dài nhất: quét toàn bộ lịch sử ────────────────────────────────────────
        var daiNhat = 1;
        var chuoi = 1;

        for (var i = 1; i < theoThuTu.Count; i++)
        {
            chuoi = theoThuTu[i] == theoThuTu[i - 1].AddDays(1) ? chuoi + 1 : 1;
            if (chuoi > daiNhat) daiNhat = chuoi;
        }

        // ── Chuỗi hiện tại: đếm ngược từ mốc neo ─────────────────────────────────────────
        DateOnly? neo = ngay.Contains(homNay) ? homNay
                      : ngay.Contains(homNay.AddDays(-1)) ? homNay.AddDays(-1)
                      : null;

        var hienTai = 0;

        if (neo is { } moc)
        {
            var d = moc;
            while (ngay.Contains(d))
            {
                hienTai++;
                d = d.AddDays(-1);
            }
        }

        return new StreakOutcome(hienTai, daiNhat, theoThuTu[^1]);
    }
}
