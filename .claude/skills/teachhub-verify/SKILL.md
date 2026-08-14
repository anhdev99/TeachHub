---
name: teachhub-verify
description: Vòng lặp TDD và cổng kiểm chất lượng của TeachHub — viết test đỏ trước, đỏ vì đúng lý do, rồi mới code; sau đó chạy `dotnet build` 0 cảnh báo, toàn bộ test suite, và bộ script grep/python kiểm các luật dự án (Domain không tham chiếu hạ tầng, record command không có tham số `= default`, enum trong jsonb thiếu `HasConversion<string>`, `[Authorize(Policy...)]` đặt sai ở cấp class, tàn dư Minimal API, `await next(ct)`, `double` trong code điểm, 403 ở chỗ lẽ ra 404). Dùng skill này khi verify một module vừa xong, khi đóng một task theo Definition of Done, khi build đỏ hay test đỏ mà chưa rõ vì sao, khi cần biết "test này đỏ có đúng lý do không", khi test suite bắt đầu chậm, và cả khi "chạy lại", "kiểm lại", "sửa cho xanh", "bổ sung test", "cập nhật ma trận cách ly" sau một vòng sửa.
---

# Cổng kiểm chất lượng TeachHub

Skill này là công cụ làm việc của agent `build-verifier` và là luật bắt buộc cho mọi agent viết
code. Nó trả lời đúng một câu hỏi: **task này đã "Xong" thật chưa, hay chỉ trông như đã xong?**

Nguyên tắc bao trùm: **kiểm bằng lệnh chạy được, không kiểm bằng mắt.** Đọc code rồi kết luận
"có vẻ ổn" là cách mà mọi lỗi ở mục 4 đã lọt qua trong phiên thiết kế — mỗi mục ở đó là một lỗi
THẬT đã trả giá bằng build đỏ hoặc dữ liệu sai, nay biến thành một lệnh chạy trong một giây.
Chạy mọi lệnh tại gốc repo: `/Users/steven/repositories/anhdev99/TeachHub`.

---

## 0. Kiểm INCREMENTAL — sau mỗi module, không đợi hết phase

Chạy cổng kiểm **ngay khi một module xong** (entity + configuration, một use case
Command/Handler/Validator, một controller), không gom lại cuối phase:

```
module xong → teachhub-verify (mục 3 + mục 4) → module tiếp theo
                     ↓ đỏ
          trả về đúng agent gây lỗi, không tick task
```

Lỗi kiến trúc để lâu thì **giá sửa tăng theo cấp số**. Một `OwnsOne` thiếu `HasConversion<string>`
phát hiện ngay thì sửa một dòng; phát hiện sau ba module thì migration đã apply, dữ liệu jsonb đã
ghi số thay vì chuỗi, phải viết thêm migration sửa dữ liệu. Một `Result.Forbidden` sai chỗ phát
hiện ngay thì sửa một handler; phát hiện cuối phase thì mười handler đã chép cùng khuôn sai đó.

---

## 1. Vòng lặp TDD — năm bước, không bỏ bước nào

```
1. Viết test cho hành vi mong muốn
2. CHẠY test → thấy ĐỎ, và đọc thông báo đỏ để xác nhận nó đỏ VÌ ĐÚNG LÝ DO
3. Viết code TỐI THIỂU đủ làm nó xanh — không viết thêm gì cho "sau này"
4. CHẠY lại → xanh, và toàn bộ suite vẫn xanh
5. Commit theo Conventional Commits
```

### Vì sao bước 2 không bỏ được

**Một test chưa từng đỏ là một test chưa từng chứng minh nó kiểm được gì.**

Test luôn xanh có thể xanh vì hai lý do hoàn toàn khác nhau, và từ bên ngoài chúng giống hệt
nhau: (a) code đúng, hoặc (b) test không thực sự chạm vào code — assert vào biến cục bộ, quên
`await` nên `Task` chưa chạy đã kết thúc test, `[Fact]` viết thành `[Theory]` không có data nên
xUnit bỏ qua, hoặc mock trả sẵn giá trị mong đợi. Chỉ có một cách phân biệt: **bắt nó đỏ một
lần trước đã.** Đỏ rồi xanh = nó có nối với code. Xanh từ đầu = không biết gì cả.

