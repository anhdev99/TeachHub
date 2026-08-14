---
name: teachhub-backend
description: Cách viết backend .NET 10 của TeachHub — Clean Architecture 4 tầng, MediatR 12.5 CQRS, kiểu Result, hai mô hình lỗi, controller mỏng, JSON snake_case, record command, upload multipart, phân quyền 404-không-403. Dùng khi tạo hoặc sửa file dưới backend/src/TeachHub.Application hoặc backend/src/TeachHub.Api (KHÔNG dùng cho entity/EF configuration/migration → teachhub-data; KHÔNG dùng cho .sln/.csproj/lệnh sinh khung → teachhub-scaffold; KHÔNG dùng cho chạy build/test → teachhub-verify) (Command, Query, Handler, Validator, Controller, Behavior, DbContext, Program.cs), khi thêm endpoint mới hay endpoint upload, khi kiểm quyền sở hữu tài nguyên của giảng viên; và cả khi được yêu cầu "chạy lại", "sửa", "bổ sung", "cập nhật", "refactor", "review" phần backend đã viết. Cũng dùng khi build đỏ vì CS8602/CS1593, khi /openapi/v1.json trả 500, khi pnpm gen:api chết, hoặc khi API trả camelCase thay vì snake_case.
---

# Viết backend TeachHub (.NET 10)

Mọi luật dưới đây từng là một lỗi thật, đã trả giá bằng một phiên build đỏ hoặc một hợp đồng
API hỏng âm thầm. Mỗi luật kèm **hậu quả cụ thể** — đọc hậu quả trước khi quyết định bỏ qua.

**Trước hết, luật số một (CLAUDE.md):** khung dự án do **công cụ sinh**. Không gõ tay `.sln`,
`.csproj`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`,
`<PackageReference>`, file migration EF Core. Chạy `dotnet new …`, `dotnet add reference|package`,
`dotnet ef migrations add`. Không chạy được lệnh → **báo lại**, không đoán rồi gõ tay thay thế.

Nguồn chân lý kỹ thuật: `docs/architecture/00-tech-stack-decisions.html` (13 ADR + quy ước §5).

## 1. Cấu trúc 4 tầng — file nào nằm ở đâu

```
backend/src/
├─ TeachHub.Domain/            entity, enum, luật thuần (GradebookCalculator, BonusLedger).
│  └─ Common/Entity.cs         Id = Guid.CreateVersion7(). KHÔNG tham chiếu ai.
├─ TeachHub.Application/       use case. → Domain
│  ├─ Common/Messaging/        ICommand, IQuery, IBaseCommand, ICommandHandler, IQueryHandler
│  ├─ Common/Behaviors/        LoggingBehavior, ValidationBehavior
│  ├─ Common/                  Result.cs, ICurrentUser, IAppDbContext, IFileStorage (port)
│  ├─ Features/<Nhóm>/Commands/<TênUseCase>/   Command + Validator + Handler + DTO
│  ├─ Features/<Nhóm>/Queries/<TênUseCase>/    Query + Handler + DTO
│  └─ DependencyInjection.cs   AddMediatR + AddValidatorsFromAssembly
├─ TeachHub.Infrastructure/    EF Core, storage, Hangfire, Excel. → Application
│  ├─ Persistence/             AppDbContext, EntityTypeConfiguration, Migrations
│  └─ Behaviors/               TransactionBehavior.cs  ← ở ĐÂY, không phải Application
└─ TeachHub.Api/               → tất cả
   ├─ Controllers/             ApiControllerBase + 1 controller / nhóm endpoint SRS §7
   └─ Program.cs               DI, JSON, rate limiter, pipeline HTTP
