---
name: teachhub-scaffold
description: Sinh khung dự án TeachHub bằng công cụ chính thức thay vì gõ tay — dùng khi cần tạo/sửa solution, project, Directory.Build.props, Directory.Packages.props, global.json, .gitignore, .editorconfig, thêm package hoặc project reference, sinh migration EF Core, khởi tạo frontend Next.js, cài component shadcn/ui, hay sinh client TypeScript từ OpenAPI. Trigger cả khi bắt đầu Phase 0 Task 0.1 lẫn về sau — "thêm project mới", "thêm package", "chạy lại lệnh sinh", "sửa csproj", "bổ sung migration", "cập nhật client API", "sao build lỗi sau khi tôi sửa tay file .sln/.csproj". Nếu đang định gõ nội dung một file mà `dotnet new` / `dotnet add` / `dotnet ef` / `pnpm create` / `pnpm dlx shadcn` / `pnpm gen:api` sinh được, dừng lại và đọc skill này trước.
---

# Khung dự án phải do CÔNG CỤ SINH

Đây là **luật số một** của TeachHub (`CLAUDE.md` §Nguyên tắc số một, Kiến trúc §5 khối cảnh báo
cuối mục). Không phải quy ước phong cách — là luật kỹ thuật, vi phạm sẽ trả giá bằng thời gian
debug hoặc bằng dữ liệu.

**Có template/CLI sinh được thì BẮT BUỘC chạy nó rồi mới sửa.** AI chỉ được sửa file *sau khi*
công cụ đã sinh, tuyệt đối không gõ lại từ đầu nội dung mà template tạo ra.

---

## Quy trình 4 bước — không bỏ bước nào

```
1. Chạy lệnh sinh của công cụ chính thức
2. Xem nó tạo ra gì  →  git status  +  đọc file vừa sinh
3. CHỈ SỬA phần cần khác đi
4. Build/chạy để xác nhận vẫn đúng
```

**Bước 2 không phải thủ tục.** Bỏ qua `git status` là mất dấu: template có thể sinh thêm file
bạn không biết (`Class1.cs`, `WeatherForecast`, `appsettings.Development.json`, `.gitignore` con,
thư mục `Properties/`), và bạn sẽ commit rác hoặc xóa nhầm file cần thiết. Đọc file sinh ra để
biết template bản đó *đã đặt sẵn thuộc tính gì* — bạn chỉ cần sửa phần chênh, không phải viết lại.

**Bước 3 nghĩa là diff nhỏ.** Ví dụ đúng: `dotnet new buildprops` sinh `Directory.Build.props`
gần như rỗng, ta thêm `<TargetFramework>net10.0</TargetFramework>`,
`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<InvariantGlobalization>false</InvariantGlobalization>`.
Ví dụ đúng thứ hai: template `xunit` ghim `Version="..."` ngay trong `.csproj`, xung đột với
Central Package Management — mở `tests/*/*.csproj`, **xóa** thuộc tính `Version`, xóa
`coverlet.collector`, thêm `<PackageReference Include="Shouldly" />`. Đó là sửa. Không phải xóa
file rồi gõ lại.

**Bước 4 là `dotnet build` với kỳ vọng `0 Warning(s) 0 Error(s)`** — `TreatWarningsAsErrors`
đang bật, nên một cảnh báo là một lỗi build và task chưa "Xong" (Roadmap, DoD mục 2).

---

## Bảng đối chiếu: phải chạy gì vs. không được gõ tay

| Việc | ✅ Phải chạy | ❌ Không được làm |
|---|---|---|
| Tạo solution | `dotnet new sln -n TeachHub` | Gõ tay file `.sln` |
| Tạo project | `dotnet new classlib\|web\|xunit -o <đường-dẫn>` | Gõ tay `.csproj` |
| Tham chiếu project | `dotnet add <A> reference <B>` | Thêm tay `<ProjectReference>` |
| Thêm package | `dotnet add <proj> package <Tên>` | Thêm tay `<PackageReference>` |
| `Directory.Build.props` | `dotnet new buildprops` rồi sửa | Gõ mới từ đầu |
| `Directory.Packages.props` | `dotnet new packagesprops` rồi sửa | Gõ mới từ đầu |
| `.gitignore` / `.editorconfig` | `dotnet new gitignore` / `dotnet new editorconfig` | Gõ tay |
| `global.json` | `dotnet new globaljson --sdk-version <v>` | Gõ tay |
| Migration EF Core | `dotnet ef migrations add <Tên>` | **Viết tay file migration** |
| Bỏ migration vừa sinh sai | `dotnet ef migrations remove` | Xóa tay file trong `Migrations/` |
| Tạo frontend | `pnpm create next-app@latest frontend …` | Dựng tay cấu trúc Next.js |
| Cài shadcn/ui | `pnpm dlx shadcn@latest init` / `add <component>` | Chép tay mã component |
| Client gọi API (TS) | `pnpm gen:api` (openapi-typescript) | Gõ tay type/hàm fetch |

