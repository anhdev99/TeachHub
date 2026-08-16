# TeachHub — Hướng dẫn cho AI agent

Hệ thống quản lý dạy học cá nhân. Backend .NET 10 + Controllers/MediatR, frontend Next.js 16,
PostgreSQL 17. Tài liệu thiết kế đầy đủ ở [`docs/index.html`](docs/index.html).

---

## ⛔ NGUYÊN TẮC SỐ MỘT: khung dự án phải do CÔNG CỤ SINH, không phải AI viết tay

**Mọi thứ có công cụ chính thức sinh ra được thì BẮT BUỘC chạy công cụ đó.** AI chỉ được
sửa file sau khi công cụ đã sinh, tuyệt đối không tự gõ lại nội dung file mà template tạo ra.

Lý do không phải hình thức. File do AI gõ tay trông đúng nhưng sai ở những chỗ không nhìn thấy:
thiếu thuộc tính MSBuild mà SDK bản đó cần, sai GUID trong `.sln`, thiếu `Using` ngầm định,
lệch version package so với bản thực sự có trên NuGet, thiếu file cấu hình đi kèm. Những sai lệch
đó không lộ ra lúc đọc, chỉ lộ khi build hoặc — tệ hơn — khi nâng cấp SDK sáu tháng sau.
Công cụ chính thức luôn biết bản hiện tại cần gì; trí nhớ của mô hình thì không.

### Quy trình bắt buộc

```
1. Chạy lệnh sinh của công cụ chính thức
2. Xem nó tạo ra gì (đọc file, chạy `git status`)
3. CHỈ SỬA phần cần khác đi
4. Build/chạy để xác nhận vẫn đúng
```

### Bảng đối chiếu

| Việc | ✅ Phải chạy | ❌ Không được làm |
|---|---|---|
| Tạo solution | `dotnet new sln -n TeachHub` | Gõ tay file `.sln` |
| Tạo project | `dotnet new classlib\|web\|xunit -o <đường-dẫn>` | Gõ tay `.csproj` |
| Tham chiếu project | `dotnet add <A> reference <B>` | Thêm tay `<ProjectReference>` |
| Thêm package | `dotnet add <proj> package <Tên>` | Thêm tay `<PackageReference>` |
| `Directory.Build.props` | `dotnet new buildprops` rồi sửa | Gõ mới từ đầu |
| `Directory.Packages.props` | `dotnet new packagesprops` rồi sửa | Gõ mới từ đầu |
| `.gitignore` / `.editorconfig` | `dotnet new gitignore` / `editorconfig` | Gõ tay |
| `global.json` | `dotnet new globaljson --sdk-version <v>` | Gõ tay |
| Migration EF Core | `dotnet ef migrations add <Tên>` | **Viết tay file migration** |
| Tạo frontend | `pnpm create next-app@latest frontend …` | Dựng tay cấu trúc Next.js |
| Cài shadcn/ui | `pnpm dlx shadcn@latest init` / `add <component>` | Chép tay mã component |
| Client gọi API (TS) | `pnpm gen:api` (openapi-typescript) | Gõ tay type/hàm fetch |

### Riêng migration EF Core — không có ngoại lệ

Migration viết tay là một trong những cách hỏng dữ liệu nhanh nhất: `ModelSnapshot` không khớp
model thật, và migration kế tiếp sẽ sinh ra lệnh sai trên cơ sở dữ liệu đã có dữ liệu thật của
sinh viên. Luôn sửa entity/configuration trước, rồi `dotnet ef migrations add`, rồi **đọc file
sinh ra** xem có lệnh `DROP` nào ngoài ý muốn không.

### Được phép viết tay

Code nghiệp vụ do mình nghĩ ra: entity, handler, controller, behavior, test, component React,
file cấu hình mà không template nào sinh (`docker-compose.yml`, workflow CI, `appsettings.json`).
Ranh giới đơn giản: **có công cụ sinh thì chạy công cụ; không có thì tự viết.**

### Nếu không chạy được lệnh

Không được đoán rồi gõ tay thay thế. Báo lại cho người dùng biết lệnh nào không chạy được và vì sao,
để họ chạy hộ hoặc quyết định cách khác.

