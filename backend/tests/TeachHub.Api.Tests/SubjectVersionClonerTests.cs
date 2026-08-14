using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TeachHub.Application.Features.Subjects;
using TeachHub.Domain.Content;
using TeachHub.Domain.Identity;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests;

public class SubjectVersionClonerTests(TeachHubApiFactory factory) : IClassFixture<TeachHubApiFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Dựng sẵn: 1 version có 2 chương; chương 1 có 2 bài (bài 1 gắn assignment + quiz,
    /// bài 2 không gắn gì); chương 2 có 1 bài. Đúng hình dạng ví dụ trong SRS mục 5.1.
    /// </summary>
    private async Task<Guid> SeedFullVersionAsync()
    {
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gv = await db.Set<AppUser>().AsNoTracking().FirstAsync();

        var subject = new Subject
        {
            Code = $"CL{Guid.CreateVersion7().ToString("N")[^6..].ToUpperInvariant()}",
            Name = "Môn để clone", InstructorId = gv.Id,
        };
        var version = SubjectVersion.CreateDraft(subject.Id, "2026-2027", Now);
        version.Syllabus = new Syllabus
        {
            Credits = 3,
            Outcomes = [new LearningOutcome { Code = "CĐR1.1", Text = "Mục tiêu gốc" }],
        };

        var assignment = new Assignment { SubjectVersionId = version.Id, Title = "Bài tập 1" };
        var quiz = new Quiz { SubjectVersionId = version.Id, Title = "Trắc nghiệm 1" };

        var ch1 = new Chapter
        {
            SubjectVersionId = version.Id, Title = "Chương 1", OrderIndex = 0,
            FoundationTags = ["hooks"],
        };
        var bai1 = new Lesson
        {
            ChapterId = ch1.Id, Title = "Bài 1", OrderIndex = 0, ContentType = LessonContentType.Ppt,
            FileUrl = "content/goc.pptx",
        };
        bai1.AddComponent(LessonComponentType.Assignment, assignment.Id, new ComponentConfig());
        bai1.AddComponent(LessonComponentType.Quiz, quiz.Id, new ComponentConfig());
        bai1.AddComponent(LessonComponentType.Reflection, null,
            new ComponentConfig { ReflectionPrompt = "Em học được gì?" });

        // Bài 2 KHÔNG gắn component nào — hoàn toàn hợp lệ theo SRS mục 5.1.
        var bai2 = new Lesson
        {
            ChapterId = ch1.Id, Title = "Bài 2", OrderIndex = 1, ContentType = LessonContentType.Text,
        };

        var ch2 = new Chapter { SubjectVersionId = version.Id, Title = "Chương 2", OrderIndex = 1 };
        var bai3 = new Lesson
        {
            ChapterId = ch2.Id, Title = "Bài 3", OrderIndex = 0, ContentType = LessonContentType.Pdf,
        };

        db.Subjects.Add(subject);
        db.SubjectVersions.Add(version);
        db.Assignments.Add(assignment);
        db.Quizzes.Add(quiz);
        db.Chapters.AddRange(ch1, ch2);
        db.Lessons.AddRange(bai1, bai2, bai3);
        await db.SaveChangesAsync();

        return version.Id;
    }

    private async Task<Guid> CloneAsync(Guid nguon, string nhan)
    {
        using var scope = factory.CreateServiceScope();
        var cloner = scope.ServiceProvider.GetRequiredService<SubjectVersionCloner>();
        var moi = await cloner.CloneAsync(nguon, nhan, CancellationToken.None);
        return moi.Id;
    }

    private AppDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();

    [Fact]
    public async Task Clone_sao_chep_du_so_luong_chuong_bai_va_component()
    {
        var nguon = await SeedFullVersionAsync();
        var moi = await CloneAsync(nguon, "2027-2028");

        using var scope = factory.CreateServiceScope();
        var db = Db(scope);

        (await db.Chapters.CountAsync(c => c.SubjectVersionId == moi))
            .ShouldBe(await db.Chapters.CountAsync(c => c.SubjectVersionId == nguon));

        var baiNguon = await db.Lessons.CountAsync(l =>
            db.Chapters.Any(ch => ch.Id == l.ChapterId && ch.SubjectVersionId == nguon));
        var baiMoi = await db.Lessons.CountAsync(l =>
            db.Chapters.Any(ch => ch.Id == l.ChapterId && ch.SubjectVersionId == moi));
        baiMoi.ShouldBe(baiNguon);
        baiMoi.ShouldBe(3);
    }

    [Fact]
    public async Task Ban_clone_luon_o_trang_thai_draft_va_ghi_nho_nguon()
    {
        var nguon = await SeedFullVersionAsync();

        using (var scope = factory.CreateServiceScope())
        {
            var db = Db(scope);
            var v = await db.SubjectVersions.SingleAsync(x => x.Id == nguon);
            v.Publish(Now);
            await db.SaveChangesAsync();
        }

        var moi = await CloneAsync(nguon, "2027-2028");

        using var scope2 = factory.CreateServiceScope();
        var v2 = await Db(scope2).SubjectVersions.AsNoTracking().SingleAsync(x => x.Id == moi);

        v2.Status.ShouldBe(SubjectVersionStatus.Draft);
        v2.ClonedFromVersionId.ShouldBe(nguon);
    }

    [Fact]
    public async Task KHONG_co_ban_ghi_nao_cua_ban_clone_dung_chung_id_voi_ban_goc()
    {
        var nguon = await SeedFullVersionAsync();
        var moi = await CloneAsync(nguon, "2027-2028");

        using var scope = factory.CreateServiceScope();
        var db = Db(scope);

        var idNguon = await db.Chapters.Where(c => c.SubjectVersionId == nguon).Select(c => c.Id).ToHashSetAsync();
        var idMoi = await db.Chapters.Where(c => c.SubjectVersionId == moi).Select(c => c.Id).ToHashSetAsync();

        idNguon.Overlaps(idMoi).ShouldBeFalse();
        idNguon.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Ref_id_cua_component_tro_toi_BAN_SAO_chu_khong_phai_ban_goc()
    {
        // ĐÂY LÀ TEST QUAN TRỌNG NHẤT CỦA CẢ PHASE.
        // Quên remap ref_id không làm test đếm số lượng nào đỏ; nó chỉ lộ ra một năm sau,
        // khi sửa bài tập của version mới và phát hiện bài tập lớp năm ngoái đổi theo.
        var nguon = await SeedFullVersionAsync();
        var moi = await CloneAsync(nguon, "2027-2028");

        using var scope = factory.CreateServiceScope();
        var db = Db(scope);

        var refCuaBanGoc = await db.Assignments.Where(a => a.SubjectVersionId == nguon).Select(a => a.Id)
            .Concat(db.Quizzes.Where(q => q.SubjectVersionId == nguon).Select(q => q.Id))
            .ToHashSetAsync();

        var refTrongBanClone = await db.LessonComponents
            .Where(c => db.Lessons.Any(l => l.Id == c.LessonId &&
                        db.Chapters.Any(ch => ch.Id == l.ChapterId && ch.SubjectVersionId == moi)))
            .Where(c => c.RefId != null)
            .Select(c => c.RefId!.Value)
            .ToListAsync();

        refTrongBanClone.ShouldNotBeEmpty("bộ dữ liệu seed phải có ít nhất 1 component có ref_id");
        refTrongBanClone.Count.ShouldBe(2);
        refTrongBanClone.ShouldAllBe(id => !refCuaBanGoc.Contains(id));
    }

    [Fact]
    public async Task Sua_ban_clone_KHONG_lam_thay_doi_ban_goc()
    {
        var nguon = await SeedFullVersionAsync();
        var moi = await CloneAsync(nguon, "2027-2028");

        using (var scope = factory.CreateServiceScope())
        {
            var db = Db(scope);
            var chuongClone = await db.Chapters.FirstAsync(c => c.SubjectVersionId == moi);
            db.Chapters.Remove(chuongClone);
            await db.SaveChangesAsync();
        }

        using var scope2 = factory.CreateServiceScope();
        (await Db(scope2).Chapters.CountAsync(c => c.SubjectVersionId == nguon)).ShouldBe(2);
    }

    [Fact]
    public async Task Clone_giu_nguyen_thu_tu_va_noi_dung_config()
    {
        var nguon = await SeedFullVersionAsync();
        var moi = await CloneAsync(nguon, "2027-2028");

        using var scope = factory.CreateServiceScope();
        var db = Db(scope);

        async Task<string> KyAsync(Guid v) => string.Join("|", await db.Chapters
            .Where(c => c.SubjectVersionId == v).OrderBy(c => c.OrderIndex)
            .Select(c => c.Title).ToListAsync());

        (await KyAsync(moi)).ShouldBe(await KyAsync(nguon));

        // Config jsonb phải sang nguyên vẹn, không chỉ đúng số lượng.
        var reflection = await db.LessonComponents
            .Where(c => db.Lessons.Any(l => l.Id == c.LessonId &&
                        db.Chapters.Any(ch => ch.Id == l.ChapterId && ch.SubjectVersionId == moi)))
            .Where(c => c.Type == LessonComponentType.Reflection)
            .SingleAsync();

        reflection.Config.ReflectionPrompt.ShouldBe("Em học được gì?");

        // Syllabus cũng phải là BẢN SAO, không dùng chung danh sách.
        var vMoi = await db.SubjectVersions.AsNoTracking().SingleAsync(x => x.Id == moi);
        vMoi.Syllabus.Outcomes.Single().Text.ShouldBe("Mục tiêu gốc");
    }

    [Fact]
    public async Task Clone_dung_chung_FileUrl_voi_ban_goc_mot_cach_co_y()
    {
        // Nhân bản file PPT/PDF mỗi lần clone là lãng phí vô nghĩa: nội dung file không đổi.
        // Chỉ tách bản sao khi giảng viên thật sự thay file ở bản mới.
        var nguon = await SeedFullVersionAsync();
        var moi = await CloneAsync(nguon, "2027-2028");

        using var scope = factory.CreateServiceScope();
        var db = Db(scope);

        var fileMoi = await db.Lessons
            .Where(l => db.Chapters.Any(ch => ch.Id == l.ChapterId && ch.SubjectVersionId == moi))
            .Where(l => l.FileUrl != null)
            .Select(l => l.FileUrl).SingleAsync();

        fileMoi.ShouldBe("content/goc.pptx");
    }

    [Fact]
    public async Task Component_tro_ra_ngoai_version_nguon_lam_clone_dung_lai_thay_vi_am_tham_bo_qua()
    {
        // Gán null cho ref_id hỏng sẽ tạo ra bài học có "component bài tập" nhưng không có
        // bài tập nào — trang trắng với sinh viên, không lỗi nào với giảng viên.
        var nguon = await SeedFullVersionAsync();

        using (var scope = factory.CreateServiceScope())
        {
            var db = Db(scope);
            var cp = await db.LessonComponents
                .Where(c => c.Type == LessonComponentType.Assignment)
                .Where(c => db.Lessons.Any(l => l.Id == c.LessonId &&
                            db.Chapters.Any(ch => ch.Id == l.ChapterId && ch.SubjectVersionId == nguon)))
                .SingleAsync();
            cp.RefId = Guid.CreateVersion7();          // trỏ ra hư không
            await db.SaveChangesAsync();
        }

        var loi = await Should.ThrowAsync<InvalidOperationException>(
            () => CloneAsync(nguon, "2027-2028-hong"));

        loi.Message.ShouldContain("không thuộc");
    }

    [Fact]
    public async Task Clone_mon_hoc_co_quy_mo_that_hoan_thanh_duoi_3_giay()
    {
        var nguon = await SeedLargeVersionAsync(chapters: 15, lessonsPerChapter: 6, componentsPerLesson: 3);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await CloneAsync(nguon, "quy-mo-lon");
        sw.Stop();

        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
    }

    private async Task<Guid> SeedLargeVersionAsync(int chapters, int lessonsPerChapter, int componentsPerLesson)
    {
        using var scope = factory.CreateServiceScope();
        var db = Db(scope);
        var gv = await db.Set<AppUser>().AsNoTracking().FirstAsync();

        var subject = new Subject
        {
            Code = $"BIG{Guid.CreateVersion7().ToString("N")[^5..].ToUpperInvariant()}",
            Name = "Môn quy mô lớn", InstructorId = gv.Id,
        };
        var version = SubjectVersion.CreateDraft(subject.Id, "goc-lon", Now);
        db.Subjects.Add(subject);
        db.SubjectVersions.Add(version);

        for (var c = 0; c < chapters; c++)
        {
            var ch = new Chapter { SubjectVersionId = version.Id, Title = $"Chương {c}", OrderIndex = c };
            db.Chapters.Add(ch);

            for (var l = 0; l < lessonsPerChapter; l++)
            {
                var bai = new Lesson
                {
                    ChapterId = ch.Id, Title = $"Bài {c}.{l}", OrderIndex = l,
                    ContentType = LessonContentType.Text,
                };
                for (var k = 0; k < componentsPerLesson; k++)
                    bai.AddComponent(LessonComponentType.CustomNote, null,
                        new ComponentConfig { Html = $"<p>Nội dung {c}.{l}.{k}</p>" });

                db.Lessons.Add(bai);
            }
        }

        await db.SaveChangesAsync();
        return version.Id;
    }
}