Thêm package vào `Directory.Packages.props` bằng tay cũng tính là vi phạm ở cột phải: chạy
`dotnet add <proj> package <Tên>` trước để nó tự thêm đúng dòng `<PackageVersion>`, rồi mới
chỉnh version nếu cần ghim (xem mục MediatR bên dưới).

---

## Vì sao — lý do không hình thức

File do AI gõ tay **trông đúng nhưng sai ở những chỗ không nhìn thấy**. Đọc lại nó không phát
hiện được, vì cái sai nằm ở phần bạn không biết là mình phải viết:

- **Thiếu thuộc tính MSBuild mà SDK bản đó cần.** Template `dotnet new` của .NET 10 đặt sẵn
  những `PropertyGroup` mà bản .NET 8 không có. Bạn nhớ mẫu csproj cũ, gõ theo trí nhớ, build
  vẫn xanh hôm nay — rồi vỡ khi bật một tính năng của SDK mới, và triệu chứng không trỏ về
  file bạn đã gõ.
- **Sai GUID trong `.sln`.** Mỗi loại project có một Project Type GUID riêng và mỗi project có
  một GUID định danh riêng. Gõ tay hoặc chép từ một solution khác là hai project trùng GUID:
  Visual Studio/Rider mở lên thấy thiếu project, `dotnet build` thì build một project hai lần
  hoặc bỏ sót project kia. Đây là lỗi tốn hàng giờ vì không ai nghi ngờ file `.sln`.
- **Lệch version so với bản thật trên NuGet.** Mô hình đoán một số version nghe hợp lý
  (`10.0.0`, `9.0.1`) cho package chỉ có `10.0.11` trên feed. `dotnet restore` báo NU1101/NU1102 —
  đó là ca may. Ca xấu: version *có tồn tại* nhưng là bản khác hẳn về API, và lỗi hiện ra dưới
  dạng `CS1061` ở một file không liên quan.
- **Thiếu file đi kèm.** Template sinh cả `Properties/launchSettings.json`, `appsettings*.json`,
  `ImplicitUsings` ngầm định. Gõ tay `.csproj` xong thiếu `ImplicitUsings` là hàng chục file báo
  lỗi thiếu `using System.Linq` — và phản xạ sai lúc đó là đi thêm `using`, càng lún sâu.

**Điểm chung: sai lệch không lộ ra lúc đọc.** Nó lộ lúc build, hoặc — tệ hơn — lúc nâng SDK sáu
tháng sau, khi không còn ai nhớ file đó ai viết và viết theo gì. Công cụ chính thức luôn biết
bản hiện tại cần gì; trí nhớ của mô hình thì không, và nó *không tự biết là mình không biết*.

---

## Riêng migration EF Core — không có ngoại lệ

Migration viết tay là một trong những cách hỏng dữ liệu nhanh nhất, vì lý do rất cụ thể:

Mỗi migration đi kèm một `ModelSnapshot` — ảnh chụp *toàn bộ* model tại thời điểm đó, do EF sinh
ra bằng cách phản chiếu thật `DbContext`. EF không đọc database để tính migration kế tiếp; nó
**diff model hiện tại với `ModelSnapshot`**. Viết tay file migration mà snapshot không khớp model
thật thì lần `migrations add` sau sinh ra lệnh sai — có thể là `DropColumn` cho cột nó tưởng vừa
được thêm, hoặc `AddColumn` cho cột đã tồn tại. Và database áp lệnh đó là database **đang chứa
điểm thật và bài nộp thật của sinh viên**. Không có nút hoàn tác cho `DROP COLUMN grade`.

### Quy trình bắt buộc