```

**Một use case = một thư mục.** `Features/Subjects/Commands/CreateSubject/` chứa
`CreateSubjectCommand.cs` (command + validator + handler có thể chung file), `SubjectDto.cs`.
Sửa một chức năng = mở đúng một thư mục. Rải Command ở một nơi và Handler ở nơi khác thì
mỗi lần sửa phải mở bốn thư mục và sẽ có file bị bỏ quên khi xóa use case.

Đặt tên: `<Động từ><Danh từ>Command` / `Get<Danh từ>Query`, `<Tên>Handler`, `<Tên>Validator`.

**Ghi → `ICommand<T>`, đọc → `IQuery<T>`.** Không phải hình thức: `TransactionBehavior` kiểm
`request is not IBaseCommand` để quyết định có mở transaction hay không (ADR-010). Khai nhầm
query thành command là giữ connection vô ích khi 300 sinh viên cùng mở bảng điểm; khai nhầm
command thành query là **mất tính nguyên tử** — ghi dở dang mà không có lỗi nào báo.

## 2. Ranh giới tầng — thứ ArchitectureTests canh giữ

Chiều phụ thuộc `Domain ← Application ← Infrastructure ← Api`, **không bao giờ đảo**.

`TeachHub.Domain` **cấm** tham chiếu assembly bắt đầu bằng: `Microsoft.EntityFrameworkCore`,
`Npgsql`, `Hangfire`, `MediatR`, `FluentValidation`, `Microsoft.AspNetCore`, `AWSSDK`.

`ArchitectureTests.Domain_khong_duoc_phu_thuoc_vao_ha_tang` (Domain.Tests) đọc
`GetReferencedAssemblies()` và đỏ ngay nếu vi phạm. **MediatR và FluentValidation nằm trong
danh sách cấm cùng lý do với EF Core:** `ICommand`/`IQuery` là hợp đồng của tầng Application.
Không chặn thì cám dỗ "cho Domain phát `INotification` làm domain event" sẽ thắng, và luật tính
điểm mất khả năng test trong mili-giây — đúng thứ đắt nhất mà kiến trúc này mua được.
Ngoại lệ đã cân nhắc và chấp nhận: Domain tham chiếu `Microsoft.Extensions.Identity.Stores`
(chỉ chứa `IdentityUser<TKey>` là POCO thuần, không kéo hạ tầng theo).

### `TransactionBehavior` nằm ở Infrastructure

Vì nó cần `AppDbContext.Database.BeginTransactionAsync`. Đặt ở Application buộc Application
tham chiếu EF Core — phá đúng ranh giới mà kiến trúc này sinh ra để giữ. Behavior của MediatR
**không** bắt buộc cùng tầng với handler: nó chỉ là một `IPipelineBehavior<,>` trong DI
container, đăng ký ở đâu cũng chạy. Cách né duy nhất còn lại là dựng `IUnitOfWork` giả trong
Application chỉ để giữ hình thức — thêm một lớp trừu tượng không ai dùng ngoài chính nó,
đúng thứ ADR-002 đã từ chối.

### Validator là hàm thuần, không chạm I/O

Ranh giới chia theo **luật đó có phải đọc database không**, chứ không theo cảm giác "nghiệp vụ
hay không nghiệp vụ":

| Luật | Ở đâu | Ví dụ |
|---|---|---|
| Thuần hình dạng trên payload | `Validator` | tên rỗng, điểm âm, **tổng trọng số các cột = 100%** (cả bộ cột gửi trong một request nên tự đủ dữ liệu) |
| Phụ thuộc dữ liệu đang có | `Handler`, trả `Result` | cột nguồn phải thuộc lớp của sinh viên đó, không cho xóa cột đã có điểm (409), áp mẫu chế độ `replace` khi đã có điểm (409) |

Cho validator đọc DB là mở một round-trip **trước** `TransactionBehavior`, ngoài transaction,
và biến test validator thành bài dựng database.

## 3. Kiểu `Result` — chốt ở Phase 0, không sửa lại

```csharp
public enum ResultStatus { Ok, Invalid, NotFound, Conflict, Forbidden, Unauthorized, PayloadTooLarge }
```

Bảy trạng thái là **bản chốt**. `Invalid` (→400) và `PayloadTooLarge` (→413) bắt buộc phải có
từ Phase 0 dù mãi Phase 3 mới dùng: thiếu chúng thì Phase 3 và 4b không biên dịch được.

```csharp
public sealed class Result<T>
{
    private Result(ResultStatus status, T? value, string? error) =>
        (Status, Value, Error) = (status, value, error);

