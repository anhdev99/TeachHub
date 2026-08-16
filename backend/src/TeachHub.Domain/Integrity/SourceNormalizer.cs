namespace TeachHub.Domain.Integrity;

/// <summary>
/// Một token đã chuẩn hóa, kèm vị trí trong văn bản GỐC. Giữ vị trí gốc là bắt buộc cho màn
/// hình so sánh cạnh nhau: sau chuẩn hóa mọi tên biến đều là "V", nên nếu không nhớ token này
/// đến từ ký tự nào thì không tô sáng lại được đoạn trùng trên mã nguồn thật.
/// </summary>
public readonly record struct SourceToken(string Text, int Offset, int Length);

/// <summary>
/// Chuẩn hóa mã nguồn thành chuỗi token trước khi băm. ĐÂY LÀ TOÀN BỘ GIÁ TRỊ của cơ chế
/// phát hiện trùng lặp.
///
/// Không chuẩn hóa thì chỉ cần đổi tên biến, sửa chú thích và bấm auto-format là qua mặt được,
/// và ta có một tính năng tạo cảm giác an toàn giả — tệ hơn hẳn không có tính năng nào, vì
/// giảng viên sẽ tin vào con số nó đưa ra và ngừng đọc bài bằng mắt.
///
/// Bốn phép chuẩn hóa, theo đúng thứ tự quan trọng:
/// 1. Bỏ chú thích (một dòng, nhiều dòng, kiểu <c>#</c>).
/// 2. Bỏ khoảng trắng và xuống dòng.
/// 3. Quy mọi định danh về MỘT token chung <c>V</c>, nhưng GIỮ NGUYÊN từ khóa.
/// 4. Quy mọi chuỗi ký tự về <c>S</c> và mọi con số về <c>N</c>.
///
/// Vì sao phải giữ từ khóa: quy cả từ khóa về "V" thì <c>if (a) b(); else c();</c> và
/// <c>int x = y + z;</c> trông giống nhau tới mức mọi bài đều trùng nhau — danh sách cảnh báo
/// dài tới mức không ai đọc nữa, tức là mất luôn tính năng theo một đường khác.
///
/// Hàm thuần, không I/O, không phụ thuộc ngôn ngữ lập trình cụ thể. Bộ luật cố tình phủ chung
/// họ C (C/C++/C#/Java/JS/TS) và Python thay vì dựng một parser riêng cho từng ngôn ngữ:
/// tách token theo lối này đã đủ để nhận ra bài chép, còn parser riêng là chi phí bảo trì
/// nhân với số ngôn ngữ mà sinh viên nộp.
/// </summary>
public static class SourceNormalizer
{
    /// <summary>Mọi tên biến, tên hàm, tên lớp đều thành token này.</summary>
    public const string IdentifierToken = "V";

    /// <summary>Mọi hằng số — số nguyên, số thực, hex — đều thành token này.</summary>
    public const string NumberToken = "N";

    /// <summary>Mọi chuỗi ký tự đều thành token này: đổi lời chào trong printf không né được.</summary>
    public const string StringToken = "S";

    /// <summary>
    /// Từ khóa của họ C và Python gộp làm một. Nhận nhầm một định danh thành từ khóa (ví dụ
    /// biến tên <c>string</c>) chỉ làm token đó cụ thể hơn một chút, không phá kết quả — vì
    /// cả hai bài đều được chuẩn hóa bằng đúng bộ luật này.
    /// </summary>
    private static readonly HashSet<string> TuKhoa = new(StringComparer.Ordinal)
    {
        "if", "else", "elif", "for", "while", "do", "switch", "case", "default", "break",
        "continue", "return", "goto", "foreach", "in", "of", "is", "as", "with", "yield",
        "class", "struct", "interface", "enum", "record", "namespace", "package", "module",
        "import", "using", "from", "export", "extends", "implements", "abstract", "override",
        "virtual", "sealed", "partial", "final", "static", "const", "readonly", "volatile",
        "public", "private", "protected", "internal", "friend", "extern", "inline", "register",
        "new", "delete", "this", "self", "super", "base", "null", "none", "nil", "nullptr",
        "true", "false", "and", "or", "not", "try", "catch", "except", "finally", "throw",
        "throws", "raise", "assert", "pass", "global", "nonlocal", "lambda", "def", "function",
        "func", "fn", "var", "let", "async", "await", "void", "int", "long", "short", "byte",
        "float", "double", "char", "bool", "boolean", "string", "str", "unsigned", "signed",
        "template", "typename", "typedef", "operator", "sizeof", "typeof", "instanceof",
        "delegate", "event", "out", "ref", "params",
    };

    /// <summary>Chuỗi token đã chuẩn hóa — đây là đầu vào của <see cref="Winnowing"/>.</summary>
    public static IReadOnlyList<string> Normalize(string? source) =>
        [.. Tokenize(source).Select(t => t.Text)];

