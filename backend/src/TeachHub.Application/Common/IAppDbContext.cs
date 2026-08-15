using Microsoft.EntityFrameworkCore;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Assessments;
using TeachHub.Domain.Content;
using TeachHub.Domain.Grading;
using TeachHub.Domain.Groups;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Learning;
using TeachHub.Domain.Submissions;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Common;

/// <summary>
/// Cổng truy cập dữ liệu. Handler viết LINQ trực tiếp trên các DbSet này (ADR-002:
/// không dùng Repository generic). Mỗi phase sau sẽ bổ sung thêm DbSet vào đây.
/// </summary>
public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<InstructorInvite> InstructorInvites { get; }

    // Phase 1 — nội dung dạy học
    DbSet<Subject> Subjects { get; }
    DbSet<SubjectVersion> SubjectVersions { get; }
    DbSet<Chapter> Chapters { get; }
    DbSet<Lesson> Lessons { get; }
    DbSet<LessonComponent> LessonComponents { get; }
    DbSet<Assignment> Assignments { get; }
    DbSet<Quiz> Quizzes { get; }

    // Phase 2 — lớp học phần và tiến độ học
    DbSet<CourseOffering> CourseOfferings { get; }
    DbSet<Enrollment> Enrollments { get; }
    DbSet<LessonProgress> LessonProgresses { get; }
    DbSet<ComponentProgress> ComponentProgresses { get; }

    // Phase 3 — bài tập và nộp bài
    DbSet<Rubric> Rubrics { get; }
    DbSet<RubricCriterion> RubricCriteria { get; }
    DbSet<AssignmentHint> AssignmentHints { get; }
    DbSet<SubmissionSlot> SubmissionSlots { get; }
    DbSet<Submission> Submissions { get; }
    DbSet<SubmissionFile> SubmissionFiles { get; }

    // Phase 4a — chấm điểm và làm việc nhóm
    DbSet<StudentGroup> StudentGroups { get; }
    DbSet<GroupMember> GroupMembers { get; }
    DbSet<GradeAuditLog> GradeAuditLogs { get; }
    DbSet<FinalAssessment> FinalAssessments { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
