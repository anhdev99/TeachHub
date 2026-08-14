---
name: teachhub-data
description: Dựng và sửa tầng dữ liệu TeachHub bằng EF Core 10 + Npgsql trên PostgreSQL 17 — entity, IEntityTypeConfiguration, cột jsonb qua ToJson(), enum lưu chuỗi, CHECK constraint, FK đa hình, soft delete, partial unique index, migration và integration test bằng Testcontainers. Dùng skill này khi thêm/sửa entity hay DbSet, khi viết hoặc đọc lại file cấu hình trong TeachHub.Infrastructure/Persistence, khi chạy `dotnet ef migrations add`, khi model EF không dựng được hoặc ứng dụng không khởi động vì owned type, khi INSERT bị CHECK constraint chặn, khi enum ra số trong cột jsonb, khi điểm hoặc thời gian sai kiểu, và cả khi "chạy lại", "sửa", "bổ sung", "cập nhật", "rà lại" phần schema hay migration đã có.
---

# Tầng dữ liệu TeachHub — EF Core 10 + PostgreSQL 17

Nguồn chân lý kỹ thuật: `docs/architecture/00-tech-stack-decisions.html` (ADR-002, ADR-003,
ADR-008, ADR-012; quy ước §5 "Kiểu dữ liệu"). Skill này là phần thi công của các quyết định đó.

Mọi luật dưới đây đều kèm **hậu quả nếu làm sai** — đó là lý do luật tồn tại, không phải hình thức.

---

## 0. Luật số một: migration do công cụ sinh, không bao giờ gõ tay

`CLAUDE.md` xếp đây là nguyên tắc số một của dự án và **migration EF Core không có ngoại lệ**.

Quy trình bắt buộc, đúng thứ tự:

```
1. Sửa entity + IEntityTypeConfiguration trước
2. dotnet ef migrations add <Tên> -p src/TeachHub.Infrastructure -s src/TeachHub.Api -o Persistence/Migrations
3. ĐỌC file .cs vừa sinh ra — tìm mọi lệnh DROP
4. dotnet ef database update -p src/TeachHub.Infrastructure -s src/TeachHub.Api
5. dotnet build → 0 Warning(s) 0 Error(s)
```

**Bước 3 không được bỏ.** Đổi tên property, đổi kiểu, gỡ một `OwnsOne` — EF có thể diễn giải
thành `DropColumn` + `AddColumn` thay vì `RenameColumn`. Migration chạy trót lọt, và điểm của
sinh viên trong cột đó biến mất không dấu vết. Thấy `DropColumn`/`DropTable` ngoài ý muốn thì
**xóa migration, sửa model, sinh lại** — không vá tay file migration.

Viết tay file migration → `ModelSnapshot` lệch model thật → migration **kế tiếp** sinh lệnh sai
trên database đã có điểm thật. Lỗi không lộ lúc review, chỉ lộ khi đã hỏng dữ liệu.

Định nghĩa "Xong" (roadmap, mục 4): migration phải apply thành công trên **DB trống lẫn DB đã có
dữ liệu**. Chạy đủ hai lần, không suy đoán.

Không chạy được `dotnet ef` (chưa cài tool, thiếu `ConnectionStrings:Default`, Docker chưa bật)
→ **BÁO LẠI**, không tự gõ file migration thay thế.

---

## 1. Quy ước đặt tên: `UseSnakeCaseNamingConvention()`

Đăng ký một lần trong `TeachHub.Infrastructure/DependencyInjection.cs`:

```csharp
services.AddDbContext<AppDbContext>(o => o
    .UseNpgsql(config.GetConnectionString("Default"),
        npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
    .UseSnakeCaseNamingConvention());
```

- C#: `PascalCase`, entity **số ít** (`SubjectVersion`). DB: `snake_case`, bảng **số nhiều**
  (`subject_versions`).
- **Không viết tay từng `HasColumnName`.** Viết tay một chỗ là mở đường cho chỗ thứ hai viết khác,
  và tên cột trong CHECK constraint (chuỗi SQL thô) sẽ trỏ vào cột không tồn tại.

Xác minh sau migration đầu tiên — đừng tin, hãy nhìn:

```bash
docker exec -it teachhub-db psql -U teachhub -d teachhub -c '\dt'
```