    public static IReadOnlyList<SourceToken> Tokenize(string? source)
    {
        var kq = new List<SourceToken>();
        if (string.IsNullOrEmpty(source)) return kq;

        var n = source.Length;
        var i = 0;

        while (i < n)
        {
            var c = source[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            // ── Chú thích ────────────────────────────────────────────────────────────────
            // '#' nuốt cả dòng nên chỉ thị tiền xử lý của C (#include, #define) cũng biến mất.
            // Cố ý: đó là phần khung giống hệt nhau ở mọi bài, giữ lại chỉ làm mọi bài trùng
            // nhau thêm mà không mang thông tin gì về việc ai chép của ai.
            if (c == '#' || (c == '/' && i + 1 < n && source[i + 1] == '/'))
            {
                while (i < n && source[i] != '\n') i++;
                continue;
            }

            if (c == '/' && i + 1 < n && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < n && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i = Math.Min(n, i + 2);          // chú thích không đóng ⇒ nuốt tới hết file
                continue;
            }

            // ── Chuỗi ký tự ──────────────────────────────────────────────────────────────
            if (c is '"' or '\'')
            {
                var dau = i;
                i = BoQuaChuoi(source, i);
                kq.Add(new SourceToken(StringToken, dau, i - dau));
                continue;
            }

            // ── Số ───────────────────────────────────────────────────────────────────────
            if (char.IsAsciiDigit(c))
            {
                var dau = i;
                i = BoQuaSo(source, i);
                kq.Add(new SourceToken(NumberToken, dau, i - dau));
                continue;
            }

            // ── Định danh / từ khóa ──────────────────────────────────────────────────────
            if (c == '_' || char.IsLetter(c))
            {
                var dau = i;
                while (i < n && (source[i] == '_' || char.IsLetterOrDigit(source[i]))) i++;

                var chu = source[dau..i].ToLowerInvariant();
                kq.Add(new SourceToken(
                    TuKhoa.Contains(chu) ? chu : IdentifierToken, dau, i - dau));
                continue;
            }

            // ── Dấu câu và toán tử ───────────────────────────────────────────────────────
            // MỖI KÝ TỰ một token, không gộp "==" hay "+=" thành một. Gộp hay không gộp đều
            // đúng miễn là hai bài dùng chung một luật; tách từng ký tự thì không có bảng
            // toán tử nào phải bảo trì theo từng ngôn ngữ mới.
            kq.Add(new SourceToken(source[i].ToString(), i, 1));
            i++;
        }

        return kq;
    }

    /// <summary>Trả về chỉ số ngay SAU chuỗi ký tự bắt đầu tại <paramref name="i"/>.</summary>
    private static int BoQuaChuoi(string s, int i)
    {
        var n = s.Length;
        var nhay = s[i];

        // Ba dấu nháy (docstring Python, raw string C#). Quét ngây thơ theo từng dấu nháy sẽ
        // hiểu """abc""" thành chuỗi rỗng + định danh abc + chuỗi rỗng, và MỌI token sau đó
        // trong file bị lệch — hai bài giống hệt nhau sẽ ra 0% trùng.
        if (i + 2 < n && s[i + 1] == nhay && s[i + 2] == nhay)
        {
            i += 3;
            while (i + 2 < n && !(s[i] == nhay && s[i + 1] == nhay && s[i + 2] == nhay)) i++;
            return i + 2 < n ? i + 3 : n;
        }

        i++;
        while (i < n)
        {
            if (s[i] == '\\') { i += 2; continue; }        // ký tự thoát: '\'' , "\""
            if (s[i] == nhay) return i + 1;

            // Chuỗi không đóng trước khi hết dòng: dừng tại đây thay vì nuốt hết phần còn lại
            // của file. Thường là dấu nháy lẻ trong chú thích tiếng Việt không dấu ("don't").
            if (s[i] == '\n') return i;

            i++;
        }

        return n;
    }

    /// <summary>Trả về chỉ số ngay SAU hằng số bắt đầu tại <paramref name="i"/>.</summary>
    private static int BoQuaSo(string s, int i)
    {
        var n = s.Length;

        while (i < n)
        {
            var c = s[i];

            // Dấu mũ có dấu: 3.14e-2, 1E+9. Không xử lý riêng thì "e" kết thúc con số và
            // "-2" tách thành toán tử trừ — chuỗi token lệch so với cùng con số viết kiểu khác.
            if ((c is 'e' or 'E') && i + 2 < n && s[i + 1] is '+' or '-' && char.IsAsciiDigit(s[i + 2]))
            {
                i += 2;
                continue;
            }

            // Chấp nhận chữ cái để nuốt trọn 0xFF, 100L, 1_000, 3.14f.
            if (char.IsLetterOrDigit(c) || c is '.' or '_') { i++; continue; }

            break;
        }

        return i;
    }
}

/// <summary>
/// Tệp nào đáng đem đi băm. Băm cả ảnh và PDF thì bảng fingerprint phình lên vì dữ liệu nhị
/// phân vô nghĩa, và hai bài nộp cùng một ảnh đề bài sẽ bị chấm trùng 100%.
/// </summary>
public static class SourceFileTypes
{
    private static readonly HashSet<string> Duoi = new(StringComparer.OrdinalIgnoreCase)
    {
        ".c", ".h", ".cpp", ".cc", ".cxx", ".hpp", ".hh", ".cs", ".java", ".kt", ".kts",
        ".py", ".js", ".jsx", ".mjs", ".cjs", ".ts", ".tsx", ".go", ".rs", ".php", ".rb",
        ".swift", ".scala", ".m", ".mm", ".sql", ".r", ".pas", ".dart", ".lua", ".pl", ".sh",
    };

    public static bool IsSourceCode(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && Path.GetExtension(fileName) is { Length: > 0 } duoi
        && Duoi.Contains(duoi);
}