    public ResultStatus Status { get; }
    public T? Value { get; }
    public string? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]   // ← BẮT BUỘC
    public bool IsSuccess => Status == ResultStatus.Ok;

    public static Result<T> Ok(T value)                        => new(ResultStatus.Ok, value, null);
    public static Result<T> Invalid(string error)              => new(ResultStatus.Invalid, default, error);
    public static Result<T> NotFound(string? error = null)     => new(ResultStatus.NotFound, default, error);
    public static Result<T> Conflict(string error)             => new(ResultStatus.Conflict, default, error);
    public static Result<T> Forbidden(string? error = null)    => new(ResultStatus.Forbidden, default, error);
    public static Result<T> Unauthorized(string? error = null) => new(ResultStatus.Unauthorized, default, error);
    public static Result<T> PayloadTooLarge(string error)      => new(ResultStatus.PayloadTooLarge, default, error);
}
```

Bản không generic (`Result`) có đúng bảy factory đó nhưng bỏ `Value`, và `Ok()` không tham số.

### `[MemberNotNullWhen]` không phải trang trí

`Value` khai là `T?`. Thiếu attribute này thì trình biên dịch **không biết** `IsSuccess == true`
kéo theo `Value != null`, nên mọi chỗ viết theo khuôn chuẩn đều đứt build với **CS8602**
(`TreatWarningsAsErrors` đang bật).

```csharp
// ✅ ĐÚNG — có [MemberNotNullWhen(true, nameof(Value))]
var kq = await sender.Send(new GetFileQuery(id), ct);
if (!kq.IsSuccess) return ToActionResult(kq);
return File(kq.Value.Content, kq.Value.ContentType);   // Value đã được coi là non-null

