namespace TeachHub.Domain.Integrity;

/// <summary>Một dấu vân tay: giá trị băm của một k-gram và vị trí k-gram đó trong chuỗi token.</summary>
public readonly record struct Fingerprint(long Hash, int Position);

/// <summary>
/// Thuật toán Winnowing (Schleimer, Wilkerson, Aiken — 2003): chọn một tập con nhỏ nhưng
/// ĐẢM BẢO của các k-gram để đại diện cho cả tệp.
///
/// Vì sao không băm hết mọi k-gram: một bài 500 dòng ra khoảng 4000 token, tức 4000 k-gram.
/// Nhân với 40 sinh viên × 8 bài tập × nhiều học kỳ thì bảng <c>source_fingerprints</c> phình
/// tới hàng chục triệu dòng, và Gap #14 (index trên hash) cũng không cứu nổi truy vấn so sánh.
/// Winnowing giữ lại khoảng 2/(w+1) số k-gram mà vẫn bảo đảm: mọi đoạn giống nhau dài từ
/// (w + k − 1) token trở lên CHẮC CHẮN sinh ra ít nhất một dấu vân tay chung.
///
/// Hàm thuần, không I/O.
/// </summary>
public static class Winnowing
{
    /// <summary>Số token trong một k-gram. Tăng k làm giảm báo động giả, nhưng bỏ sót đoạn chép ngắn.</summary>
    public const int DefaultK = 5;

    /// <summary>Kích thước cửa sổ chọn cực tiểu.</summary>
    public const int DefaultWindow = 4;

    private const ulong FnvOffset = 0xCBF29CE484222325UL;
    private const ulong FnvPrime = 0x100000001B3UL;

    /// <summary>Cơ số của hàm băm đa thức. Số lẻ để phép nhân mod 2^64 không mất bit thấp.</summary>
    private const ulong CoSo = 1_000_003UL;

    /// <summary>
    /// FNV-1a 64-bit. TUYỆT ĐỐI KHÔNG dùng <c>string.GetHashCode()</c>: từ .NET Core 3.0 nó
    /// được ngẫu nhiên hóa theo từng tiến trình. Dùng nó thì dấu vân tay lưu hôm nay không
    /// khớp dấu vân tay tính lại sau lần khởi động sau — bảng đầy dữ liệu mà mọi phép so sánh
    /// đều ra 0%, không có exception nào, không có log nào để lần ra.
    /// </summary>
    public static long HashToken(string token)
    {
        var h = FnvOffset;

        foreach (var c in token)
        {
            h ^= c;
            h = unchecked(h * FnvPrime);
        }

        return unchecked((long)h);
    }

    /// <summary>Băm một k-gram trực tiếp. Dùng để đối chiếu với bản rolling trong test.</summary>
    public static long HashKGram(IReadOnlyList<string> kgram)
    {
        ulong h = 0;
        foreach (var t in kgram) h = unchecked(h * CoSo + (ulong)HashToken(t));
        return unchecked((long)h);
    }

    /// <summary>
    /// Băm mọi k-gram bằng rolling hash: O(n) thay vì O(n·k). Phép trừ và phép nhân đều chạy
    /// mod 2^64 nhờ <c>unchecked</c>, nên kết quả trùng khít với <see cref="HashKGram"/>.
    /// </summary>
    public static IReadOnlyList<long> KGramHashes(IReadOnlyList<string> tokens, int k = DefaultK)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);

        if (tokens.Count < k) return [];

        var v = new ulong[tokens.Count];
        for (var i = 0; i < tokens.Count; i++) v[i] = unchecked((ulong)HashToken(tokens[i]));

        ulong luyThua = 1;                                   // CoSo^(k-1)
        for (var i = 0; i < k - 1; i++) luyThua = unchecked(luyThua * CoSo);

        var kq = new long[tokens.Count - k + 1];

        ulong h = 0;
        for (var i = 0; i < k; i++) h = unchecked(h * CoSo + v[i]);
        kq[0] = unchecked((long)h);

        for (var i = 1; i < kq.Length; i++)
        {
            h = unchecked((h - v[i - 1] * luyThua) * CoSo + v[i + k - 1]);
            kq[i] = unchecked((long)h);
        }

        return kq;
    }

    /// <summary>
    /// Chọn dấu vân tay theo cửa sổ trượt: mỗi cửa sổ lấy giá trị băm nhỏ nhất; hòa thì lấy
    /// vị trí PHẢI NHẤT. Chọn phải-nhất là chi tiết bắt buộc của thuật toán gốc — nó khiến
    /// hai cửa sổ liền nhau thường chọn trúng cùng một k-gram, nhờ đó bỏ được bản trùng và
    /// giảm số dấu vân tay xuống. Đổi thành trái-nhất thì số dòng ghi xuống DB tăng vọt mà
    /// khả năng phát hiện không hơn.
    /// </summary>
    public static IReadOnlyList<Fingerprint> Compute(
        IReadOnlyList<string> tokens, int k = DefaultK, int w = DefaultWindow)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(w, 1);

        var bam = KGramHashes(tokens, k);
        if (bam.Count == 0) return [];

        // Tệp ngắn hơn một cửa sổ vẫn phải sinh dấu vân tay. Bỏ qua hẳn thì mọi hàm ngắn đều
        // vô hình với hệ thống, và chép một hàm ngắn là cách né dễ nhất còn lại.
        var cuaSo = Math.Min(w, bam.Count);

        var kq = new List<Fingerprint>();
        var daChon = -1;

        for (var i = 0; i + cuaSo <= bam.Count; i++)
        {
            var min = i;
            for (var j = i + 1; j < i + cuaSo; j++)
                if (bam[j] <= bam[min]) min = j;             // <= ⇒ phải nhất khi hòa

            if (min == daChon) continue;

            daChon = min;
            kq.Add(new Fingerprint(bam[min], min));
        }

        return kq;
    }
}