---

## Tài liệu

Đọc theo thứ tự khi bắt đầu:

1. [`docs/index.html`](docs/index.html) — mục lục
2. [`docs/architecture/00-tech-stack-decisions.html`](docs/architecture/00-tech-stack-decisions.html) —
   13 ADR, 26 lỗ hổng của SRS kèm cách xử lý, quy ước code. **Nguồn chân lý về kỹ thuật.**
3. [`docs/superpowers/plans/2026-08-13-teachhub-roadmap.html`](docs/superpowers/plans/2026-08-13-teachhub-roadmap.html) —
   lộ trình và Definition of Done
4. Plan của phase đang làm

[`docs/requirements/SRS-LMS-GiangVien.md`](docs/requirements/SRS-LMS-GiangVien.md) là nguồn chân lý
về **nghiệp vụ**. Khi SRS và tài liệu kiến trúc mâu thuẫn về mặt kỹ thuật, lấy tài liệu kiến trúc.

## Quy ước code (chi tiết ở Kiến trúc §5)

- Chiều phụ thuộc: `Domain ← Application ← Infrastructure ← Api`. **Không bao giờ đảo.**
  Domain không được tham chiếu EF Core, ASP.NET Core, MediatR, FluentValidation, Hangfire, AWSSDK —
  có `ArchitectureTests` canh giữ.
- Điểm số luôn `decimal`, không bao giờ `double`. Mọi điểm kẹp trong `[0, 10]`.
- Thời gian: `DateTimeOffset` + `timestamptz` UTC, mọi logic phụ thuộc thời gian đi qua `TimeProvider`.
- Id: `Guid.CreateVersion7()` sinh ở tầng ứng dụng, không để DB sinh.
- Enum xuống DB: `.HasConversion<string>()`, kể cả enum nằm trong cột `jsonb`.
- Tài nguyên không thuộc người gọi: trả **404, không bao giờ 403**.
- MediatR ghim **12.5.0** (Apache-2.0). Không nâng lên 13.x/14.x — giấy phép đổi sang RPL-1.5.
- Controller phải mỏng: định tuyến, lấy `CurrentUserId`, gọi `sender.Send`, quy đổi `Result`. Hết.

## Định nghĩa "Xong"

Đầy đủ ở roadmap. Hai mục hay bị quên nhất:

- Test viết **trước**, đã thấy đỏ vì đúng lý do mong đợi, rồi mới có code làm nó xanh.
- Phase nào thêm loại tài nguyên mới thuộc về giảng viên thì **phải** thêm dòng vào
  `CrossInstructorIsolationTests` (Phase 0 Task 0.8c) — thiếu thì task chưa được tick.

## Harness

**Mục tiêu:** thi công các plan trong `docs/superpowers/plans/` theo TDD mà không tái phạm
13 blocker + 27 major đã tìm ra trong giai đoạn thiết kế.

**Trigger:**

| Khi nào | Dùng |
|---|---|
| "làm Phase 0", "thi công plan", "làm tiếp", "chạy lại phase" | skill `teachhub-build` (điều phối) |
| Trước khi gõ bất kỳ file khung nào (`.sln`, `.csproj`, migration, khởi tạo Next.js) | skill `teachhub-scaffold` |
| Sửa `backend/src/TeachHub.Application` hoặc `TeachHub.Api` | skill `teachhub-backend` |
| Entity, EF configuration, migration, truy vấn | skill `teachhub-data` |
| Bất kỳ file nào dưới `frontend/` | skill `teachhub-frontend` — **đọc `frontend/node_modules/next/dist/docs/` trước khi gõ code**, xem ghi chú Next 16 bên dưới |
| Chạy build/test, kiểm tuân thủ luật | skill `teachhub-verify` |

Câu hỏi đơn giản (một dòng, một khái niệm) thì trả lời thẳng, không cần gọi skill.

**Agent** (`.claude/agents/`): `plan-executor` (trưởng nhóm) · `domain-engineer` ·
`backend-engineer` · `frontend-engineer` · `build-verifier` (cổng chất lượng).
Spawn bằng `Agent` với `subagent_type` đúng tên file, luôn `model: "opus"`.

