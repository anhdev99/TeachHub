# TeachHub — Hướng dẫn cho AI agent

Hệ thống quản lý dạy học cá nhân. Backend .NET 10 + Controllers/MediatR, frontend Next.js 15,
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

## Ngôn ngữ

Tài liệu, chú thích code, tên test và thông báo lỗi trả cho người dùng: **tiếng Việt**.
Tên kiểu/biến/hàm và commit message: tiếng Anh.
