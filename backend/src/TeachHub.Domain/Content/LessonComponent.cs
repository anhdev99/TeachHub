using TeachHub.Domain.Common;

namespace TeachHub.Domain.Content;

public enum LessonComponentType
{
    Recap, Hook, CheckpointQuiz, CodeAlong, Assignment, Quiz, Reflection, CustomNote
}

public class LessonComponent : Entity
{
    public Guid LessonId { get; set; }
    public LessonComponentType Type { get; set; }
    public int OrderIndex { get; set; }

    /// <summary>
    /// Trỏ tới Assignment.Id hoặc Quiz.Id. Null với các loại mang nội dung nhúng.
    /// CHÚ Ý: khi clone SubjectVersion, giá trị này BẮT BUỘC phải được ánh xạ lại
    /// sang id của bản sao — xem SubjectVersionCloner.
    /// </summary>
    public Guid? RefId { get; set; }

    public ComponentConfig Config { get; set; } = new();
    public bool IsRequired { get; set; }

    /// <summary>Các loại trỏ tới bản ghi khác; phần còn lại nhúng nội dung trong Config.</summary>
    public static bool RequiresRef(LessonComponentType t) =>
        t is LessonComponentType.Assignment or LessonComponentType.Quiz;
}
