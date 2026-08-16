namespace TeachHub.Application.Features.Learning;

/// <summary>
/// Luật chấm câu hỏi nhúng của phần <c>checkpoint_quiz</c>. Hàm thuần, không I/O.
///
/// Tồn tại như một chỗ RIÊNG vì luật này được đọc từ hai đường: đường trả payload bài học
/// (<c>InlineQuestionDto.MultiSelect</c> — giao diện dựa vào nó để render radio hay checkbox)
/// và đường chấm ở <c>CheckInlineQuestionsCommand</c>. Hai bản sao chép sẽ lệch nhau đúng vào
/// ngày ai đó thêm đáp án đúng thứ hai cho một câu, và triệu chứng là cả lớp trả lời câu đó
/// không bao giờ đúng — không lỗi, không log, không ai biết.
///
/// Nằm ở Application chứ không phải Domain: đây không phải luật tính điểm (câu hỏi nhúng là
/// TỰ KIỂM TRA, không vào sổ điểm), và nó chỉ đọc <see cref="Domain.Content.InlineQuestion"/>
/// chứ không thêm hành vi nào cho entity. Ngày nào câu hỏi nhúng có điểm thì chuyển xuống Domain.
/// </summary>
public static class InlineQuizGrader
{
    /// <summary>
    /// Câu nhiều đáp án suy ra từ CHÍNH dữ liệu, không phải từ một cờ soạn tay: cờ soạn tay
    /// lệch được với danh sách đáp án, còn số đáp án đúng thì không.
    /// </summary>
    public static bool IsMultiSelect(IReadOnlyCollection<int> correctIndexes) => correctIndexes.Count > 1;

    /// <summary>
    /// So sánh TẬP HỢP, không so sánh danh sách: thứ tự bấm của sinh viên không mang nghĩa gì,
    /// và bấm hai lần cùng một ô vẫn là "chọn ô đó".
    ///
    /// Chọn thừa một phương án là SAI, kể cả khi đã chọn đủ đáp án đúng — cùng luật với
    /// <c>QuizGrader</c> của Phase 5. Nới ra là cho điểm người chọn tất cả các ô.
    /// </summary>
    public static bool IsCorrect(IEnumerable<int> correctIndexes, IEnumerable<int>? selectedIndexes)
    {
        var dung = correctIndexes.ToHashSet();

        // Câu soạn thiếu đáp án đúng thì không bài làm nào đúng được. Trả true ở đây là để một
        // câu hỏi vô nghĩa mở khoá bài kế tiếp mà không ai nhận ra.
        if (dung.Count == 0) return false;

        return dung.SetEquals(selectedIndexes ?? []);
    }
}
