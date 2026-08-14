namespace TeachHub.Domain.Identity;

public static class Roles
{
    public const string Instructor        = "Instructor";
    public const string TeachingAssistant = "TeachingAssistant";
    public const string Student           = "Student";

    public static readonly string[] All = [Instructor, TeachingAssistant, Student];
}