// ❌ SAI — thiếu attribute
public bool IsSuccess => Status == ResultStatus.Ok;
// → CS8602 "Dereference of a possibly null reference" tại kq.Value.Content
// → build đỏ, và cách "sửa" quen tay là rắc `!` khắp nơi, tức là vứt bỏ nullable analysis
```

### `ToActionResult` — thành công LUÔN là 200

Overload **không generic** phải map `ResultStatus.Ok => Ok()` (200), **KHÔNG phải
`NoContent()`** (204). Trước đây chỗ này trả `NoContent()` trong khi mọi `[ProducesResponseType]`
và mọi test Phase 1–4b đều khẳng định 200 → hợp đồng OpenAPI sai và một loạt test đỏ. Một mã
thành công duy nhất cho cả hai overload là quy tắc rẻ nhất để không lệch lại. (Phần văn xuôi
ADR-011 còn nhắc `NoContent()`; bản chốt là Phase 0 Task 0.7.)

**Mọi nhánh lỗi đi qua `Problem(statusCode:, title:, detail: result.Error)`**, cố ý không dùng
`NotFound()` / `Forbid()`: `Problem` luôn trả `application/problem+json` theo RFC 9457 kèm
`detail` đọc được, còn `Forbid()` trả 403 thân rỗng — frontend không có gì để hiển thị.

## 4. Hai mô hình lỗi — ném hay trả, không được lẫn

| Loại | Cơ chế | Đường đi |
|---|---|---|
| **Đầu vào không hợp lệ** (email sai định dạng, điểm âm, tên rỗng) | ném `ValidationException` | FluentValidation → `ValidationBehavior` → `IExceptionHandler` toàn cục → 400 `ValidationProblemDetails` |
| **Kết quả nghiệp vụ mong đợi** (không tìm thấy, xung đột trạng thái, không có quyền) | trả `Result` | handler → controller → `ToActionResult` |

**Vì sao chia thế.** Lỗi đầu vào **không bao giờ** cần xử lý riêng theo từng use case — không
có chỗ nào trong hệ thống muốn "bắt lỗi email sai định dạng rồi làm việc khác". Đã vậy thì gom
về một chỗ là đúng; bắt nó ở từng handler chỉ tạo ra 200 khối `if` giống hệt nhau.
Ngược lại, "publish một version đã publish" là **một phần hợp đồng** của use case, phải hiện ra
trong chữ ký hàm để người gọi buộc phải xử lý, không được giấu vào exception rồi hy vọng ai đó
nhớ bắt.

**Hậu quả nếu trộn lẫn:** khi cùng một loại lỗi lúc thì ném lúc thì trả, không ai còn biết khi
nào phải `try/catch`, khi nào phải kiểm `Result`. Kết cục quen thuộc là làm cả hai cho chắc —
`try/catch` bọc quanh một lời gọi vốn đã trả `Result`, rồi bên trong quên kiểm `IsSuccess`.
Lỗi "không tìm thấy" âm thầm đi tiếp như thành công, và thứ nổ ra là một
`NullReferenceException` cách đó ba tầng gọi hàm.
Ranh giới thực dụng: **đọc chữ ký hàm mà không đoán được nó hỏng kiểu gì thì đó phải là `Result`.**

**KHÔNG tạo** `NotFoundException` / `ConflictException` / `ForbiddenException`, và không tạo thư
mục `Common/Exceptions/` rỗng — đó chỉ là lời mời quay lại ném exception cho nhánh nghiệp vụ.
Khi gặp ca *thực sự bất thường* đầu tiên (dữ liệu vi phạm bất biến của hệ thống, tức là bug chứ
không phải người dùng nhập sai) mới thêm lớp exception đúng cho ca đó + một dòng trong
`IExceptionHandler`.

## 5. MediatR 12.5.0

### ⛔ CẢNH BÁO GIẤY PHÉP — ghim đúng `12.5.0`, tuyệt đối không nâng

12.5.0 là bản **Apache-2.0 cuối cùng**. Từ v13 MediatR chuyển sang **RPL-1.5** hoặc giấy phép
thương mại của Lucky Penny Software. RPL-1.5 buộc **công khai mã nguồn phần mềm đã triển khai**,
kể cả khi chỉ dùng nội bộ. Nâng version là đưa toàn bộ sổ điểm và bài nộp của sinh viên vào
diện phải mở mã. Nếu công cụ/agent nào đề xuất `dotnet add package MediatR` không kèm
`--version 12.5.0`, hoặc Dependabot mở PR nâng MediatR — **từ chối và báo lại**.
12.5.0 target `netstandard2.0`/`net6.0` nên chạy tốt trên .NET 10.

### `await next()` — quy ước của dự án

Trong 12.5.0, `RequestHandlerDelegate<TResponse>` có **một tham số `CancellationToken` tùy chọn**
(`delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken t = default)`).
Tham số này thêm từ **12.4.0**; các bản 12.0–12.3 không có tham số nào.

```csharp
// ✅ ĐÚNG — quy ước dự án, chạy trên mọi bản 12.x, vẫn chuyển token gốc xuống handler
var response = await next();

// ⚠️ SAI QUY ƯỚC — biên dịch được trên 12.5.0 nhưng gãy nếu ai đó hạ về 12.4,
//    và làm code lệch với mọi behavior khác trong repo
var response = await next(ct);
```

Cạm bẫy thật nằm ở chỗ **tự khai lambda trong test**:

```csharp
// ✅ ĐÚNG
RequestHandlerDelegate<Unit> next = _ => Task.FromResult(Unit.Value);

// ❌ SAI — CS1593: Delegate 'RequestHandlerDelegate<Unit>' does not take 0 arguments
RequestHandlerDelegate<Unit> next = () => Task.FromResult(Unit.Value);
```

### Thứ tự behavior: Logging → Validation → Transaction → Handler

```csharp
// TeachHub.Application/DependencyInjection.cs
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));     // 1
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));  // 2
});
services.AddValidatorsFromAssembly(assembly);

