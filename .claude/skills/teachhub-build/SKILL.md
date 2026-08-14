---
name: teachhub-build
description: Điều phối thi công TeachHub theo plan từng phase — dựng nhóm agent chuyên trách (plan-executor, domain-engineer, backend-engineer, frontend-engineer, build-verifier), phân công task đúng thứ tự phụ thuộc ghi trong plan, chạy vòng lặp TDD với cổng build sau mỗi module, rồi chốt phase theo Definition of Done và kịch bản nghiệm thu thủ công. Dùng khi được yêu cầu "làm Phase 0", "làm Phase 1/2/3/4a/4b", "thi công plan", "triển khai plan", "bắt đầu code", "khởi động phase", "chạy tiếp phase", "làm tiếp", "tiếp tục phần dở", cũng như các yêu cầu hậu kỳ "chạy lại", "chạy lại từ đầu", "sửa lại phần X của phase", "bổ sung task còn thiếu", "cập nhật lại", "hoàn thiện nốt" — hoặc bất cứ khi nào cần biết ai làm gì theo thứ tự nào trong repo TeachHub.
---

# TeachHub — Điều phối thi công

Skill này trả lời **"ai làm gì theo thứ tự nào"**. Cách làm từng việc nằm ở skill chuyên
môn và ở prompt của từng agent — đừng nhét hướng dẫn kỹ thuật chi tiết vào đây.

> **Phân biệt hai chữ "phase" — nhầm là điều phối sai hoàn toàn.**
> *Phase dự án* = 0, 1, 2, 3, 4a, 4b, 5–9 (đơn vị giao hàng, mỗi phase một file plan).
> *Giai đoạn* = 5 bước điều phối của skill này (0→4) chạy **bên trong mỗi** phase dự án.
> Người dùng nói "làm Phase 0" là nói phase dự án Nền móng, không phải Giai đoạn 0 dưới đây.

---

## Giai đoạn 0 — Xác định ngữ cảnh

**Không được gọi agent nào trước khi xong giai đoạn này.** Bỏ qua nó là nguồn của lỗi
đắt nhất trong dự án: dựng lại solution đè lên cái đã có, hoặc gõ tay `.csproj` vì tưởng
repo còn trống — vi phạm thẳng nguyên tắc số một ở `CLAUDE.md`.

### Bước 0.1 — Đọc trạng thái sản phẩm lần chạy trước

```bash
ls -la .claude/_workspace/ 2>/dev/null || echo "CHUA_CO"
```

| Thấy gì | Kết luận | Xử lý |
|---|---|---|
| Không có thư mục / rỗng | **Chạy lần đầu** | `mkdir -p .claude/_workspace` rồi đi tiếp |
| Có file `{phase-đang-làm}_*` | **Chạy lại một phần** | ĐỌC hết những file đó trước. Cải thiện tiếp, **không xóa làm lại từ đầu** |
| Chỉ có file của phase trước | **Chạy mới cho phase này** | Đọc file phase trước để lấy hợp đồng đã chốt (kiểu `Result`, `ICommand`/`IQuery`, tên bảng, tên endpoint) |

Xóa sản phẩm cũ rồi làm lại từ đầu làm mất các quyết định đã trả giá bằng một vòng build
đỏ — và lần chạy mới sẽ đi lại đúng con đường sai đó.

### Bước 0.2 — Đọc tài liệu, đúng thứ tự này

1. `CLAUDE.md` — nguyên tắc số một (khung dự án do công cụ sinh) + quy ước code
2. `docs/architecture/00-tech-stack-decisions.html` — 13 ADR + **§5 Quy ước bắt buộc khi code**. Nguồn chân lý kỹ thuật
3. `docs/superpowers/plans/2026-08-13-teachhub-roadmap.html` — **Definition of Done** (7 mục) + thứ tự phase
4. Plan của phase đang làm:

| Phase | File plan (`docs/superpowers/plans/`) |
|---|---|
| 0 — Nền móng | `2026-08-13-phase-0-foundation.html` |
| 1 — Nội dung & Versioning | `2026-08-13-phase-1-content-versioning.html` |
| 2 — Lớp học phần & Trải nghiệm học | `2026-08-13-phase-2-offering-enrollment.html` |
| 3 — Bài tập & Nộp bài | `2026-08-13-phase-3-assignments-submissions.html` |
| 4a — Chấm điểm & Nhóm | `2026-08-13-phase-4-grading-gradebook.html` |
| 4b — Sổ điểm động & Điểm cộng | `2026-08-13-phase-4b-dynamic-gradebook.html` |
| 5–9 | `2026-08-13-phases-5-9-backlog.html` |

`docs/requirements/SRS-LMS-GiangVien.md` là chân lý về **nghiệp vụ**. Mâu thuẫn kỹ thuật
giữa SRS và tài liệu kiến trúc thì **lấy tài liệu kiến trúc**.

### Bước 0.3 — Kiểm trạng thái repo thật, không tin trí nhớ

```bash
git status --short && ls backend/TeachHub.sln 2>/dev/null
dotnet --version && docker ps --format '{{.Names}}\t{{.Status}}'
```

Có `.sln` rồi thì Phase 0 Task 0.1 là **chạy lại một phần**, không phải `dotnet new sln`
lần nữa. Docker chết thì Testcontainers không chạy — xem *Xử lý lỗi*, và **tuyệt đối
không** rơi về InMemory provider để né (L10: mất sạch giá trị của integration test).

### Bước 0.4 — Sinh bản checklist `.md` để tick

Roadmap yêu cầu: khi bắt đầu code một phase, sinh kèm bản `.md` của plan để tick checkbox.
Đặt ở `.claude/_workspace/{phase}_orchestrator_checklist.md`, một dòng `- [ ]` cho **mỗi
task** trong plan, giữ nguyên số hiệu task (`Task 0.8c`, `Task 1.6`, `Task 4b.7`).
Đây là ngoại lệ Markdown hợp lệ (công cụ tiêu thụ); mọi tài liệu khác vẫn viết HTML.

---

## Giai đoạn 1 — Lập nhóm

Gọi `Agent` cho từng vai trò dưới đây, truyền `subagent_type` đúng bằng tên file trong
`.claude/agents/` (ví dụ `subagent_type: "domain-engineer"`). Prompt của agent được nạp
TỰ ĐỘNG từ file định nghĩa — không phải chép nội dung file vào prompt.

> **Công cụ điều phối có thật trong môi trường này:** `Agent` (spawn subagent, đặt
> `run_in_background: true` để chạy song song), `TodoWrite` (thứ tự + phụ thuộc task),
> `SendMessage` (nhắn cho agent đang chạy), `ListAgents`. **KHÔNG có `TeamCreate` /
> `TaskCreate`** — đã kiểm bằng `ToolSearch`. Đừng viết kịch bản dựa vào chúng.

| Agent | Trách nhiệm | Không được đụng vào |
|---|---|---|
| `plan-executor` | **Trưởng nhóm.** Giữ thứ tự task, tick checklist, chốt DoD từng task | Không tự viết code sản xuất |
| `domain-engineer` | Entity, enum, value object, luật nghiệp vụ thuần + `Domain.Tests` | EF Core, MediatR, ASP.NET Core (L2) |
| `backend-engineer` | Command/Query + Handler + Validator, ánh xạ EF, migration, Controller | Component React |
| `frontend-engineer` | Next.js 16 App Router, shadcn/ui, TanStack Query, client sinh từ OpenAPI | Sửa entity/handler để "cho dễ gọi API" |
| `build-verifier` | `dotnet build` (0 warning), `dotnet test`, `pnpm build`, `pnpm gen:api` | Không tự sửa code — báo lỗi về đúng agent gây ra |