Hậu quả cụ thể khi bỏ bước 2 trong dự án này: `GradebookCalculator` có 30 test xanh, mọi người
tin sổ điểm đúng, cuối kỳ phát hiện `BonusLedger` cộng vào cột chưa nhân trọng số nên điểm tổng
sai — và không test nào đỏ vì tất cả đều so sánh với kết quả do chính hàm đó sinh ra. Điểm đã
nộp cho phòng đào tạo.

### Đỏ đúng lý do vs. đỏ sai lý do

Không phải màu đỏ nào cũng tính. Đọc thông báo lỗi và phân loại:

| Thông báo đỏ | Tính là "đỏ đúng lý do"? | Xử lý |
|---|---|---|
| `CS0246: type 'Entity' could not be found` | ✅ Có — code chưa tồn tại, đúng trạng thái mong đợi | Viết implementation |
| `Assert.Equal() Failure: Expected 8.0, Actual 10.0` | ✅ Có — logic chưa đúng | Sửa logic |
| `Shouldly.ShouldAssertException: res.StatusCode should be NotFound but was Forbidden` | ✅ Có — đúng thứ đang muốn kiểm | Sửa handler |
| `NullReferenceException` ở dòng dựng fixture | ❌ Không — hỏng ở khâu chuẩn bị | Sửa test rồi chạy lại, chưa được viết code |
| `CS1593: Delegate 'RequestHandlerDelegate<T>' does not take 0 arguments` | ❌ Không — gõ sai lambda trong test | Đổi `() =>` thành `_ =>` |
| `CS0103: The name 'subjecId' does not exist` | ❌ Không — gõ sai tên biến | Sửa chính tả, chạy lại |
| `Testcontainers: Cannot connect to Docker daemon` | ❌ Không — hạ tầng test chưa sẵn sàng | Bật Docker, chạy lại |

**Luật:** chỉ được sang bước 3 khi thông báo đỏ nói đúng về **hành vi đang thiếu**. Đỏ vì gõ
sai tên biến rồi sửa tên cho hết đỏ là đã đi thẳng từ bước 1 sang bước 4 — test chưa bao giờ
được chứng minh.

### Ghi lại lý do đỏ

Khi báo cáo, phải nêu **thông báo đỏ đã thấy**, không chỉ "đã làm TDD":

```
✅ ĐÚNG:  Task 4b.3 — test `Bonus_vao_cot_chi_cong_theo_trong_so` đỏ với
          "Expected 0.1 but was 0.5" (bonus đang cộng thẳng vào tổng), sau đó xanh.
❌ SAI:   Task 4b.3 — đã viết test trước, test xanh.
```

Câu SAI không kiểm chứng được và vì thế vô giá trị: nó đúng kể cả khi test chưa từng đỏ.

---

## 2. Định nghĩa "Xong" — bảy mục, thiếu một là chưa tick

Lấy nguyên từ `docs/superpowers/plans/2026-08-13-teachhub-roadmap.html` §Định nghĩa "Xong".
Áp cho **mọi** task của **mọi** phase:

1. Có test viết **trước**, đã thấy **đỏ vì đúng lý do mong đợi**, rồi mới có code làm nó xanh.
2. `dotnet build` không cảnh báo (`TreatWarningsAsErrors` đang bật).
3. Toàn bộ test suite xanh, không chỉ test vừa viết.
4. Chạm schema → có migration EF Core, apply thành công trên DB trống **lẫn** DB có sẵn dữ liệu.
5. Chạm API → OpenAPI doc sinh lại được, client TS (`pnpm gen:api`) sinh lại được, frontend vẫn build.
6. Đã commit theo Conventional Commits.
7. Phase thêm **loại tài nguyên mới thuộc về giảng viên** → đã thêm dòng vào
   `CrossInstructorIsolationTests` (Phase 0 Task 0.8c).

Ngoài DoD từng task: kết thúc mỗi phase phải chạy **kịch bản nghiệm thu** ghi trong plan của
phase đó — thao tác tay trên UI thật, không phải chỉ test tự động.

### Hai mục hay quên nhất

**Mục 1 — test viết trước.** Bị "quên" theo cách khó thấy: code viết xong rồi mới viết test cho
khớp code. Test kiểu đó chỉ chép lại hành vi hiện có, kể cả khi hành vi đó sai. Dấu hiệu khi
review: test không có commit riêng trước commit implementation, hoặc tên test mô tả *cách làm*
(`Handler_goi_SaveChanges`) thay vì *hành vi mong muốn* (`Khong_luu_khi_tong_trong_so_khac_100`).