// TeachHub.Infrastructure/DependencyInjection.cs — đăng ký SAU hai cái trên
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));  // 3
```

Thứ tự đăng ký **chính là** thứ tự chạy từ ngoài vào trong.
**Validation phải TRƯỚC Transaction.** Đảo lại thì mỗi request có dữ liệu sai vẫn mở một
transaction trên Postgres, chạy validator, ném exception, rồi rollback một transaction chưa làm
gì cả. Với form nhập điểm mà giảng viên gõ sai liên tục, đó là hàng loạt connection bị giữ vô
ích chỉ để rollback — và **không test nào bắt được**, vì kết quả HTTP vẫn đúng 400.

`TransactionBehavior` bỏ qua query (`if (request is not IBaseCommand) return await next();`) và
bỏ qua transaction lồng (`if (db.Database.CurrentTransaction is not null) return await next();`).

Hệ quả: **handler không bao giờ tự `BeginTransaction`.** `SubjectVersionCloner`, import Excel
danh sách sinh viên, sao chép `GradeColumn` lúc mở lớp, chấm hàng loạt — tất cả đã được bọc sẵn.
Tự mở transaction trong handler là mở transaction lồng và làm `CommitAsync` mất nghĩa.

Behavior sai một chỗ là sai toàn hệ thống → **mỗi behavior phải có test riêng**.

## 6. Controller mỏng

Controller chỉ làm bốn việc: định tuyến, lấy `CurrentUserId` từ claim, `sender.Send(...)`,
quy đổi `Result` bằng `ToActionResult`. **Không `if` nghiệp vụ, không đụng `DbContext`,
không `try/catch`.** Một dòng logic lọt vào controller là một dòng không test được nếu không
dựng HTTP. Review thấy vi phạm là trả lại, không du di.

```csharp
[Route("api/subjects")]
[Tags("Subjects")]
public sealed class SubjectsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<IReadOnlyList<SubjectDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => ToActionResult(await sender.Send(new GetSubjectsQuery { InstructorId = CurrentUserId }, ct));

    [HttpPost]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<SubjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateSubjectCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));
}
```

`ApiControllerBase` đã mang `[ApiController]` + `[Produces("application/json")]` và cung cấp
`CurrentUserId` (ném `InvalidOperationException` nếu thiếu claim — fallback policy bảo đảm mọi
request tới được đây đều đã xác thực, nên thiếu claim là lỗi cấu hình của ta, phải nổ ngay chứ
đừng âm thầm trả `Guid.Empty` rồi truy vấn nhầm dữ liệu người khác).

### Policy chỉ đặt ở **cấp action**

`[Authorize]` ở class và ở action **cộng dồn, không ghi đè**.

```csharp
// ❌ SAI — policy ở cấp class
[Route("api/assignments")]
[Authorize(Policy = Policies.Instructor)]        // ← áp cho MỌI action bên dưới
public sealed class AssignmentsController(ISender sender) : ApiControllerBase
{
    [HttpGet("{id:guid}/my-submission")]
    [Authorize(Policy = Policies.Student)]       // ← CỘNG DỒN, không thay thế
    public async Task<IActionResult> MySubmission(Guid id, CancellationToken ct) => ...
}
// → yêu cầu tài khoản có CẢ vai Instructor LẪN vai Student. Không tài khoản nào có cả hai,
//   nên KHÔNG AI vào được. Chính chủ bài nộp nhận 403 khi mở bài của mình, log không nói vì sao.

