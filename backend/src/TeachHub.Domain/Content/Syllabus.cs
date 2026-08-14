namespace TeachHub.Domain.Content;

/// <summary>Snapshot đề cương theo từng version (SRS: syllabus_json).</summary>
public class Syllabus
{
    public int? Credits { get; set; }
    public int? TheoryPeriods { get; set; }
    public int? PracticePeriods { get; set; }
    public List<LearningOutcome> Outcomes { get; set; } = [];
    public List<string> References { get; set; } = [];
    public string? Description { get; set; }

    /// <summary>
    /// Sao chép SÂU, không chia sẻ một danh sách nào với bản gốc. Gán tham chiếu ở đây là
    /// sửa đề cương bản 2027-2028 sẽ đổi luôn đề cương lớp 2026-2027 đã dạy xong.
    /// </summary>
    public Syllabus DeepCopy() => new()
    {
        Credits = Credits, TheoryPeriods = TheoryPeriods, PracticePeriods = PracticePeriods,
        Outcomes = [.. Outcomes.Select(o => new LearningOutcome { Code = o.Code, Text = o.Text })],
        References = [.. References],
        Description = Description,
    };
}

public class LearningOutcome
{
    public required string Code { get; set; }   // VD: CĐR1.2
    public required string Text { get; set; }
}