> Môi trường này **không có** `TeamCreate`/`TaskCreate`. Điều phối bằng `Agent` +
> `TodoWrite` + `SendMessage`.

**Biến động:**

| Ngày | Thay đổi | Đối tượng | Lý do |
|------|----------|-----------|-------|
| 2026-08-14 | Dựng harness ban đầu | 5 agent + 6 skill | Chuẩn bị thi công Phase 0 |
| 2026-08-14 | Vá 5 blocker + 19 major sau vòng kiểm chứng | agents + skills | "Đỏ đúng lý do" bị dạy ngược nguồn chân lý; BFF cookie không biên dịch trên Next.js 15; orchestrator dựng trên hai công cụ không tồn tại; 6 lệnh grep của build-verifier báo FAIL trên chính khuôn đúng của dự án |
| 2026-08-15 | Cập nhật stack sang Next.js 16 + vá 8 chỗ sai trong plan Phase 0 | docs + agents + skills | Thi công Phase 0 thật cho thấy `create-next-app@latest` sinh Next 16; và plan sai ở 8 chỗ, nặng nhất là `AddAuth` đọc config lúc đăng ký service khiến mọi request có token trả 401 mà không log gì |

## Next.js 16 — đọc tài liệu trong `node_modules`, đừng viết theo trí nhớ

Frontend chạy **Next 16.3.1** (do `pnpm create next-app@latest` sinh ở Phase 0 Task 0.11), không
phải 15 như bản tài liệu đầu tiên ghi. Chính `create-next-app` sinh kèm `frontend/AGENTS.md` mở đầu
bằng *"This is NOT the Next.js you know — This version has breaking changes… Read the relevant guide
in `node_modules/next/dist/docs/` before writing any code."*

Vì thế: **mọi việc dưới `frontend/` phải đọc `frontend/node_modules/next/dist/docs/` trước.** API,
quy ước và cấu trúc file có thể đã khác bản trong trí nhớ mô hình. Đã kiểm chứng chạy được trên
16.3.1: App Router + route group, route handler `src/app/api/**`, `await cookies()` đọc/ghi cookie
httpOnly, `redirect()` trong server component, `params` kiểu `Promise` trong catch-all route.

## Ngôn ngữ

Ranh giới là **ai đọc dòng đó**, không phải nó nằm ở tầng nào.

| Người đọc | Ngôn ngữ | Gồm những gì |
|---|---|---|
| Giảng viên / sinh viên | **Tiếng Việt** | Nhãn nút, tiêu đề, `placeholder`, `aria-label`, thông báo lỗi trả về từ API (`Result.Invalid("…")`), nội dung `toast` |
| Lập trình viên | **Tiếng Anh** | Chú thích code, XML doc, tên test, tên kiểu/biến/hàm, thông điệp commit |

Sản phẩm phục vụ người Việt nên **mọi thứ hiển thị ra màn hình đều là tiếng Việt** — kể cả câu lỗi
kỹ thuật, vì người đọc nó là giảng viên chứ không phải lập trình viên. Ngược lại, không có dòng chú
thích nào lọt tới người dùng, nên chúng viết tiếng Anh.

Cách phân biệt khi phân vân: **chuỗi đó có khả năng xuất hiện trên màn hình không?** Có thì tiếng
Việt. Chuỗi trong `Result.Conflict(...)` là tiếng Việt vì nó đi thẳng vào `detail` của ProblemDetails
và hiện trong `toast`; chuỗi trong `log.LogWarning(...)` là tiếng Anh vì nó chỉ vào log.

Tên test **cũng là tiếng Anh** — chỉ lập trình viên đọc tên test. Dùng dấu gạch dưới để câu vẫn đọc
được: `Locked_lesson_sends_no_content`, `Closed_offering_blocks_submission`.

> Đây là quy ước **đổi ngày 17/08/2026**. Trước đó chú thích và tên test viết tiếng Việt; nếu gặp
> file còn theo lối cũ thì đó là chưa chuyển xong, không phải cố ý.