// ✅ ĐÚNG — class để trống policy, gắn xuống từng action
[Route("api/assignments")]
public sealed class AssignmentsController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.Instructor)]
    public async Task<IActionResult> Create(...) => ...

    [HttpGet("{id:guid}/my-submission")]
    [Authorize(Policy = Policies.Student)]
    public async Task<IActionResult> MySubmission(...) => ...
}
```

Chồng policy chỉ an toàn khi policy hẹp nằm trọn trong policy rộng — dự án **không có cặp nào
như vậy**, nên quy tắc thực tế: **chỉ đặt policy ở cấp action**.

**`SetFallbackPolicy(RequireAuthenticatedUser)` đang bật** → endpoint nào không khai gì vẫn đòi
đăng nhập. Muốn mở công khai (login, register, bootstrap, kiểm tra token lời mời, `/health`,
`/openapi`) phải gắn `[AllowAnonymous]` / `.AllowAnonymous()` tường minh. Quên gắn cho `/health`
thì load balancer coi service là chết.

**Chỉ có hai policy: `Policies.Instructor` và `Policies.Student`.**
`Policies.InstructorOrTa` **không tồn tại** — vai trò trợ giảng tắt hoàn toàn ở MVP (ADR-013).
Hằng số `Roles.TeachingAssistant` vẫn giữ để sau này bật lại không phải migrate, nhưng chưa có
policy nào cấp quyền cho nó: không có bảng gắn trợ giảng vào một lớp cụ thể, trong khi mọi kiểm
quyền viết dạng `subject.InstructorId == currentUserId` — nên hoặc trợ giảng không làm được gì,
hoặc làm được ở **mọi** lớp của **mọi** giảng viên. Có test canh gác đỏ ngay nếu ai đó thêm lại.

## 7. JSON phải cấu hình ở **cả hai** chỗ

Hai hàm cùng nhận một `JsonSerializerOptions` nên nhìn giống hệt nhau, nhưng chúng đọc **hai
`IOptions` khác nhau và không kế thừa nhau**.

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(o =>                                  // → phản hồi THẬT lúc chạy
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.ConfigureHttpJsonOptions(o =>            // → SCHEMA trong /openapi/v1.json
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});
```

**Hai kiểu hỏng, cả hai đều không có lỗi nào cả** — build xanh, unit test xanh, API trả 200:

- **Thiếu `AddJsonOptions`:** API trả `accessToken` thay vì `access_token`. Frontend đọc
  `access_token` nhận `undefined`, cookie rỗng, mọi request sau đó 401 — và bạn đi tìm lỗi ở
  tầng auth trong nhiều giờ.
- **Thiếu `ConfigureHttpJsonOptions`:** runtime đúng `snake_case` nhưng tài liệu OpenAPI khai
  `camelCase`. `pnpm gen:api` sinh client TypeScript từ chính file đó, nên client gõ
  `res.accessToken` trong khi server trả `access_token` — **TypeScript vẫn biên dịch**, và bạn
  nhận `undefined` lúc chạy. Kiểu hỏng khó chịu hơn hẳn vì backend và frontend đều "đúng" khi
  xét riêng.

Hai test khoá chuyện này: `AuthTests` đọc khóa `access_token` bằng `JsonElement`, và
`HostSmokeTests` assert `/openapi/v1.json` có chứa chuỗi `"access_token"`. Thêm endpoint mới
mà đổi cấu hình JSON thì phải chạy lại cả hai.

## 8. Record command — không `= default` cho kiểu giá trị

Trường **server tự điền** (`InstructorId`, `SubjectId` lấy từ route) phải là property
`init`-only, không phải tham số positional có giá trị mặc định.

```csharp
// ✅ ĐÚNG
public sealed record CloneSubjectVersionCommand(
    Guid? ClonedFromVersionId,
    string VersionLabel) : ICommand<Result<SubjectVersionDto>>
{
    public Guid SubjectId    { get; init; }
    public Guid InstructorId { get; init; }
}
// controller: sender.Send(command with { SubjectId = id, InstructorId = CurrentUserId }, ct)

// ❌ SAI — tham số positional kiểu giá trị mang `= default`
public sealed record CloneSubjectVersionCommand(
    Guid? ClonedFromVersionId,
    string VersionLabel,
    Guid SubjectId = default,          // ← nổ
    Guid InstructorId = default)       // ← nổ
    : ICommand<Result<SubjectVersionDto>>;
```

**Hậu quả:** `JsonSchemaExporter` của `Microsoft.AspNetCore.OpenApi` ném `JsonException` khi
sinh schema → `GET /openapi/v1.json` trả **500** → `pnpm gen:api` chết → frontend không sinh
lại được client, và DoD mục 5 ("chạm API thì OpenAPI doc sinh lại được") không đạt. Build vẫn
xanh, nên lỗi chỉ lộ khi ai đó mở `/scalar/v1` hoặc chạy sinh client.
`Guid? X = null` thì không sao, nhưng **init-only property là khuôn thống nhất** cho mọi command
có trường server tự điền — đừng dùng hai khuôn.