**Mọi lời gọi Agent phải có `model: "opus"`.** Không phải sở thích: bộ luật L1–L10 của dự
án toàn là lỗi âm thầm (thiếu `[MemberNotNullWhen]`, thiếu một dòng `OwnsOne`, thiếu
`ConfigureHttpJsonOptions`) — model nhỏ hơn viết ra code *trông đúng* rồi đứt ở `dotnet build`
vì `TreatWarningsAsErrors`, và mỗi vòng sửa đó tốn hơn phần chi phí tiết kiệm được.

**Song song hay tuần tự:** hai agent chạm hai tầng khác nhau và không phụ thuộc nhau thì
spawn song song (`run_in_background: true`); còn lại phải tuần tự. Ví dụ `domain-engineer`
và `frontend-engineer` của cùng một phase thường độc lập, nhưng `backend-engineer` luôn
phải đợi `domain-engineer` xong vì handler tham chiếu entity.

Nếu môi trường nào đó CÓ `TeamCreate`/`TaskCreate` thì dùng cũng được, nhưng phải **ghi rõ
vào báo cáo** là đã đổi cơ chế. Im lặng đổi cách điều phối là điều cấm — người đọc báo cáo
sẽ tưởng các task đã chạy theo cách khác với thực tế.

---

## Giai đoạn 2 — Phân công

Dựng danh sách việc bằng `TodoWrite` **theo đúng thứ tự task ghi trong plan**, giữ nguyên
số hiệu task. `TodoWrite` không có trường phụ thuộc, nên phụ thuộc được thể hiện bằng
**thứ tự** và bằng việc chỉ spawn agent kế tiếp sau khi agent trước báo xong. Plan đã được sắp theo thứ tự phụ thuộc thật — đảo thứ tự là hỏng ngay:
`Task 1.3` (ánh xạ EF + migration) phải sau `Task 1.1`/`1.2` vì migration sinh **từ** entity;
chạy trước thì `ModelSnapshot` lệch model thật và migration kế tiếp sinh lệnh sai trên DB
đã có điểm thật của sinh viên.

### Khuôn phụ thuộc chuẩn của một module

```
domain-engineer  (entity + enum + luật thuần + Domain.Tests)
        ↓
backend-engineer (ánh xạ EF → migration → Handler/Validator → Controller)
        ↓
build-verifier   (build + test + sinh lại /openapi/v1.json)   ← CỔNG, không bỏ qua
        ↓
frontend-engineer (pnpm gen:api → màn hình)
        ↓
build-verifier   (pnpm build)
```

`frontend-engineer` phụ thuộc vào **OpenAPI sinh được**, không phải vào "backend tự nhận là
xong". Cho frontend chạy sớm là nó gõ tay type từ trí nhớ — vi phạm L1 và tái diễn đúng
lớp lỗi L5/L6 mà dự án đã trả giá.

### Ba việc phải khai thành task riêng, không gộp

- **Cách ly giảng viên:** phase nào thêm một loại tài nguyên mới thuộc về giảng viên thì
  **phải** có task thêm dòng vào `CrossInstructorIsolationTests` (Phase 0 Task 0.8c).
  Thiếu dòng đó thì task chưa được tick — DoD mục 7. Ma trận cách ly chỉ có giá trị khi nó
  lớn cùng nhịp với hệ thống.
- **Migration:** luôn là bước riêng sau khi entity + configuration đã xong, và phải **đọc
  file sinh ra** xem có `DROP` ngoài ý muốn không.
- **Sinh lại client TS:** mỗi lần chạm API. Không gộp vào task frontend, vì nó là *đầu ra*
  của backend, không phải việc của frontend.

---

## Giai đoạn 3 — Thi công

Vòng lặp cho **từng task**, không gộp nhiều task vào một lượt:

1. Đọc lại đúng mục task đó trong plan (plan có sẵn code mẫu và lý do — đừng sáng tác lại).
2. **Có công cụ sinh thì chạy công cụ trước** (L1): `dotnet new`, `dotnet add reference|package`,
   `dotnet ef migrations add`, `pnpm create next-app`, `pnpm dlx shadcn@latest add`, `pnpm gen:api`.
   Lệnh không chạy được → **BÁO LẠI**, tuyệt đối không đoán rồi gõ tay thay thế.