Kỳ vọng `asp_net_users`, `refresh_tokens`. Thấy `AspNetUsers` là `UseSnakeCaseNamingConvention()`
chưa được gọi — dừng lại sửa ngay, đừng sinh thêm migration nào nữa. Sửa sau khi đã có 5 migration
nghĩa là rename toàn bộ bảng trên DB có dữ liệu.

> **Snake_case áp cho tên bảng và tên cột. Nó KHÔNG đụng tới *giá trị* bên trong ô.**
> Nhớ câu này — mục 4 sống chết vì nó.

---

## 2. Kiểu dữ liệu — không có "gần đúng"

| Thứ | Kiểu bắt buộc | Sai thì sao |
|---|---|---|
| Khóa chính | `Guid` sinh bằng `Guid.CreateVersion7()` **ở tầng ứng dụng** | Guid ngẫu nhiên (`NewGuid`) phân mảnh B-tree index; bảng `submissions`/`submission_files` phình qua từng học kỳ và insert chậm dần. Để DB sinh thì handler không biết id trước khi `SaveChanges`, không dựng được đồ thị object trong một transaction. |
| Điểm, trọng số | `decimal` (`decimal(5,2)`), kẹp `[0, 10]` | `double` làm 0.1 + 0.2 ≠ 0.3; điểm tổng lệch ở chữ số thập phân thứ hai, làm tròn ra sai một mức, và sinh viên khiếu nại đúng. **Không bao giờ `double`/`float` ở bất cứ đâu liên quan tới điểm.** |
| Thời gian | `DateTimeOffset` → `timestamptz` UTC | `DateTime` mất offset: hạn nộp 23:59 thành giờ nào tùy máy chủ, `is_late` sai cho cả lớp. |
| "Bây giờ" | luôn qua `TimeProvider` DI | `DateTimeOffset.UtcNow` trong domain làm test phải `Thread.Sleep`; suite vượt ngưỡng 10 giây và sẽ bị bỏ chạy. |

```csharp
// ĐÚNG
var subject = new Subject { Id = Guid.CreateVersion7(), CreatedAt = timeProvider.GetUtcNow() };

// SAI — cả ba lỗi trong một dòng
var subject = new Subject { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
```

---

## 3. Enum xuống DB: luôn là chuỗi

EF **mặc định map enum ra `integer`**. Dự án này không chấp nhận điều đó.

```csharp
b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
```

Áp cho **mọi** enum: `SubmissionStatus`, `LessonComponentType`, `UnlockRule`, `BonusTarget`,
`AggregationKind`, `ConditionOperator`, `CapAction`, `GradeExportSource`, `GradeSourceType`…

Thiếu `HasMaxLength(32)` → Npgsql tạo cột `text` không giới hạn: chấp nhận được về mặt chạy,
nhưng lệch với phần còn lại của schema và làm diff migration ồn ào ở lần sau.

Thiếu `HasConversion<string>()` → cột là `integer`. Đọc log DB thấy `status = 3` không ai hiểu là
gì, và mọi CHECK constraint viết bằng chuỗi **chết ngay lúc apply migration** vì so sánh
`integer = text`.

---

## 4. Enum BÊN TRONG jsonb — chỗ sai đắt nhất của tầng dữ liệu

Cột jsonb của dự án (`config_json`, `aggregation`, `grade_conditions`, `answer_config`,
`rubric_scores_json`, `file_tree_json`…) map bằng `ToJson()` sang complex type **có kiểu**,
không dùng `string` hay `JsonDocument` trần (ADR: bảng §1, dòng "Cột `jsonb`").

**EF Core dùng `JsonValueReaderWriter` riêng của nó cho cột `ToJson()`.** Nó **KHÔNG** đọc
`JsonStringEnumConverter` mà bạn cấu hình trong `AddControllers().AddJsonOptions(...)` hay
`ConfigureHttpJsonOptions(...)` — hai chỗ đó chỉ cấu hình serializer của ASP.NET Core cho
request/response HTTP. Hai thế giới tách rời.

```csharp
// ✅ ĐÚNG — khai tường minh từng enum nằm trong jsonb
modelBuilder.Entity<GradeColumn>().OwnsOne(c => c.Aggregation, b =>
{
    b.ToJson();
    b.Property(a => a.Kind).HasConversion<string>();
});

modelBuilder.Entity<CourseOffering>().OwnsMany(o => o.GradeConditions, b =>
{
    b.ToJson();
    b.Property(c => c.Op).HasConversion<string>();
    b.Property(c => c.Action).HasConversion<string>();
});
```