**Mục 7 — ma trận `CrossInstructorIsolationTests`.** Nguy hiểm nhất vì lỗi nó bắt **không có
triệu chứng nào**: không exception, không log lạ, chỉ là dữ liệu của giảng viên khác hiện ra đúng
chỗ mong đợi — không ai phát hiện cho tới lúc một giảng viên thấy lớp của đồng nghiệp trong danh
sách của mình.

File: `backend/tests/TeachHub.Api.Tests/Security/CrossInstructorIsolationTests.cs`. Mỗi loại
tài nguyên mới của giảng viên = **một dòng mới** trong `TaiNguyenCuaGiangVienA()`:

```csharp
{ "GET",    "/api/assignments/{assignmentId}" },
{ "PUT",    "/api/grade-columns/{gradeColumnId}" },
{ "DELETE", "/api/bonus-points/{bonusPointId}" },
```

Kèm dòng tương ứng trong `EndpointDanhSach` nếu tài nguyên đó có endpoint danh sách, và cập nhật
`IsolationFixture` để seed nó vào "thế giới của GV A". Ma trận chỉ có giá trị khi nó **lớn cùng
nhịp với hệ thống**: bổ sung muộn ba phase là ba phase endpoint không được canh gác, và lúc đó
không ai còn nhớ endpoint nào đã kiểm rồi.

```bash
# Số endpoint có tham số id  vs  số dòng trong ma trận — chênh lệch lớn dần = ma trận đang tụt lại
grep -rcE '\[Http(Get|Put|Post|Delete|Patch)\("[^"]*\{' \
  backend/src/TeachHub.Api/Controllers --include='*.cs' | grep -v ':0$'
grep -c '{ "' backend/tests/TeachHub.Api.Tests/Security/CrossInstructorIsolationTests.cs
```

Không phải mọi endpoint đều cần một dòng (endpoint của sinh viên, endpoint public), nhưng mọi
tài nguyên **thuộc về giảng viên** thì cần.

---

## 3. Cổng build — `0 Warning(s) 0 Error(s)`

```bash
dotnet build backend/TeachHub.sln     # kỳ vọng nguyên văn: Build succeeded. · 0 Warning(s) · 0 Error(s)
dotnet test  backend/TeachHub.sln     # toàn bộ suite, không chỉ test vừa viết
```

`TreatWarningsAsErrors=true` bật trong `backend/Directory.Build.props` (Phase 0 Task 0.1), nên
một cảnh báo **là** một lỗi build. Không có mức "cảnh báo chấp nhận được".

**Ba cách "sửa" bị cấm tuyệt đối**, vì cả ba đều là gỡ chuông báo cháy thay vì dập lửa:

| Cấm | Vì sao |
|---|---|
| Hạ `TreatWarningsAsErrors` xuống `false` | Nới ra một lần là không bao giờ siết lại được — mọi cảnh báo sau đó chìm trong hàng trăm dòng log |
| `<NoWarn>` cho cả project | Che luôn những cảnh báo chưa xuất hiện, kể cả cảnh báo thật |
| Tắt `NuGetAudit` để né `NU1903` | `NU1903` là lỗ hổng bảo mật có thật. Ví dụ đã gặp: `Hangfire.AspNetCore` kéo `Newtonsoft.Json` 11.0.1 (GHSA-5crp-9r3c-p9vr). Cách đúng là khai trực tiếp `Newtonsoft.Json` 13.0.4 để nâng version được giải quyết |

Cảnh báo hay gặp nhất và cách sửa đúng: `CS8602 Dereference of a possibly null reference` sau
`if (!kq.IsSuccess) return ...; kq.Value.X`. Nguyên nhân không nằm ở chỗ dùng, mà ở
`Result<T>.IsSuccess` **thiếu** `[MemberNotNullWhen(true, nameof(Value))]`. Sửa ở `Result.cs`,
không rắc `!` vào từng chỗ dùng — rắc `!` là tắt kiểm null ở đúng những chỗ cần nó nhất.

---

## 4. Script kiểm tuân thủ — chạy, đừng đọc bằng mắt

Mỗi khối dưới đây: **kỳ vọng không in ra gì**. Có dòng nào in ra là có vi phạm.

### 4.1 Domain tham chiếu hạ tầng (L2 · ADR-002)