3. Viết test **đỏ trước**. Chạy nó. Xác nhận đỏ **vì đúng lý do**: đỏ vì thiếu code sản xuất
   (kể cả không compile vì entity chưa tồn tại — plan Task 0.1 nói rõ đó là trạng thái đỏ
   mong đợi) là hợp lệ; đỏ vì gõ sai tên assembly thì không tính, sửa rồi chạy lại.
4. Code **tối thiểu** làm nó xanh.
5. `dotnet build` → phải `0 Warning(s) 0 Error(s)`.
6. **`build-verifier` chạy ngay sau mỗi module**, không dồn tới cuối phase. Dồn thì khi đỏ
   không còn biết module nào gây ra, và với `TreatWarningsAsErrors` thì một warning ở module
   đầu chặn luôn việc kiểm module cuối.
7. Ghi sản phẩm vào `.claude/_workspace/` (xem *Giao thức truyền dữ liệu*).
8. Commit theo Conventional Commits.

### Cổng tự soát trước khi gọi build-verifier

Mỗi dòng dưới đây từng là một lỗi thật, đã được `dotnet build` xác nhận.

| Kiểm | Sai thì hỏng thế nào |
|---|---|
| **L1** Khung dự án do công cụ sinh, không gõ tay | Thiếu thuộc tính MSBuild / sai GUID `.sln` — không lộ lúc đọc, lộ lúc nâng SDK sáu tháng sau |
| **L2** Domain không tham chiếu EF Core, MediatR, FluentValidation, ASP.NET Core, Hangfire, AWSSDK | `ArchitectureTests` đỏ. `TransactionBehavior` đặt nhầm ở Application thì không thấy `DbContext`, không compile |
| **L2** Validator FluentValidation là hàm thuần, không chạm I/O | Luật cần đọc DB nằm trong validator → không test được ở `Application.Tests`, và ném exception thay vì trả `Result` (ADR-011) |
| **L3** `Result<T>.IsSuccess` có `[MemberNotNullWhen(true, nameof(Value))]` | Mọi chỗ `if (!kq.IsSuccess) return …; kq.Value.X` sinh CS8602, `TreatWarningsAsErrors` biến thành lỗi build |
| **L4** MediatR ghim đúng `12.5.0`; behavior viết `await next()` | Nâng lên 13.x là giấy phép RPL-1.5, buộc công khai mã nguồn hệ thống chứa điểm sinh viên. Lambda `() => …` trong test là CS1593 — phải `_ => …` |
| **L4** Thứ tự behavior: Logging → Validation → Transaction → Handler | Không có lỗi nào cả; chỉ là mỗi request sai dữ liệu vẫn mở transaction rồi rollback, connection pool cạn dần lúc cao tải |
| **L5** JSON snake_case khai ở **cả** `AddJsonOptions` **lẫn** `ConfigureHttpJsonOptions` | Thiếu cái đầu: frontend nhận `access_token` là `undefined`, mọi request sau 401. Thiếu cái sau: runtime snake_case nhưng OpenAPI khai camelCase, client TS gõ sai khóa mà vẫn biên dịch |
| **L6** Record command không có tham số positional kiểu giá trị mang giá trị mặc định | `JsonSchemaExporter` ném `JsonException` → `/openapi/v1.json` trả 500 → `pnpm gen:api` chết. Trường server tự điền phải là property `init`-only |
| **L7** `IFormFile` bọc trong lớp `[FromForm]`; có **cả** `[RequestSizeLimit]` **lẫn** `[RequestFormLimits]` | Tham số rời làm OpenAPI sinh `x-www-form-urlencoded` kèm thuộc tính nội bộ của `IFormFile`, không có field nhị phân. Thiếu một trong hai giới hạn là chặn hụt ở tầng còn lại |
| **L8** Policy chỉ đặt ở **cấp action**; tài nguyên không thuộc người gọi trả **404**, lọc `instructor_id` **trong câu truy vấn** | `[Authorize]` ở class và action cộng dồn chứ không ghi đè. Trả 403 là xác nhận id có tồn tại — rò rỉ dữ liệu giữa các giảng viên (ADR-012) |
| **L9** Điểm `decimal` kẹp `[0,10]`; `DateTimeOffset` + `TimeProvider`; `Guid.CreateVersion7()`; enum `.HasConversion<string>()` — **kể cả enum trong `jsonb`**, **mỗi property một dòng `OwnsOne`** | EF dùng `JsonValueReaderWriter` riêng, không đọc `JsonStringEnumConverter` của ASP.NET Core → enum trong jsonb xuống DB thành số, CHECK constraint khớp chuỗi `'GradeColumn'` chết ngay lúc apply migration |
| **L10** Test đỏ trước; Domain suite < 10 giây; integration dùng Testcontainers + Postgres 17 thật | Đổi sang InMemory provider là mất toàn bộ giá trị của integration test — nó không có transaction thật, không có CHECK constraint, không có snake_case |