```csharp
// ❌ SAI — "JSON chung đã cấu hình JsonStringEnumConverter rồi mà"
modelBuilder.Entity<GradeColumn>().OwnsOne(c => c.Aggregation, b => b.ToJson());
```

**Hậu quả của bản SAI:** cột jsonb chứa `{"Kind": 6, "N": 2}` thay vì `{"Kind": "DropLowestN", …}`.
Không có exception, không có test đỏ nếu test chỉ round-trip qua EF. Nhưng:
mở `psql` đọc log không hiểu gì; mọi truy vấn JSON theo tên enum về sau (`aggregation->>'Kind' =
'DropLowestN'`) **trượt im lặng trả rỗng**; và ngày thêm một giá trị vào giữa enum thì toàn bộ dữ
liệu cũ đổi nghĩa — `6` hôm nay không còn là `DropLowestN` ngày mai. Đó là hỏng dữ liệu lịch sử,
không phải bug hiển thị.

### Mỗi property complex cần MỘT dòng `OwnsOne` riêng

EF **không suy ra** cấu hình owned type từ entity khác, kể cả khi hai property cùng kiểu C#.

```csharp
// ✅ ĐÚNG — hai nơi có GradeAggregation thì có hai dòng
modelBuilder.Entity<GradeColumn>().OwnsOne(c => c.Aggregation, b =>
{
    b.ToJson();
    b.Property(a => a.Kind).HasConversion<string>();
});

modelBuilder.Entity<GradeSchemeTemplateItem>().OwnsOne(i => i.Aggregation, b =>
{
    b.ToJson();
    b.Property(a => a.Kind).HasConversion<string>();
});
```

```csharp
// ❌ SAI — khai một lần rồi tưởng GradeSchemeTemplateItem.Aggregation cũng được hưởng
modelBuilder.Entity<GradeColumn>().OwnsOne(c => c.Aggregation, b => { b.ToJson(); … });
// (không có dòng nào cho GradeSchemeTemplateItem)
```

**Hậu quả:** đây không phải lỗi tinh vi — **mô hình EF không dựng được và ứng dụng không khởi
động**. Thông báo lỗi nói về một kiểu chưa được map, không nói bạn thiếu dòng nào, nên rất dễ đi
tìm nhầm chỗ. Quy tắc kiểm nhanh: **đếm số property kiểu complex trong toàn bộ Domain, phải bằng
số dòng `OwnsOne`/`OwnsMany` trong `OnModelCreating` + các `IEntityTypeConfiguration`.**

---

## 5. CHECK constraint: giá trị chuỗi phải khớp ĐÚNG tên enum C#

```sql
-- ✅ ĐÚNG
ALTER TABLE bonus_points ADD CONSTRAINT ck_bonus_target_column CHECK (
    (target = 'GradeColumn' AND target_column_id IS NOT NULL) OR
    (target = 'FinalTotal'  AND target_column_id IS NULL));

-- ❌ SAI — snake_case hóa cả giá trị
    (target = 'grade_column' AND target_column_id IS NOT NULL) OR
    (target = 'final_total'  AND target_column_id IS NULL));
```

**Hậu quả của bản SAI — đọc kỹ, đây là loại lỗi tệ nhất:** migration chạy **trót lọt**, bảng tạo
ra bình thường, test schema xanh. Nhưng cột lưu `'GradeColumn'` (vì mục 3), CHECK đòi
`'grade_column'`, nên **mọi lệnh INSERT đều bị chặn**. Bạn phát hiện lúc cấp điểm cộng đầu tiên —
thường là trên lớp, giữa hai nhóm báo cáo.

Hai lỗi ở mục 3 và mục 5 **che nhau**: nếu quên `HasConversion<string>()` thì cột là `integer`,
migration chết ngay lúc apply (dễ thấy). Sửa `HasConversion` mà vẫn giữ `'grade_column'` thì lỗi
chuyển từ dạng ồn ào sang dạng im lặng. **Sửa một cái phải kiểm luôn cái kia.**