```bash
grep -rnE '^using (Microsoft\.EntityFrameworkCore|Npgsql|Hangfire|MediatR|FluentValidation|Amazon)' \
  backend/src/TeachHub.Domain --include='*.cs'

# AspNetCore trong Domain — ngoại lệ DUY NHẤT đã được duyệt là IdentityUser<TKey>
# (package Microsoft.Extensions.Identity.Stores, POCO thuần, không kéo hạ tầng — Phase 0 Task 0.4)
grep -rn '^using Microsoft.AspNetCore' backend/src/TeachHub.Domain --include='*.cs' \
  | grep -v 'Microsoft.AspNetCore.Identity;'

grep -nE '<PackageReference Include="(Microsoft\.EntityFrameworkCore|Npgsql|Hangfire|MediatR|FluentValidation|AWSSDK|Microsoft\.AspNetCore)' \
  backend/src/TeachHub.Domain/TeachHub.Domain.csproj

# Nguồn chân lý — grep chỉ là bản kiểm nhanh 200ms trước khi chạy cái này
dotnet test backend/tests/TeachHub.Domain.Tests --filter ArchitectureTests

# TransactionBehavior cần AppDbContext nên phải nằm ở Infrastructure, không phải Application
ls backend/src/TeachHub.Application/Common/Behaviors/TransactionBehavior.cs 2>/dev/null \
  && echo "SAI TẦNG: TransactionBehavior phải nằm ở Infrastructure/Behaviors"
```

**Hậu quả nếu sai:** Domain kéo theo EF Core thì luật nghiệp vụ không còn test được trong
mili-giây — mỗi test tính điểm phải dựng DbContext, suite domain từ 2 giây lên 2 phút, và người
ta ngừng chạy nó. `MediatR`/`FluentValidation` bị cấm cùng lý do: `ICommand`/`IQuery` là hợp đồng
của tầng Application; cho Domain biết đến chúng là mở đường cho "Domain phát `INotification` làm
domain event".

### 4.2 Record command có tham số positional `= default` (L6)

```bash
python3 - <<'PY'
import re, pathlib
pat = re.compile(r'\brecord\s+(?:struct\s+)?(\w+(?:Command|Query))\s*\((.*?)\)', re.S)
val = re.compile(r'\b(Guid|int|long|short|decimal|double|float|bool|char|DateTime|DateTimeOffset|TimeSpan)\??\s+(\w+)\s*=')
for f in pathlib.Path('backend/src/TeachHub.Application').rglob('*.cs'):
    s = f.read_text(encoding='utf-8')
    for ten, thamso in pat.findall(s):
        for kieu, prop in val.findall(thamso):
            print(f'{f}: {ten} — `{kieu} {prop} = …` là tham số positional có mặc định, phải là property init-only')
PY
```

**Hậu quả nếu sai:** `JsonSchemaExporter` của .NET 10 ném `JsonException` khi gặp tham số
positional kiểu giá trị có giá trị mặc định → `GET /openapi/v1.json` trả **500** → `pnpm gen:api`
chết → frontend không sinh được client. Build vẫn xanh, test unit vẫn xanh; chỉ vỡ ở đúng cái
đường ống mà DoD mục 5 đi qua.

```csharp
// ❌ SAI — OpenAPI 500
public sealed record CreateSubjectCommand(string Ten, Guid InstructorId = default) : ICommand<Guid>;

// ✅ ĐÚNG — trường server tự điền là property init-only, không nằm trong danh sách positional
public sealed record CreateSubjectCommand(string Ten) : ICommand<Guid>
{
    public Guid InstructorId { get; init; }
}
```

### 4.3 Enum trong jsonb thiếu `HasConversion<string>` (L9)

```bash
python3 - <<'PY'
import re, pathlib
src = {f: f.read_text(encoding='utf-8') for f in pathlib.Path('backend/src/TeachHub.Domain').rglob('*.cs')}
enums = {e for s in src.values() for e in re.findall(r'\benum\s+(\w+)', s)}
kieu_cua, enum_trong = {}, {}
for s in src.values():
    for t, body in re.findall(r'\b(?:class|record|struct)\s+(\w+)[^;{]*\{(.*?)\n\}', s, re.S):
        for ty, name in re.findall(r'public\s+([A-Za-z_]\w*)\??\s+(\w+)\s*\{\s*get', body):
            kieu_cua[name] = ty
            if ty in enums:
                enum_trong.setdefault(t, []).append(name)
for f in pathlib.Path('backend/src/TeachHub.Infrastructure').rglob('*.cs'):
    s = f.read_text(encoding='utf-8')
    for m in re.finditer(r'OwnsOne\([^,]*=>\s*\w+\.(\w+)\s*,\s*\w+\s*=>\s*\{(.*?)\}\s*\)', s, re.S):
        prop, block = m.groups()
        if 'ToJson()' not in block:
            continue
        for e in enum_trong.get(kieu_cua.get(prop, ''), []):
            if not re.search(rf'\.{e}\s*\)\s*\.HasConversion<string>', block):
                print(f'{f}: OwnsOne(… {prop}) có ToJson() nhưng thiếu .Property(a => a.{e}).HasConversion<string>()')
PY

# Bổ sung — đếm bằng mắt một lần: mỗi property kiểu complex cần MỘT dòng OwnsOne riêng
grep -rn 'OwnsOne(' backend/src/TeachHub.Infrastructure/Persistence/Configurations --include='*.cs'
```