Hai chỗ dễ hiểu nhầm nhất, viết cạnh nhau:

```csharp
// ✅ ĐÚNG — server tự điền thì để init-only property
public sealed record CreateSubjectCommand(string Name) : ICommand<Guid>
{
    public Guid InstructorId { get; init; }
}

// ❌ SAI — positional + default: /openapi/v1.json trả 500, pnpm gen:api chết
public sealed record CreateSubjectCommand(string Name, Guid InstructorId = default) : ICommand<Guid>;
```

```csharp
// ✅ ĐÚNG — mỗi property complex một dòng OwnsOne riêng, khai enum tường minh
builder.OwnsOne(x => x.Aggregation, b => { b.ToJson(); b.Property(a => a.Kind).HasConversion<string>(); });
builder.OwnsOne(x => x.Condition,   b => { b.ToJson(); b.Property(a => a.Operator).HasConversion<string>(); });

// ❌ SAI — khai một lần rồi tưởng EF suy ra cho property cùng kiểu: enum xuống jsonb thành số
builder.OwnsOne(x => x.Aggregation, b => { b.ToJson(); b.Property(a => a.Kind).HasConversion<string>(); });
builder.OwnsOne(x => x.Condition,   b => b.ToJson());
```

---

## Giai đoạn 4 — Chốt phase

### Bước 4.1 — Đối chiếu Definition of Done, từng task một

Bảy mục của roadmap, không mục nào được du di:

1. Test viết **trước**, đã thấy đỏ vì đúng lý do, rồi mới có code làm nó xanh.
2. `dotnet build` **không cảnh báo**.
3. **Toàn bộ** test suite xanh, không chỉ test vừa viết.
4. Chạm schema → có migration EF Core, đã `Update-Database` thành công trên DB trống **lẫn**
   DB có sẵn dữ liệu.
5. Chạm API → OpenAPI sinh lại được, client TS sinh lại được, frontend vẫn build.
6. Đã commit theo Conventional Commits.
7. Phase thêm loại tài nguyên mới thuộc giảng viên → đã thêm dòng vào `CrossInstructorIsolationTests`.

Kèm hai lệnh xác nhận rẻ mà bắt được lỗi đắt:

```bash
dotnet test backend/TeachHub.sln                       # xanh, 0 cảnh báo
grep -r "MediatR" backend/Directory.Packages.props     # vẫn đúng 12.5.0
```

### Bước 4.2 — Chạy kịch bản nghiệm thu thủ công của plan

DoD cấp phase: **thao tác tay trên UI thật, không phải chỉ test tự động**. Mỗi plan có mục
"Nghiệm thu" riêng — làm đúng từng bước, theo đúng thứ tự. Ví dụ Phase 0: `docker compose up -d`
→ `dotnet run` → mở `/scalar/v1` → `GET /health` trả **200 chứ không phải 401** → `POST /api/auth/login`
trả JSON có khóa `access_token` (snake_case) → login sai định dạng trả `application/problem+json`
→ log có dòng `LoggingBehavior` → cookie `th_at` có cờ `HttpOnly`.