Cách khai trong EF (đi cùng migration, không chạy SQL tay ngoài luồng):

```csharp
b.ToTable(t => t.HasCheckConstraint("ck_bonus_target_column", """
    (target = 'GradeColumn' AND target_column_id IS NOT NULL) OR
    (target = 'FinalTotal'  AND target_column_id IS NULL)
    """));
```

Quy ước tên: `ck_<bảng>_<ý nghĩa>` (`ck_submission_slots_owner`, `ck_bonus_points_positive`,
`ck_bonus_reason_not_blank`, `ck_grade_column_key_slug`).

**Mỗi CHECK constraint phải có một integration test cố tình chèn hàng vi phạm bằng SQL thô và
khẳng định database từ chối.** Không có test đó thì bạn không biết constraint có thật sự hoạt
động hay chỉ tồn tại trong file migration.

---

## 6. FK đa hình → hai cột nullable + CHECK (ADR-003)

Postgres không tạo được FK cho cột đa hình `owner_type`/`owner_id`. Dữ liệu mồ côi là chuyện sớm
muộn: xóa một `Assignment` để lại slot và submission trỏ vào hư không, và không có lệnh nào báo lỗi.

Giữ `owner_type` (để lọc trong code và giữ đúng hợp đồng API ở SRS mục 7), nhưng lưu bằng **hai
cột FK thật**:

```csharp
public sealed class SubmissionSlotConfiguration : IEntityTypeConfiguration<SubmissionSlot>
{
    public void Configure(EntityTypeBuilder<SubmissionSlot> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(32);

        b.HasOne<Assignment>().WithMany()
         .HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<FinalAssessment>().WithMany()
         .HasForeignKey(x => x.FinalAssessmentId).OnDelete(DeleteBehavior.Cascade);

        b.ToTable(t => t.HasCheckConstraint("ck_submission_slots_owner", """
            (owner_type = 'Assignment'      AND assignment_id IS NOT NULL AND final_assessment_id IS NULL) OR
            (owner_type = 'FinalAssessment' AND final_assessment_id IS NOT NULL AND assignment_id IS NULL)
            """));
    }
}
```

Cả hai cột **nullable**, CHECK bảo đảm **đúng một** cột khác null và khớp với `owner_type`.
Quy đổi sang `owner_type` + `owner_id` của API làm ở tầng mapping, không đổi hợp đồng.