**Hậu quả nếu sai:** EF dùng `JsonValueReaderWriter` riêng cho cột jsonb và **không** đọc
`JsonStringEnumConverter` của ASP.NET Core. Enum vì thế ghi xuống jsonb dưới dạng **số**, trong
khi cùng enum đó ở cột thường là chuỗi. Không có lỗi lúc ghi; vỡ khi truy vấn theo giá trị enum
trong jsonb, hoặc khi CHECK constraint so với `'GradeColumn'` mà cột chứa `0`.

Script trên là heuristic (không đọc được `OwnsOne` lồng nhau): khi nó im lặng mà file vẫn có
`ToJson()`, đọc thủ công những khối mà dòng `grep` cuối in ra.

```csharp
// ✅ ĐÚNG — mỗi property complex một dòng OwnsOne, mỗi enum bên trong một dòng HasConversion
builder.OwnsOne(x => x.Aggregation, b => { b.ToJson(); b.Property(a => a.Kind).HasConversion<string>(); });
builder.OwnsOne(x => x.Condition,   b => { b.ToJson(); b.Property(a => a.Operator).HasConversion<string>(); });

// ❌ SAI — EF không suy ra cấu hình của Condition từ Aggregation dù cùng kiểu complex
builder.OwnsOne(x => x.Aggregation, b => { b.ToJson(); b.Property(a => a.Kind).HasConversion<string>(); });
```

### 4.4 `[Authorize(Policy = …)]` đặt ở cấp class (L8)

```bash
# In ra dòng nào = có [Authorize(Policy = …)] nằm ngay trên một khai báo class controller
grep -rnA3 '^[[:space:]]*\[Authorize(Policy' backend/src/TeachHub.Api --include='*.cs' \
  | grep -E 'class [A-Za-z]+Controller'

# Không có policy InstructorOrTa — vai trò trợ giảng tắt hoàn toàn ở MVP (ADR-013)
grep -rn 'InstructorOrTa' backend/src backend/tests --include='*.cs'

# Endpoint hạ tầng phải AllowAnonymous vì SetFallbackPolicy đóng mặc định toàn hệ thống
grep -rn 'MapHealthChecks\|MapOpenApi' backend/src/TeachHub.Api --include='*.cs' \
  | grep -v 'AllowAnonymous'
```

**Hậu quả nếu sai:** `[Authorize]` ở class và ở action **cộng dồn**, không ghi đè. Một policy đặt
ở class rồi một policy khác ở action nghĩa là người gọi phải thỏa **cả hai** — triệu chứng là 403
ở đúng những endpoint mà bạn tin là đã mở. Chỉ đặt policy ở **cấp action**. Còn `/health` trả 401
thì load balancer đọc là "service chết" và rút container khỏi vòng phục vụ.

### 4.5 Tàn dư Minimal API (ADR-001)

```bash
grep -rnE '\b(TypedResults\.|Results\.(Ok|NoContent|NotFound|BadRequest|Problem|Created|File)\b|MapGroup\(|Map(Get|Post|Put|Delete|Patch)\()' \
  backend/src --include='*.cs'
```

Dự án dùng **Controllers**, không Minimal API (ADR-001). Trả kết quả bằng `ToActionResult(Result)`
của `ApiControllerBase`, không bằng `Results.Ok`.

**Hậu quả nếu sai:** endpoint viết kiểu Minimal API nằm ngoài `ApiControllerBase` nên **không đi
qua** chỗ quy đổi `Result` → HTTP tập trung. Nó sẽ trả `403` ở nơi lẽ ra `404`, `204` ở nơi lẽ ra
`200`, và không nhận `[Authorize]`/`[RequestSizeLimit]` gắn theo nhóm — mỗi cái là một luật khác
trong chính danh sách này bị vô hiệu.