Test tự động xanh mà nghiệm thu tay đỏ là chuyện thường ở dự án này — L5 và L8 đều thuộc
loại "build xanh, test xanh, API trả 200" mà vẫn hỏng thật.

### Bước 4.3 — Viết báo cáo chốt phase

`.claude/_workspace/{phase}_orchestrator_report.html` (HTML, self-contained, theo quy ước
tài liệu của người dùng). Bắt buộc có: task đã xong · **task còn thiếu và vì sao** ·
quyết định kỹ thuật mới phát sinh · kết quả nghiệm thu tay từng bước · việc phải làm ở
phase sau.

---

## Giao thức truyền dữ liệu

Ba kênh, mỗi kênh một việc. Dùng nhầm kênh là mất dữ liệu hoặc mất dấu vết.

| Kênh | Dùng cho | Không dùng cho |
|---|---|---|
| **Danh sách việc** (`TodoWrite`) | Điều phối: ai đang làm task nào, xong chưa, chặn ai | Chở nội dung dài |
| **File** (`.claude/_workspace/`) | **Mọi hợp đồng liên agent**: danh sách entity, tên bảng, tên endpoint, shape DTO, log build | Trò chuyện |
| **Message** (`SendMessage`) | Hỏi/đáp tức thời, báo đỏ ngay lúc phát hiện | Bàn giao hợp đồng |

Đặt tên file: `{phase}_{agent}_{artifact}.{ext}`

```
phase-1_domain-engineer_entities.md
phase-1_backend-engineer_endpoints.json
phase-1_build-verifier_build-log.txt
phase-1_orchestrator_checklist.md
phase-1_orchestrator_report.html
```

**Hợp đồng phải qua file, không qua message.** Nói miệng thì `frontend-engineer` dựng client
theo trí nhớ hội thoại thay vì theo `/openapi/v1.json` thật — và đó chính là cách L5/L6 lọt
qua: tên khóa camelCase/snake_case lệch nhau mà TypeScript vẫn biên dịch sạch.

---

## Xử lý lỗi

**Thử lại đúng 1 lần**, kèm thông tin bổ sung thật (log lỗi nguyên văn, lệnh tái hiện) —
không phải chỉ nhắc "làm lại đi", vì lần hai sẽ ra đúng kết quả lần một.

Thất bại lần thứ hai:

1. Đánh dấu task là **BỎ DỞ** trong checklist (`- [!]`), không tick.
2. Ghi vào mục "Phần còn thiếu" của báo cáo: số hiệu task · triệu chứng nguyên văn ·
   lệnh tái hiện · task nào ở sau bị ảnh hưởng.
3. **Đi tiếp** các task độc lập. Task phụ thuộc trực tiếp thì chặn lại, không code mù lên
   trên một nền chưa chắc.

Im lặng bỏ qua là điều cấm tuyệt đối. Một phase báo "xong" mà thiếu task còn tệ hơn phase
báo "xong 9/12 task, thiếu 3 task vì lý do X" — cái sau sửa được, cái trước phải phát hiện
lại từ đầu ở phase sau.

### Ba ca DỪNG HẲN, không thử lại, báo ngay cho người dùng

- **Lệnh sinh khung không chạy được** (`dotnet new`, `dotnet ef`, `pnpm create next-app`,
  `pnpm dlx shadcn`, `pnpm gen:api`). Cấm đoán rồi gõ tay thay thế — đó là vi phạm nguyên
  tắc số một, và file gõ tay sai ở chỗ không nhìn thấy.
- **Docker/Testcontainers không chạy được.** Báo để người dùng bật Docker Desktop hoặc đặt
  `DOCKER_HOST` cho Colima. Cấm chuyển sang InMemory provider để né.