## 9. Upload / form binding

```csharp
/// Bọc IFormFile trong một lớp [FromForm] — KHÔNG khai IFormFile làm tham số rời.
public sealed class UploadSubmissionForm
{
    [FromForm(Name = "file")]
    public required IFormFile File { get; init; }

    [FromForm(Name = "slot_key")]              // model binder form KHÔNG theo JsonNamingPolicy
    public required string SlotKey { get; init; }
}

[HttpPost("{id:guid}/files")]
[Authorize(Policy = Policies.Student)]
[Consumes("multipart/form-data")]
[EnableRateLimiting("upload")]
[RequestSizeLimit(300_000_000)]                                  // hạn mức Kestrel
[RequestFormLimits(MultipartBodyLengthLimit = 300_000_000)]      // hạn mức multipart của MVC
[IgnoreAntiforgeryToken]
[ProducesResponseType<SubmissionFileDto>(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
public async Task<IActionResult> Upload(Guid id, [FromForm] UploadSubmissionForm form, CancellationToken ct)
{
    await using var stream = form.File.OpenReadStream();
    return ToActionResult(await sender.Send(new UploadSubmissionFileCommand
    {
        SubmissionId = id, StudentId = CurrentUserId, SlotKey = form.SlotKey,
        FileName = form.File.FileName, Length = form.File.Length, Content = stream,
    }, ct));
}
```

**Vì sao phải bọc.** Đo trên host .NET 10 thật: với tham số rời `[FromForm] IFormFile file`
(kể cả khi đã có `[Consumes]`), `/openapi/v1.json` sinh content-type
`application/x-www-form-urlencoded` và schema liệt kê các thuộc tính **nội bộ** của `IFormFile`
(`ContentType`, `Headers`, `Length`, `Name`, `FileName`…) — **không có field nhị phân nào**.
Hậu quả: client TypeScript sinh tự động đòi truyền sáu chuỗi vô nghĩa thay vì một file, và
**không endpoint upload nào gọi được từ client sinh ra** — trong khi build xanh và integration
test dùng `MultipartFormDataContent` viết tay cũng xanh. Chỉ người dùng cuối mới phát hiện.
→ Mỗi endpoint upload phải có test đọc `/openapi/v1.json` bằng `JsonElement` (đừng
`Contains("\"format\":\"binary\"")` — tài liệu in đẹp có khoảng trắng, chuỗi ghép tay không khớp).

**`[FromForm(Name = "snake_case")]` bắt buộc trên từng property.** Model binder của form không
đọc `JsonNamingPolicy` — thiếu `Name` thì trên dây là `SlotKey`/`File`, lệch hợp đồng snake_case.
Thiếu hẳn `[FromForm]` thì binder đi tìm trong query string và luôn nhận `null` → 400 với
thông điệp vô nghĩa dù client gửi file đúng.

**Phải có CẢ HAI attribute giới hạn dung lượng.** `[RequestSizeLimit]` nâng
`MaxRequestBodySize` của Kestrel (mặc định 30 MB); `[RequestFormLimits(MultipartBodyLengthLimit)]`
nâng hạn mức riêng của model binder multipart trong MVC (mặc định 128 MB). Nâng mỗi cái đầu thì
file 150 MB qua được Kestrel rồi chết ở binder với `InvalidDataException` — **500 khó hiểu chứ
không phải 413 tử tế**.

**`[EnableRateLimiting("upload")]`** — bộ giới hạn tên `"upload"` đã đăng ký từ Phase 0
(`AddFixedWindowLimiter`, 30 request/phút, `QueueLimit = 0`) cùng `app.UseRateLimiter()`.
Attribute trỏ tới bộ chưa đăng ký là **metadata trơ**: không chặn gì và cũng không báo lỗi gì.

