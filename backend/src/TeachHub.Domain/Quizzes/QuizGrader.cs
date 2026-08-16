using System.Globalization;
using System.Text;

namespace TeachHub.Domain.Quizzes;

/// <summary>
/// Đáp án của sinh viên cho một câu. <paramref name="SelectedOptionIds"/> dùng cho câu chọn,
/// <paramref name="Text"/> dùng cho câu điền khuyết.
///
/// Đáp án LUÔN gửi kèm id thật của câu và của phương án, không bao giờ dùng chỉ số vị trí:
/// xáo trộn chỉ xảy ra ở tầng hiển thị, nên chỉ số vị trí của client không có nghĩa gì ở
/// phía máy chủ và sẽ chấm nhầm ngay lần đầu bật xáo trộn.
/// </summary>
public sealed record QuizAnswer(Guid QuestionId, List<Guid> SelectedOptionIds, string? Text);

public sealed record QuestionResult(Guid QuestionId, bool IsCorrect, decimal Earned, decimal Max);

public sealed record QuizGradeResult(
    decimal RawScore, decimal MaxScore, List<QuestionResult> PerQuestion)
{
    /// <summary>
    /// Quy về thang 10 — ĐÚNG quy tắc Phase 4b đã chốt cho nguồn Assignment.
    /// Quiz 20 câu có thang tự nhiên là 20; đưa thẳng con số đó vào cột điểm rồi kẹp ở 10 thì
    /// mọi sinh viên đúng từ 10 câu trở lên đều thành 10 điểm.
    /// </summary>
    public decimal NormalizedScore =>
        MaxScore <= 0m ? 0m : Math.Round(RawScore / MaxScore * 10m, 2, MidpointRounding.AwayFromZero);

    public List<Guid> WrongQuestionIds => [.. PerQuestion.Where(p => !p.IsCorrect).Select(p => p.QuestionId)];
}

/// <summary>Hàm thuần, không I/O. Toàn bộ luật chấm trắc nghiệm nằm ở đây.</summary>
public static class QuizGrader
{
    public static QuizGradeResult Grade(
        IReadOnlyList<QuizQuestion> questions, IReadOnlyList<QuizAnswer> answers)
    {
        var ketQua = new List<QuestionResult>(questions.Count);

        foreach (var q in questions)
        {
            var tra = answers.FirstOrDefault(a => a.QuestionId == q.Id);
            var diem = ChamMotCau(q, tra);
            ketQua.Add(new QuestionResult(q.Id, diem >= q.Points && q.Points > 0m, diem, q.Points));
        }

        return new QuizGradeResult(
            Math.Round(ketQua.Sum(r => r.Earned), 2, MidpointRounding.AwayFromZero),
            questions.Sum(q => q.Points),
            ketQua);
    }

    private static decimal ChamMotCau(QuizQuestion q, QuizAnswer? tra) => q.Type switch
    {
        QuestionType.FillBlank => ChamDienKhuyet(q, tra?.Text),
        QuestionType.MultipleChoice => ChamNhieuDapAn(q, tra),
        _ => ChamMotDapAn(q, tra),                      // SingleChoice và TrueFalse cùng luật
    };

    private static decimal ChamMotDapAn(QuizQuestion q, QuizAnswer? tra)
    {
        // Chọn nhiều hơn một phương án cho câu chọn-một là câu trả lời không hợp lệ, không
        // phải "chọn đúng một trong số đó". Nhận bừa phương án đầu tiên là nới tay cho người
        // chọn hú họa.
        if (tra?.SelectedOptionIds is not { Count: 1 }) return 0m;

        var chon = tra.SelectedOptionIds[0];
        return q.Options.Any(o => o.Id == chon && o.IsCorrect) ? q.Points : 0m;
    }

    private static decimal ChamNhieuDapAn(QuizQuestion q, QuizAnswer? tra)
    {
        var dung = q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();
        var chon = (tra?.SelectedOptionIds ?? []).ToHashSet();

        // Bỏ phương án không thuộc câu này: id lạ đến từ client sửa payload, và nó không được
        // phép ảnh hưởng tới điểm theo bất kỳ chiều nào.
        chon.IntersectWith(q.Options.Select(o => o.Id));

        if (dung.Count == 0) return 0m;

        if (!q.AllowPartialCredit)
            return chon.SetEquals(dung) ? q.Points : 0m;

        // Chấm một phần: mỗi đáp án đúng cộng, mỗi đáp án sai TRỪ một phần tương đương —
        // không trừ thì chọn hết mọi phương án là ăn trọn điểm.
        var soDung = chon.Count(c => dung.Contains(c));
        var soSai = chon.Count - soDung;
        var tyLe = (soDung - soSai) / (decimal)dung.Count;

        return Math.Max(0m, Math.Round(q.Points * tyLe, 2, MidpointRounding.AwayFromZero));
    }

    private static decimal ChamDienKhuyet(QuizQuestion q, string? tra)
    {
        if (q.FillBlank is not { } cfg || tra is null) return 0m;

        var sv = ChuanHoa(tra, cfg);

        return cfg.AcceptedAnswers.Any(d => string.Equals(ChuanHoa(d, cfg), sv, StringComparison.Ordinal))
            ? q.Points
            : 0m;
    }

    /// <summary>
    /// Chuẩn hóa cả đáp án mẫu LẪN bài làm bằng cùng một hàm. Chuẩn hóa mỗi một bên là so sánh
    /// hai thứ không cùng dạng, và ca hỏng sẽ phụ thuộc vào việc giảng viên gõ đáp án bằng bộ
    /// gõ nào — thứ không ai nghĩ tới khi đi tìm nguyên nhân.
    /// </summary>
    private static string ChuanHoa(string s, FillBlankConfig cfg)
    {
        // NFC TRƯỚC: "ế" gõ Telex là U+0065 U+0302 U+0301 (e + mũ + sắc), gõ trên bàn phím
        // macOS là U+1EBF (một ký tự dựng sẵn). Nhìn giống hệt nhau, so sánh byte thì khác.
        if (cfg.NormalizeUnicode) s = s.Normalize(NormalizationForm.FormC);

        if (cfg.TrimWhitespace)
        {
            // Gộp mọi cụm khoảng trắng thành một dấu cách rồi cắt hai đầu: "Hà  Nội" và
            // "Hà Nội " phải chấm như nhau — gõ thừa dấu cách không phải là sai kiến thức.
            s = string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        if (!cfg.CaseSensitive) s = s.ToLower(CultureInfo.InvariantCulture);

        return s;
    }
}