- **MediatR bị nâng khác `12.5.0`.** Trả về 12.5.0 rồi mới đi tiếp; đây là ràng buộc giấy phép,
  không phải sở thích kỹ thuật.

---

## Kịch bản kiểm thử

### Luồng bình thường — "làm Phase 1" trên repo đã xong Phase 0

1. Giai đoạn 0: `.claude/_workspace/` có `phase-0_*`, không có `phase-1_*` → **chạy mới cho
   phase này**; đọc `phase-0_backend-engineer_*` để lấy hợp đồng `Result` / `ICommand` /
   `IQuery` đã chốt; đọc CLAUDE.md → kiến trúc §5 → roadmap → `2026-08-13-phase-1-content-versioning.html`;
   `git status` sạch, `backend/TeachHub.sln` đã có → **không** chạy lại `dotnet new sln`;
   sinh `phase-1_orchestrator_checklist.md` với 9 dòng `- [ ]` từ Task 1.1 tới Task 1.9.
2. Giai đoạn 1: spawn 5 agent bằng `Agent` với `subagent_type` đúng tên file, tất cả `model: "opus"`.
3. Giai đoạn 2: `TodoWrite` Task 1.1 → 1.9 đúng thứ tự; 1.3 phụ thuộc 1.1+1.2; 1.4/1.5 phụ
   thuộc 1.3; 1.6 phụ thuộc 1.5; 1.8/1.9 phụ thuộc `pnpm gen:api` chạy được; thêm task cách
   ly cho `Subject`/`SubjectVersion` vào `CrossInstructorIsolationTests`.
4. Giai đoạn 3: mỗi task một vòng TDD; `build-verifier` chạy sau Task 1.2 (module Domain),
   sau Task 1.3 (migration), sau Task 1.5 (API), sau Task 1.9 (frontend).
5. Giai đoạn 4: soát 7 mục DoD; chạy nghiệm thu tay của Phase 1 — tạo môn "Lập trình di động",
   version `2026-2027` 2 chương, xuất bản, tạo version `2027-2028` từ nó, sửa chương 1, xác
   nhận `2026-2027` **không đổi**; viết `phase-1_orchestrator_report.html`.

**Kết quả mong đợi:** checklist 9/9 tick, build 0 warning, nghiệm thu tay xanh, báo cáo HTML
ghi rõ `SubjectVersionCloner` remap những FK nào.

### Luồng lỗi — "làm Phase 3" nhưng `dotnet ef migrations add` không chạy được

1. Giai đoạn 3, task ánh xạ EF: `dotnet ef migrations add AddSubmissionSlots` báo không tìm
   thấy công cụ `dotnet-ef`.
2. Thử lại 1 lần, có bổ sung thật: cài `dotnet tool restore` / `dotnet tool install dotnet-ef`,
   chạy lại. Vẫn đỏ.
3. **DỪNG HẲN cho nhánh migration** — đây là ca "lệnh sinh khung không chạy được". Không viết
   tay file migration (ModelSnapshot sẽ lệch model thật, và migration kế tiếp sinh lệnh sai
   trên DB đã có bài nộp thật của sinh viên).
4. Đánh dấu task `- [!]`, ghi vào báo cáo: số hiệu task, log nguyên văn, lệnh tái hiện,
   và các task bị chặn theo (controller upload + màn hình 6.6 vì bảng chưa tồn tại).
5. **Đi tiếp** các task độc lập của Phase 3 không cần schema mới: entity + luật thuần ở Domain,
   `Domain.Tests` cho chống zip-slip / zip bomb — những thứ này không đụng DB.
6. Giai đoạn 4: báo cáo ghi rõ Phase 3 **chưa đạt DoD mục 4**, nghiệm thu tay không chạy được
   tới bước nộp file, và việc đầu tiên của lần chạy sau là dựng lại `dotnet-ef`.

**Kết quả mong đợi:** không có file migration gõ tay nào trong repo; báo cáo nêu đích danh
task bỏ dở và lệnh tái hiện; các task độc lập vẫn xong và vẫn xanh.