Cùng khuôn mẫu áp cho `Quiz` (Gap #3): `subject_version_id` **hoặc** `offering_id`, đúng một cái
khác null. Bỏ CHECK ở đây thì một Quiz thuộc cả hai nơi cùng lúc, và clone version sẽ nhân đôi nó.

---

## 7. Soft delete + named query filter

Nội dung dạy học (`Subject`, `SubjectVersion`, `Chapter`, `Lesson`, `LessonComponent`,
`Assignment`, `Quiz`) có `IsDeleted` + `DeletedAt`.
**Dữ liệu sinh viên (`Submission`, `GradeRecord`, `GradeAuditLog`) KHÔNG BAO GIỜ xóa** — kể cả
xóa mềm. Xóa cứng một Chapter đang có bài đã nộp là mất điểm thật, không khôi phục được.

EF Core 10 có *named query filter* — cho phép chồng nhiều filter trên một entity:

```csharp
b.HasQueryFilter("SoftDelete", x => !x.IsDeleted);
```

Nếu bản EF đang dùng chưa có overload `(name, expr)` thì dùng `b.HasQueryFilter(x => !x.IsDeleted)`
(một filter mỗi entity). Chỉ khi cần chồng nhiều điều kiện mới cần dạng có tên.

**Query filter KHÔNG dùng cho cách ly giảng viên.** Cám dỗ là thêm filter
`x => x.InstructorId == currentUser.Id` cho gọn. Đừng: filter đó chạy cả trong job Hangfire và
seeder — nơi không có người dùng đăng nhập — nên hoặc ném lỗi, hoặc lọc theo `Guid.Empty` và trả
rỗng. ADR-012 chốt: **mọi truy vấn danh sách lọc `instructor_id` ngay trong câu truy vấn**, không
lọc sau khi nạp, không giấu vào filter toàn cục.

Muốn đọc cả bản đã xóa (màn hình lịch sử phiên bản): `IgnoreQueryFilters()` tại đúng câu truy vấn
đó, không gỡ filter khỏi cấu hình.

---

## 8. Partial unique index

Nộp lại bài = tạo hàng mới `attempt_no + 1`, hàng cũ `is_current = false` (Gap #5). Bất biến "mỗi
người nộp chỉ có đúng một bản hiện hành" phải do **database** giữ, không phải do handler nhớ:

```sql
CREATE UNIQUE INDEX ux_submission_current
  ON submissions (assignment_id, final_assessment_id, enrollment_id, group_id)
  NULLS NOT DISTINCT
  WHERE is_current;
```

Khai trong EF để nó nằm trong migration:

```csharp
b.HasIndex(x => new { x.AssignmentId, x.FinalAssessmentId, x.EnrollmentId, x.GroupId })
 .IsUnique()
 .HasFilter("is_current");   // tên cột snake_case — đây là SQL thô, không phải LINQ
```

Chú ý `HasFilter` nhận **SQL thô**: viết `IsCurrent` (PascalCase) là index sinh ra tham chiếu cột
không tồn tại và migration chết lúc apply.

Không có index này, một cú double-click nút "Nộp chính thức" tạo hai hàng cùng `is_current = true`;
sổ điểm lấy `Single()` và ném exception, hoặc lấy `First()` và chấm nhầm bài. Unique index thường
`(offering_id, key)` cho `GradeColumn`, `(enrollment_id, grade_column_id)` cho `GradeRecord`.

Cập nhật `is_current` phải chạy trong cùng command: `TransactionBehavior` đã bọc sẵn mọi
`ICommand` (ADR-010) — **không tự gọi `BeginTransactionAsync` trong handler**.

---

## 9. Không sửa owned entity đang được EF theo dõi

Ca thật: sổ điểm có công tắc "Xem điểm tạm tính (bỏ qua bài chưa chấm)" — chỉ đổi **cách xem**,
không đổi dữ liệu (ADR-008).

```csharp
// ❌ SAI — cách viết "tự nhiên" nhất, và là cái bẫy
var columns = await db.GradeColumns.Where(c => c.OfferingId == offeringId).ToListAsync(ct);
foreach (var c in columns)
    if (provisional) c.Aggregation.TreatMissingAsZero = false;   // ⚠️ đang sửa entity được theo dõi
```

```csharp
// ✅ ĐÚNG — AsNoTracking + dựng BẢN SAO của quy tắc gộp
var columns = await db.GradeColumns
    .AsNoTracking()
    .Where(c => c.OfferingId == offeringId)
    .ToListAsync(ct);

var rules = columns.ToDictionary(c => c.Id, c => new GradeAggregation
{
    Kind = c.Aggregation.Kind,
    N    = c.Aggregation.N,
    TreatMissingAsZero = provisional ? false : c.Aggregation.TreatMissingAsZero,
});
```

**Hậu quả của bản SAI:** `GradeAggregation` là owned entity **có setter** và đang nằm trong change
tracker. Chỉ cần `SaveChangesAsync` chạy ở **bất kỳ đâu trong cùng scope** — một behavior, một
audit log, một command chạy sau đó — là cấu hình thật của cột bị ghi đè xuống DB.
**Một lần giảng viên bật công tắc "xem điểm tạm tính" là đổi vĩnh viễn cách tính điểm của cả lớp**:
không thông báo, không log, không ai lần ra nguyên nhân, và điểm hiển thị hôm sau khác hôm nay.

Luật tổng quát: **mọi truy vấn chỉ để đọc đều `AsNoTracking()`**, và mọi biến thể theo tham số
hiển thị đều dựng object mới. Không sửa entity đang tracked để phục vụ một lần đọc.

---

## 10. Integration test: Postgres thật trong Testcontainers

**Không dùng InMemory provider. Không có ngoại lệ.** Lý do cụ thể, không phải khẩu hiệu:

- **jsonb** — InMemory giữ object trong bộ nhớ, không serialize gì cả. Lỗi ở mục 4 (enum ra số)
  **không thể** phát hiện được.
- **CHECK constraint** — InMemory không có. Test "chèn hàng vi phạm phải bị từ chối" ở mục 5 và 6
  sẽ **xanh trong khi database thật sẽ chặn**, hoặc ngược lại.
- **Partial unique index** — InMemory không hiểu `WHERE is_current`. Bất biến ở mục 8 không được
  kiểm chứng.
- Ngoài ra: `text[]`, `LeftJoin`, thứ tự sắp xếp theo collation, `timestamptz` — tất cả đều khác.

Nói ngắn: test qua InMemory **xanh giả**. Nó chứng minh code chạy được trên một cấu trúc dữ liệu
không phải Postgres.

```csharp
/// <summary>
/// Dựng API thật trên một Postgres thật trong container. Không dùng InMemory provider:
/// hệ thống này phụ thuộc nặng vào jsonb, CHECK constraint và partial index — những thứ
/// InMemory không mô phỏng, nên test qua nó sẽ xanh giả.
/// </summary>
public sealed class TeachHubApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();
    // …
}
```

Ranh giới tầng test (kiến trúc §5 "Test"):

- Luật tính điểm, clone, unlock, late penalty → `Domain.Tests`, thuần, **không I/O**, toàn bộ
  suite < 10 giây.
- Handler + validator → `Application.Tests`, `new` handler rồi gọi `Handle`, **không dựng HTTP**.
- Schema, jsonb, CHECK, index, controller, auth policy, serialization → `Api.Tests` với container.

TDD bắt buộc: test **đỏ trước**, thấy đỏ **vì đúng lý do mong đợi**, rồi mới code.
Docker chưa bật (Colima) → Testcontainers không chạy → **BÁO LẠI**, không đổi sang InMemory
"tạm cho qua". Đổi tạm một lần là mất luôn lớp bảo vệ.

**Phase nào thêm loại tài nguyên mới thuộc về giảng viên thì PHẢI thêm dòng vào
`CrossInstructorIsolationTests`** (Phase 0 Task 0.8c). Thiếu dòng đó thì task chưa được tick —
một entity quên lọc `instructor_id` là lộ điểm và bài nộp của lớp người khác, và loại lỗi này
không có triệu chứng nào.

---

## 11. Checklist trước khi commit tầng dữ liệu

- [ ] Id sinh bằng `Guid.CreateVersion7()` ở tầng ứng dụng, không để DB sinh.
- [ ] Không có `double`/`float` ở bất kỳ chỗ nào liên quan tới điểm; điểm và trọng số là `decimal`.
- [ ] Không có `DateTime`; mọi mốc thời gian là `DateTimeOffset`, lấy qua `TimeProvider`.
- [ ] Mọi enum xuống DB có `.HasConversion<string>().HasMaxLength(32)`.
- [ ] **Đếm**: số property kiểu complex trong Domain == số dòng `OwnsOne`/`OwnsMany`.
- [ ] Mỗi `OwnsOne(...b => b.ToJson())` có enum bên trong đều khai `.HasConversion<string>()` cho
      từng enum đó.
- [ ] Mọi giá trị chuỗi trong CHECK constraint khớp **đúng tên enum C#** (`'GradeColumn'`),
      không snake_case.
- [ ] Mỗi CHECK constraint mới có một test chèn SQL thô vi phạm và khẳng định DB từ chối.
- [ ] FK đa hình dùng hai cột nullable + CHECK, không dùng một cột `owner_id` trần.
- [ ] `HasFilter(...)` viết tên cột **snake_case** (SQL thô), không PascalCase.
- [ ] Mọi truy vấn chỉ-đọc có `AsNoTracking()`; không sửa owned entity đang tracked.
- [ ] Mọi truy vấn danh sách lọc `instructor_id` **ngay trong câu truy vấn**.
- [ ] Migration sinh bằng `dotnet ef migrations add`, đã **đọc file sinh ra**, không có `DROP`
      ngoài ý muốn.
- [ ] Migration đã apply thành công trên DB trống **và** DB có sẵn dữ liệu.
- [ ] Integration test chạy trên Testcontainers Postgres 17, không InMemory.
- [ ] `dotnet build` → **0 Warning(s) 0 Error(s)** (`TreatWarningsAsErrors` đang bật).
- [ ] Nếu thêm tài nguyên mới thuộc giảng viên: đã thêm dòng vào `CrossInstructorIsolationTests`.

Chú thích code, tên test và thông báo lỗi cho người dùng viết **tiếng Việt**; tên kiểu/biến/hàm và
commit message viết tiếng Anh.