```
1. Sửa entity / EntityTypeConfiguration trước    ← model là nguồn chân lý
2. dotnet ef migrations add <Tên>                 ← để EF sinh cả migration lẫn snapshot
3. ĐỌC file vừa sinh, soi lệnh phá hủy            ← bước hay bị bỏ nhất
4. Mới apply: dotnet ef database update
```

Bước 3 cụ thể — mở file `Migrations/<timestamp>_<Tên>.cs` và tìm:

- `migrationBuilder.DropColumn` / `DropTable` / `DropForeignKey` mà bạn **không chủ ý**. Nguyên
  nhân thường gặp: đổi tên property trong entity. EF không nhìn thấy "đổi tên", nó thấy "xóa cột
  cũ + thêm cột mới" — apply lên DB thật là mất sạch dữ liệu cột đó. Muốn đổi tên thì sửa
  migration thành `RenameColumn` (đây là sửa file *đã sinh*, hợp lệ ở Bước 3 của quy trình chung).
- `AlterColumn` thu hẹp kiểu (`decimal(5,2)` → `decimal(4,1)`, `varchar(64)` → `varchar(32)`):
  Postgres sẽ từ chối hoặc cắt cụt trên dữ liệu đã có.
- Cột enum sinh ra kiểu `integer` thay vì `text`: nghĩa là thiếu `.HasConversion<string>().HasMaxLength(32)`
  (Kiến trúc §5). Nếu đồng thời có CHECK constraint so sánh với chuỗi tên enum C# (`'GradeColumn'`),
  migration sẽ **chết ngay lúc apply**. Sửa configuration rồi `migrations remove` + sinh lại,
  đừng vá tay file migration.
- Enum nằm trong cột `jsonb`: EF dùng `JsonValueReaderWriter` riêng, **không** đọc
  `JsonStringEnumConverter` của ASP.NET Core — mỗi property complex cần một dòng `OwnsOne` khai
  tường minh `b.ToJson()` + `b.Property(a => a.Kind).HasConversion<string>()`. Migration sẽ không
  báo gì; sai chỉ lộ ra lúc đọc dữ liệu và enum trở thành số trong JSON.

Migration vừa sinh mà sai thì **`dotnet ef migrations remove`**, không xóa file bằng tay: xóa tay
để lại `ModelSnapshot` cũ đứng lệch, tái lập đúng cái bẫy ở đầu mục này.

DoD mục 4 (Roadmap): migration phải `database update` thành công trên **DB trống lẫn DB có sẵn
dữ liệu**. Chỉ thử trên DB trống là bỏ lọt đúng loại lỗi giết dữ liệu.

---

## Ranh giới: được phép viết tay

Code nghiệp vụ do mình nghĩ ra, và file cấu hình không template nào sinh:

- Entity, value object, enum trong `TeachHub.Domain`
- Command/Query + Handler + Validator trong `Features/<Nhóm>/...`
- Pipeline behavior (`LoggingBehavior`, `ValidationBehavior` ở Application;
  `TransactionBehavior` ở **Infrastructure** vì cần `DbContext` — ADR-010)
- Controller mỏng, `EntityTypeConfiguration`, DI extension
- Test mọi tầng
- Component React tự viết (khác với **mã component shadcn** — cái đó phải `pnpm dlx shadcn@latest add`)
- `infra/docker-compose.yml`, `.github/workflows/ci.yml`, `appsettings*.json`

**Ranh giới đơn giản: có công cụ sinh thì chạy công cụ; không có thì tự viết.**

Ca biên hay nhầm: sửa file *đã do công cụ sinh* là **được phép** và là Bước 3 của quy trình —
thêm `PackageVersion` vào `Directory.Packages.props` đã sinh, sửa `TargetFramework` trong
`Directory.Build.props` đã sinh, đổi `DropColumn` thành `RenameColumn` trong migration đã sinh.
Cái bị cấm là *tạo mới* nội dung đó khi chưa chạy lệnh sinh.

---

## Nếu không chạy được lệnh

**Không được đoán rồi gõ tay thay thế.** Đây là ca nguy hiểm nhất, vì lúc đó động cơ "cho xong
việc" mạnh nhất và kết quả trông y hệt như làm đúng.

