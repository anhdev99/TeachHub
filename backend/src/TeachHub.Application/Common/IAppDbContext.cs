using Microsoft.EntityFrameworkCore;
using TeachHub.Domain.Content;
using TeachHub.Domain.Identity;

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

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