Ngoại lệ hợp lệ, **không** tính vi phạm (endpoint routing chung, không phải Minimal API):
`MapControllers()`, `MapHealthChecks()`, `MapOpenApi()`, `MapScalarApiReference()`, và
`MapHangfireDashboard("/jobs").RequireAuthorization(Policies.Instructor)` — dashboard Hangfire là
middleware nên không có class để gắn `[Authorize]`.

### 4.6 `await next(ct)` trong behavior (L4)

```bash
grep -rnE 'await next\(\s*[^)\s]' backend/src --include='*.cs'

# Lambda RequestHandlerDelegate trong test phải là `_ =>`, không phải `() =>`
grep -rn -A2 'RequestHandlerDelegate' backend/tests --include='*.cs' | grep -E '\(\)\s*=>'

# Thứ tự đăng ký behavior: Logging → Validation → Transaction → Handler
grep -rn 'AddOpenBehavior\|IPipelineBehavior' \
  backend/src/TeachHub.Application/DependencyInjection.cs \
  backend/src/TeachHub.Infrastructure/DependencyInjection.cs
```

MediatR ghim **12.5.0** (Apache-2.0; từ v13 là RPL-1.5 — không nâng, xem `Directory.Packages.props`).
`RequestHandlerDelegate<TResponse>` ở bản này có một `CancellationToken` **tùy chọn**. Quy ước dự
án: **luôn viết `await next()`**.

**Hậu quả nếu sai:** `next(ct)` rải rác làm mỗi behavior một kiểu, và ngày phải rời MediatR (thay
bằng dispatcher tự viết sau `ICommand`/`IQuery`) thì mỗi chữ ký lệch là một chỗ phải sửa tay.
Trong test, khai lambda `() => ...` cho `RequestHandlerDelegate` là lỗi **CS1593** ngay lúc biên
dịch — đây là "đỏ sai lý do" ở mục 1, đừng nhầm với đỏ của hành vi.

**Validation phải TRƯỚC Transaction.** Sai thứ tự **không sinh lỗi nào**: mỗi request dữ liệu sai
vẫn mở một transaction rồi rollback, connection pool cạn dần lúc cao tải. `BehaviorPipelineTests`
(Phase 0 Task 0.6) khóa thứ tự này — đừng xóa nó vì nó "chỉ kiểm cấu hình".

### 4.7 `double` trong code liên quan điểm (L9)

```bash
# Điểm luôn decimal(5,2), kẹp [0,10]; trọng số cũng decimal — không có double/float ở đâu cả
grep -rliE 'grade|score|bonus|rubric|diem|point' backend/src --include='*.cs' \
  | while read -r f; do grep -nE '\b(double|float)\b' "$f" /dev/null; done
```

**Hậu quả nếu sai:** `double` là nhị phân dấu phẩy động — `0.1 + 0.2` ra `0.30000000000000004`.
Cộng ba cột điểm có trọng số rồi so với ngưỡng điểm sàn 4.0 sẽ có sinh viên rơi sai phía ngưỡng,
và điểm hiển thị `6.999999999999999`. Không có exception, không có test nào đỏ nếu test cũng
dùng `double`. Đây là lỗi làm sai **điểm thật của sinh viên thật**.

### 4.8 Trả 403 ở chỗ lẽ ra 404 (L8 · ADR-012)

```bash
grep -rn 'Status403Forbidden\|Forbid()\|StatusCode(403)' backend/src/TeachHub.Api --include='*.cs'
grep -rn 'Forbidden' backend/src/TeachHub.Application/Features --include='*.cs'
```

Mỗi dòng in ra phải giải trình được là **không** phải kiểm quyền sở hữu tài nguyên.
`ResultStatus.Forbidden` chỉ hợp lệ cho những ca không tiết lộ sự tồn tại — ví dụ sinh viên gọi
một endpoint dành riêng cho giảng viên trên tài nguyên của chính lớp mình.

**Hậu quả nếu sai:** `403` nghĩa là "tài nguyên này có thật, nhưng bạn không được vào" — nó **xác
nhận sự tồn tại**. Giảng viên B thử id của giảng viên A: nhận 403 là biết id đó có thật, và dò
dần ra được cấu trúc dữ liệu của người khác. Cùng lý do với việc đăng nhập sai trả "email hoặc
mật khẩu không đúng" thay vì "không có email này".