Báo lại cho người dùng: lệnh nào, lỗi gì (dán nguyên văn), đã thử gì. Để họ chạy hộ, cài công cụ
thiếu, hoặc quyết định cách khác. Ví dụ tình huống thật: `dotnet ef` chưa cài
(`dotnet tool install --global dotnet-ef --version 10.0.11`), `pnpm gen:api` chết vì API chưa chạy
ở `http://localhost:5080`, `dotnet ef database update` chết vì container Postgres chưa `docker compose up`.
Tất cả đều là "thiếu điều kiện", không phải "lệnh vô dụng" — sửa điều kiện rồi chạy lại, đừng
thay bằng file gõ tay.

---

## Lệnh chính xác cho Phase 0 (Task 0.1 + các task liên quan)

Nguồn: `docs/superpowers/plans/2026-08-13-phase-0-foundation.html`. Chạy đúng thứ tự.

### Gốc repo — cấu hình nền

```bash
dotnet new gitignore
dotnet new editorconfig
dotnet new globaljson --sdk-version 10.0.203 --roll-forward latestFeature
```

Sau đó bổ sung vào cuối `.gitignore`: `node_modules/`, `.next/`,
`frontend/src/lib/api/schema.d.ts`, `infra/data/`, `uploads/`, `*.user`, `.env.local`.

### `backend/` — quy ước build

```bash
cd backend
dotnet new buildprops      # sinh Directory.Build.props
dotnet new packagesprops   # sinh Directory.Packages.props (Central Package Management)
```

Rồi **sửa** hai file vừa sinh theo Task 0.1 Bước 2–3: `net10.0`, `Nullable`, `ImplicitUsings`,
`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, và `InvariantGlobalization=false` (bắt buộc
`false` — hệ thống xử lý tên tiếng Việt có dấu, `true` làm `ToUpper`/so sánh chuỗi sai).

Ghim **MediatR đúng `12.5.0`** trong `Directory.Packages.props`, giữ nguyên comment rào chắn cạnh
nó. Từ v13 giấy phép đổi sang RPL-1.5/thương mại — RPL buộc công khai mã nguồn phần mềm đã triển
khai, và đây là hệ thống chứa điểm sinh viên. **Không bao giờ chạy `dotnet outdated` rồi nâng
MediatR theo quán tính.**

### `backend/` — solution, project, chiều phụ thuộc

```bash
cd backend
dotnet new sln -n TeachHub

dotnet new classlib -o src/TeachHub.Domain
dotnet new classlib -o src/TeachHub.Application
dotnet new classlib -o src/TeachHub.Infrastructure
dotnet new web     -o src/TeachHub.Api

dotnet new xunit -o tests/TeachHub.Domain.Tests
dotnet new xunit -o tests/TeachHub.Application.Tests
dotnet new xunit -o tests/TeachHub.Api.Tests

dotnet sln add src/**/*.csproj tests/**/*.csproj

# Chiều phụ thuộc — KHÔNG BAO GIỜ được đảo ngược
dotnet add src/TeachHub.Application    reference src/TeachHub.Domain
dotnet add src/TeachHub.Infrastructure reference src/TeachHub.Application
dotnet add src/TeachHub.Api            reference src/TeachHub.Infrastructure

dotnet add tests/TeachHub.Domain.Tests      reference src/TeachHub.Domain
dotnet add tests/TeachHub.Application.Tests reference src/TeachHub.Application
dotnet add tests/TeachHub.Api.Tests         reference src/TeachHub.Api

# Xóa file mẫu do template sinh
rm src/TeachHub.Domain/Class1.cs src/TeachHub.Application/Class1.cs src/TeachHub.Infrastructure/Class1.cs
```

Đảo chiều phụ thuộc (ví dụ `dotnet add src/TeachHub.Domain reference src/TeachHub.Infrastructure`)
làm `ArchitectureTests` đỏ ngay — và đó là test quan trọng nhất Phase 0: nó ngăn EF Core lọt vào
Domain, thứ sẽ giết tính test-được trong mili-giây của toàn bộ luật tính điểm.

Sau khối này: `dotnet build` → kỳ vọng `Build succeeded`, **0 Warning(s)**, 0 Error(s).

### Package (chạy `dotnet add`, không sửa tay `.csproj`)

```bash
dotnet add src/TeachHub.Domain          package Microsoft.Extensions.Identity.Stores
dotnet add src/TeachHub.Application     package Microsoft.EntityFrameworkCore
dotnet add src/TeachHub.Infrastructure  package Microsoft.EntityFrameworkCore
dotnet add src/TeachHub.Infrastructure  package Microsoft.EntityFrameworkCore.Design
dotnet add src/TeachHub.Infrastructure  package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/TeachHub.Infrastructure  package EFCore.NamingConventions
dotnet add src/TeachHub.Infrastructure  package Microsoft.AspNetCore.Identity.EntityFrameworkCore

