using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TeachHub.Domain.Content;
using TeachHub.Domain.Identity;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests;

[Collection(TeachHubCollection.Ten)]
public class ContentPersistenceTests(TeachHubApiFactory factory)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

    private async Task<(Guid subjectId, Guid versionId, Guid lessonId)> SeedAsync()
    {
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gv = await db.Set<AppUser>().AsNoTracking().FirstAsync();

        // Mã môn phải khác nhau giữa các test: chỉ số unique (instructor_id, code) là thật,
        // dùng lại "IN4203" là test thứ hai đỏ vì 23505 chứ không phải vì lỗi ánh xạ.
        var subject = new Subject
        {
            // Lấy phần ĐUÔI: UUIDv7 sắp theo thời gian nên các ký tự đầu gần như không đổi
            // giữa hai lời gọi liền nhau, dùng chúng là vẫn trùng mã.
            Code = $"IN{Guid.CreateVersion7().ToString("N")[^6..].ToUpperInvariant()}",
            Name = "Lập trình di động", InstructorId = gv.Id,
        };
        var version = SubjectVersion.CreateDraft(subject.Id, "2026-2027", Now);
        version.Syllabus = new Syllabus
        {
            Credits = 3,
            Outcomes = [new LearningOutcome { Code = "CĐR1.1", Text = "Dựng được ứng dụng" }],
            References = ["React Native docs"],
        };

        var chapter = new Chapter
        {
            SubjectVersionId = version.Id, Title = "React Native cơ bản", OrderIndex = 0,
            FoundationTags = ["hooks", "state"],
            UnlockRule = UnlockRule.AfterPreviousCompleted,
        };
        var lesson = new Lesson
        {
            ChapterId = chapter.Id, Title = "Hooks", OrderIndex = 0,
            ContentType = LessonContentType.Ppt,
        };
        lesson.AddComponent(LessonComponentType.Reflection, null,
            new ComponentConfig { ReflectionPrompt = "3 điều em nhớ nhất?" });

        db.Subjects.Add(subject);
        db.SubjectVersions.Add(version);
        db.Chapters.Add(chapter);
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();

        return (subject.Id, version.Id, lesson.Id);
    }

    [Fact]
    public async Task Luu_lesson_co_component_roi_doc_lai_giu_nguyen_config_jsonb()
    {
        var (_, _, lessonId) = await SeedAsync();

        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var doc = await db.Lessons.AsNoTracking()
            .Include(l => l.Components)
            .SingleAsync(l => l.Id == lessonId);

        doc.Components.Single().Config.ReflectionPrompt.ShouldBe("3 điều em nhớ nhất?");
    }

    [Fact]
    public async Task Config_duoc_luu_thanh_jsonb_that_chu_khong_phai_chuoi_text()
    {
        // Nếu lưu nhầm sang text, các truy vấn JSON sau này (thống kê reflection,
        // lọc theo foundation tag) sẽ không dùng được index và phải parse thủ công.
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var kieu = await db.Database
            .SqlQuery<string>($"SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name='lesson_components' AND column_name='config'")
            .SingleAsync();

        kieu.ShouldBe("jsonb");
    }

    [Fact]
    public async Task Syllabus_cung_la_jsonb_va_round_trip_nguyen_ven()
    {
        var (_, versionId, _) = await SeedAsync();

        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var kieu = await db.Database
            .SqlQuery<string>($"SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name='subject_versions' AND column_name='syllabus'")
            .SingleAsync();
        kieu.ShouldBe("jsonb");

        var v = await db.SubjectVersions.AsNoTracking().SingleAsync(x => x.Id == versionId);
        v.Syllabus.Credits.ShouldBe(3);
        v.Syllabus.Outcomes.Single().Code.ShouldBe("CĐR1.1");
        v.Syllabus.References.ShouldBe(["React Native docs"]);
    }

    [Fact]
    public async Task Enum_xuong_DB_la_CHUOI_chu_khong_phai_so()
    {
        // Đọc log DB thấy status = 3 thì không ai hiểu là gì, và mọi CHECK constraint
        // viết bằng chuỗi sẽ chết ngay lúc apply migration.
        var (_, versionId, _) = await SeedAsync();

        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ràng buộc vào ĐÚNG hàng vừa gieo. Dùng LIMIT 1 là phụ thuộc vào việc database chỉ có
        // dữ liệu của mình — đúng cho tới khi cả assembly dùng chung một container.
        var status = await db.Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM subject_versions WHERE id = {versionId}")
            .SingleAsync();
        status.ShouldBe("Draft");

        var unlock = await db.Database
            .SqlQuery<string>($"SELECT unlock_rule AS \"Value\" FROM chapters WHERE subject_version_id = {versionId}")
            .SingleAsync();
        unlock.ShouldBe("AfterPreviousCompleted");
    }

    [Fact]
    public async Task Foundation_tags_luu_thanh_mang_text_khong_phai_chuoi_noi()
    {
        await SeedAsync();

        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var kieu = await db.Database
            .SqlQuery<string>($"SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name='chapters' AND column_name='foundation_tags'")
            .FirstAsync();

        kieu.ShouldBe("ARRAY");
    }

    [Fact]
    public async Task Xoa_mem_chuong_thi_truy_van_thuong_khong_thay_no_nua()
    {
        var (_, versionId, _) = await SeedAsync();

        using (var scope = factory.CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ch = await db.Chapters.FirstAsync(c => c.SubjectVersionId == versionId);
            db.Chapters.Remove(ch);
            await db.SaveChangesAsync();
        }

        using var scope2 = factory.CreateServiceScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db2.Chapters.CountAsync(c => c.SubjectVersionId == versionId)).ShouldBe(0);

        // Nhưng hàng vẫn còn trong bảng — nội dung dạy học không bao giờ bị DELETE thật,
        // vì bài nộp của sinh viên trỏ vào nó.
        (await db2.Chapters.IgnoreQueryFilters().CountAsync(c => c.SubjectVersionId == versionId))
            .ShouldBe(1);
    }
}