Kèm theo — mọi truy vấn phải lọc `instructor_id` **ngay trong câu truy vấn**, không nạp rồi lọc
trong bộ nhớ: lọc sau khi nạp cũng "chạy đúng" cho tới khi một dòng `.Where()` bị quên trong một
lần refactor. Lọc trong truy vấn thì quên là **không có** dữ liệu, chứ không phải là có **quá
nhiều** dữ liệu.

```bash
python3 - <<'PY'
import pathlib
for f in pathlib.Path('backend/src/TeachHub.Application/Features').rglob('*Handler.cs'):
    s = f.read_text(encoding='utf-8')
    if ('.Where(' in s or 'FirstOrDefaultAsync' in s or 'ToListAsync' in s) and 'InstructorId' not in s:
        print(f'{f}: handler truy vấn DB nhưng không nhắc tới InstructorId — kiểm lại bộ lọc quyền sở hữu')
PY
```

### 4.9 Kiểm nhanh bổ sung — mỗi cái một dòng

| Kiểm | Lệnh | Kỳ vọng |
|---|---|---|
| MediatR còn ghim 12.5.0 | `grep -n 'MediatR' backend/Directory.Packages.props` | đúng `12.5.0` |
| JSON cấu hình cả HAI chỗ (L5) | `grep -rn 'AddJsonOptions\|ConfigureHttpJsonOptions' backend/src/TeachHub.Api` | có **cả hai** |
| `Result<T>.IsSuccess` có attribute (L3) | `grep -n 'MemberNotNullWhen' backend/src/TeachHub.Application/Common/Result.cs` | có 1 dòng |
| `IFormFile` không đứng lẻ (L7) | `grep -rn 'IFormFile' backend/src/TeachHub.Api/Controllers` | chỉ trong lớp `[FromForm]` |
| Upload có đủ hai giới hạn (L7) | `grep -rn 'RequestSizeLimit\|RequestFormLimits' backend/src/TeachHub.Api` | đi **thành cặp** |
| Không có InMemory provider (L10) | `grep -rn 'UseInMemoryDatabase' backend` | không có dòng nào |

Mỗi dòng trong bảng là một lỗi đã xảy ra thật: thiếu `ConfigureHttpJsonOptions` thì runtime trả
`snake_case` nhưng `/openapi/v1.json` khai `camelCase`, client TS sinh ra gõ sai khóa **mà vẫn
biên dịch**. Thiếu `MemberNotNullWhen` thì mọi chỗ `kq.Value.X` sau `if (!kq.IsSuccess)` đứt build
vì `TreatWarningsAsErrors`. Thiếu `RequestFormLimits` thì `[RequestSizeLimit]` chặn được Kestrel
nhưng model binder của MVC vẫn từ chối file lớn bằng một lỗi khác hẳn.

---

## 5. Integration test: Testcontainers + Postgres 17 thật

**Không dùng InMemory provider.** InMemory không có CHECK constraint, không có unique index,
không có jsonb, không phân biệt `timestamptz` — đúng những thứ mà tầng dữ liệu của dự án này dựa
vào để đúng. Test qua InMemory là test một cơ sở dữ liệu không tồn tại.

**Chia sẻ một container cho cả assembly bằng `ICollectionFixture`.** Mỗi class một container thì
mỗi class trả thêm 3–5 giây khởi động Postgres; 20 class là hơn một phút chỉ để bật container.

```csharp
// tests/TeachHub.Api.Tests/TeachHubApiFactory.cs — dựng container MỘT lần cho cả assembly
[CollectionDefinition(Ten)]
public sealed class DatabaseCollection : ICollectionFixture<TeachHubApiFactory>
{
    public const string Ten = "teachhub-db";
}

// ✅ ĐÚNG — class test chỉ gắn nhãn, không tự dựng gì
[Collection(DatabaseCollection.Ten)]
public sealed class SubjectsTests(TeachHubApiFactory factory) { … }

// ❌ SAI — IClassFixture dựng container MỚI cho TỪNG class test
public sealed class SubjectsTests(TeachHubApiFactory factory) : IClassFixture<TeachHubApiFactory>
```

Đánh đổi phải biết: container dùng chung nghĩa là các class test **thấy dữ liệu của nhau**. Mỗi
test phải tự tạo dữ liệu riêng (id UUIDv7 nên không đụng nhau) và **không được assert trên tổng
số bản ghi toàn bảng** — `ShouldBe(3)` cho `GET /api/subjects` sẽ đỏ ngẫu nhiên khi một class
khác chạy trước. Assert trên chính dữ liệu vừa tạo.