dotnet add src/TeachHub.Application     package MediatR --version 12.5.0
dotnet add src/TeachHub.Application     package FluentValidation
dotnet add src/TeachHub.Application     package FluentValidation.DependencyInjectionExtensions
dotnet add src/TeachHub.Infrastructure  package MediatR --version 12.5.0

dotnet add src/TeachHub.Api             package Microsoft.AspNetCore.OpenApi
dotnet add src/TeachHub.Api             package Scalar.AspNetCore
dotnet add src/TeachHub.Api             package Serilog.AspNetCore
dotnet add tests/TeachHub.Api.Tests     package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/TeachHub.Api.Tests     package Testcontainers.PostgreSql

dotnet add src/TeachHub.Infrastructure  package Hangfire.AspNetCore
dotnet add src/TeachHub.Infrastructure  package Hangfire.PostgreSql
dotnet add src/TeachHub.Infrastructure  package Newtonsoft.Json
dotnet add tests/TeachHub.Application.Tests package Microsoft.Extensions.TimeProvider.Testing
```

### Migration đầu tiên (Task 0.4)

Cần `ConnectionStrings:Default` trong `appsettings.Development.json` của Api trước.

```bash
dotnet tool install --global dotnet-ef --version 10.0.11   # nếu chưa có
dotnet ef migrations add Initial \
  -p src/TeachHub.Infrastructure -s src/TeachHub.Api -o Persistence/Migrations
dotnet ef database update -p src/TeachHub.Infrastructure -s src/TeachHub.Api
```

Xác minh ngay: `docker exec -it teachhub-db psql -U teachhub -d teachhub -c '\dt'` → phải thấy
`asp_net_users`, `asp_net_roles`, `refresh_tokens`. Nếu còn `AspNetUsers` PascalCase thì
`UseSnakeCaseNamingConvention()` chưa được gọi — sửa `AddPersistence`, `migrations remove`, sinh lại.

### Frontend (Task 0.11)

```bash
pnpm create next-app@latest frontend --ts --tailwind --app --src-dir --eslint --import-alias "@/*" --no-turbopack
cd frontend
pnpm dlx shadcn@latest init
pnpm dlx shadcn@latest add button input label card form sonner table dialog dropdown-menu
pnpm add @tanstack/react-query openapi-fetch react-hook-form @hookform/resolvers zod
pnpm add -D openapi-typescript
```

Script trong `frontend/package.json` (file này viết tay được, nó không do template sinh ra phần scripts):

```json
"scripts": {
  "gen:api": "openapi-typescript http://localhost:5080/openapi/v1.json -o src/lib/api/schema.d.ts"
}
```

`pnpm gen:api` cần API **đang chạy**. Kỳ vọng: sinh `src/lib/api/schema.d.ts` có type cho
`/api/auth/login`. File này nằm trong `.gitignore` — nó là sản phẩm sinh, không phải mã nguồn.

Gõ tay type/hàm fetch thay cho `pnpm gen:api` phá đúng giá trị của OpenAPI 3.1: mỗi lần đổi DTO
ở C#, client sinh lại sẽ báo lỗi TypeScript ngay chỗ dùng sai; client gõ tay thì im lặng và sai
ở runtime. Frontend gọi API qua BFF route handler với JWT trong httpOnly cookie (ADR-007) — route
handler tự viết, còn *type* thì sinh.

---

## Tự kiểm trước khi báo xong

- [ ] Mọi file khung trong diff đều do lệnh sinh ra, chỉ có sửa đè lên — không file nào được gõ mới.
- [ ] Đã chạy `git status` sau mỗi lệnh sinh và biết rõ từng file mới là gì.
- [ ] `dotnet build` → `0 Warning(s) 0 Error(s)`.
- [ ] Nếu có migration: đã **đọc** file sinh ra, không có `DropColumn`/`DropTable` ngoài ý muốn,
      cột enum ra `text` chứ không phải `integer`.
- [ ] Nếu có lệnh không chạy được: đã **báo lại**, không có file gõ tay nào thay thế nó.