**`IFormFile` chỉ được xuất hiện trong `Controllers/`.** Command nhận `Stream` + tên file + độ
dài. Đưa `IFormFile` xuống Application kéo `Microsoft.AspNetCore.Http` vào tầng đó và biến mọi
test handler thành bài dựng HTTP giả; với `MemoryStream` thì test chỉ cần một fixture.

## 10. Phân quyền dữ liệu: 404 không 403, lọc trong câu truy vấn

Nhiều giảng viên **độc lập** — không cộng tác, không đồng sở hữu, không chia sẻ thư viện
(ADR-012). Mọi tài nguyên gắn `instructor_id` trực tiếp hoặc gián tiếp qua
`Subject`/`CourseOffering`.

**Tài nguyên không thuộc người gọi → trả 404, KHÔNG BAO GIỜ 403.** Trả 403 là xác nhận rằng id
đó *có tồn tại* — một giảng viên dò id tuần tự sẽ đếm được số môn học và số lớp của đồng nghiệp,
và biết chính xác id nào đáng để tấn công tiếp. 404 không tiết lộ gì.
(`ResultStatus.Forbidden` vẫn tồn tại cho ca "đúng chủ nhưng sai trạng thái/vai trò", không dùng
cho ca "không phải của bạn".)

**Lọc `instructor_id` ngay trong câu truy vấn, không lọc sau khi nạp.**

```csharp
// ✅ ĐÚNG — điều kiện sở hữu nằm trong WHERE, không tìm thấy là 404
var subject = await db.Subjects.SingleOrDefaultAsync(
    s => s.Id == request.SubjectId && s.InstructorId == request.InstructorId, ct);
if (subject is null) return Result<SubjectDto>.NotFound();

// ❌ SAI — nạp rồi mới kiểm
var subject = await db.Subjects.SingleOrDefaultAsync(s => s.Id == request.SubjectId, ct);
if (subject is null) return Result<SubjectDto>.NotFound();
if (subject.InstructorId != request.InstructorId) return Result<SubjectDto>.Forbidden();
// → vừa lộ sự tồn tại của id, vừa dễ quên nhánh thứ hai khi copy sang handler kế tiếp;
//   với truy vấn danh sách thì còn kéo cả dữ liệu lớp người khác vào bộ nhớ tiến trình.
```

Mỗi handler tự lọc, nên **bắt buộc** có ma trận `CrossInstructorIsolationTests`. **Phase nào
thêm một loại tài nguyên mới thuộc về giảng viên thì phải thêm dòng vào ma trận đó** — thiếu
dòng thì task chưa được tick (Definition of Done mục 7). Một endpoint quên lọc là lộ điểm số và
bài nộp của lớp người khác, mà loại lỗi này **không có triệu chứng nào**.

## 11. Trước khi báo "xong"

- [ ] Test viết **trước**, đã thấy **đỏ vì đúng lý do**, rồi mới có code làm nó xanh (TDD, không ngoại lệ cho code tính điểm).
- [ ] `dotnet build` → **0 Warning(s), 0 Error(s)** (`TreatWarningsAsErrors` đang bật).
- [ ] Toàn bộ test suite xanh, không chỉ test vừa viết. Unit test suite **< 10 giây**.
- [ ] Domain test thuần, không I/O. Integration test dùng **Testcontainers + Postgres 17 thật**, không dùng InMemory provider.
- [ ] Chạm schema → có migration sinh bằng `dotnet ef migrations add`, đã **đọc file sinh ra** xem có `DROP` ngoài ý muốn, và apply thành công trên DB trống lẫn DB có dữ liệu.
- [ ] Chạm API → `/openapi/v1.json` sinh lại được (không 500), `pnpm gen:api` chạy được, frontend vẫn build.
- [ ] Thêm tài nguyên mới của giảng viên → đã thêm dòng vào `CrossInstructorIsolationTests`.
- [ ] Commit theo Conventional Commits (message tiếng Anh).

**Ngôn ngữ:** tài liệu, chú thích code, tên test, thông báo lỗi trả cho người dùng → **tiếng Việt**.
Tên kiểu/biến/hàm và commit message → tiếng Anh.