Nếu Testcontainers không chạy (Docker chưa bật, hoặc dùng Colima): bật Docker Desktop hoặc đặt
`DOCKER_HOST` cho Colima. **Đừng** né bằng InMemory — đó là bỏ giá trị của test rồi báo xanh.

---

## 6. Ngân sách thời gian — unit test toàn bộ < 10 giây

```bash
time dotnet test backend/tests/TeachHub.Domain.Tests backend/tests/TeachHub.Application.Tests
```

Ngưỡng (Kiến trúc §5, bảng "Ngân sách chất lượng"): **toàn bộ unit test < 10 giây**. Domain test
thuần, không I/O, mỗi test dưới 1 ms.

Đây không phải chỉ tiêu làm đẹp. Vượt ngưỡng thì chuỗi nhân quả luôn diễn ra: chạy test sau mỗi
thay đổi trở nên khó chịu → chạy thưa dần → chỉ chạy trước khi commit → ngừng hẳn và chỉ tin CI —
và lúc đó **vòng lặp TDD ở mục 1 đã chết**, vì bước 2 "thấy đỏ" cần phản hồi tính bằng giây.
Ngân sách này bảo vệ chính cái vòng lặp đó. Vượt ngưỡng thì nguyên nhân gần như luôn là một
trong ba:

| Nguyên nhân | Dấu hiệu | Xử lý |
|---|---|---|
| Test I/O lọt vào project unit test | Có `DbContext`, `HttpClient`, đọc file | Chuyển sang `Api.Tests` |
| `Thread.Sleep` / chờ thời gian thật | Test lẻ mất vài giây | Dùng `FakeTimeProvider` + `Advance()` — mọi logic thời gian đã đi qua `TimeProvider` |
| Vòng lặp sinh dữ liệu quá lớn | Test tạo hàng trăm nghìn bản ghi | Giảm số lượng; `Hai_entity_khong_bao_gio_trung_id` với 10.000 id là mức trần hợp lý |

Integration test (`Api.Tests`) không nằm trong ngân sách 10 giây, nhưng nếu vượt **60 giây** thì
kiểm ngay `ICollectionFixture` ở mục 5 — đó là triệu chứng kinh điển của mỗi class một container.

---

## 7. Mẫu báo cáo sau khi verify

Ngắn, kiểm chứng được; gửi lỗi **thẳng cho agent gây ra lỗi**, đồng thời báo `plan-executor`:

```
Module: Task 4b.2 — GradeColumn + configuration
Build:  0 Warning(s) 0 Error(s) ✅
Test:   Domain 42/42 (1.8s) · Application 63/63 (4.1s) · Api 28/28 (34s) ✅
Luật:   4.3 ĐỎ — GradeColumnConfiguration.cs:41 OwnsOne(… Aggregation) có ToJson()
        nhưng thiếu .Property(a => a.Kind).HasConversion<string>()
        → chuyển backend-engineer
DoD:    mục 7 CHƯA đạt — /api/grade-columns/{id} chưa có dòng trong
        CrossInstructorIsolationTests. Task giữ nguyên trạng thái chưa tick.
```

Không tick "tạm" rồi sửa sau. Một task đã tick là một task không ai quay lại nữa.

---

## 8. Checklist đóng task

- [ ] Test viết trước, đã **thấy đỏ**, và **nêu được thông báo đỏ** đó (mục 1)
- [ ] `dotnet build backend/TeachHub.sln` → `0 Warning(s) 0 Error(s)` (mục 3)
- [ ] `dotnet test backend/TeachHub.sln` → toàn bộ xanh, không chỉ test vừa viết
- [ ] Tám khối script mục 4.1–4.8 chạy hết, không in ra dòng nào — hoặc mỗi dòng in ra đều đã giải trình
- [ ] Bảng 4.9 chạy hết, đúng kỳ vọng
- [ ] Chạm schema → có migration sinh bằng `dotnet ef migrations add`, đã đọc file sinh ra, apply được trên DB trống **và** DB có dữ liệu
- [ ] Chạm API → `/openapi/v1.json` trả 200, `pnpm gen:api` chạy được, `pnpm build` xanh
- [ ] Tài nguyên mới của giảng viên → đã thêm dòng vào `CrossInstructorIsolationTests` + seed trong `IsolationFixture`
- [ ] `time dotnet test` hai project unit → **< 10 giây**
- [ ] Đã commit theo Conventional Commits
