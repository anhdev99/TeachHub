# Phase 0 — Nền móng · Checklist thi công

Nguồn: `docs/superpowers/plans/2026-08-13-phase-0-foundation.html`
Bắt đầu: 2026-08-14 · Trạng thái repo lúc bắt đầu: chưa có `backend/`, chưa có `frontend/`

Ký hiệu: `- [ ]` chưa làm · `- [x]` xong theo DoD · `- [!]` BỎ DỞ (ghi lý do vào báo cáo)

## Chunk 1 — Bộ khung solution & hạ tầng local

- [x] **Task 0.1** — Khởi tạo repo, solution, quy ước build (`dotnet new` gitignore/editorconfig/globaljson/buildprops/packagesprops/sln + 4 src + 3 test project)
- [x] **Task 0.2** — Base entity Domain + Id UUIDv7 (`Entity.cs`, `ISoftDeletable`, `IAuditable`)
- [x] **Task 0.3** — Hạ tầng local: Postgres 17 + MinIO (`infra/docker-compose.yml`)

## Chunk 2 — Dữ liệu, xác thực, và host API

- [x] **Task 0.4** — Entity Identity & DbContext (+ migration `Initial`, xác minh tên bảng snake_case)
- [x] **Task 0.5** — Host API: Serilog, ProblemDetails, OpenAPI 3.1, health check (+ `TeachHubApiFactory`)
- [x] **Task 0.6** — MediatR + pipeline behaviors (Logging → Validation → Transaction), `Result`, `ICommand`/`IQuery`
- [ ] **Task 0.7** — Đăng nhập & phát hành JWT (`TokenService`, `ApiControllerBase`, `AuthController`, seed)
- [ ] **Task 0.8** — Refresh token xoay vòng & phát hiện tái sử dụng
- [ ] **Task 0.8b** — Mời & tạo tài khoản giảng viên (`InstructorInvite`, bootstrap, register)
- [ ] **Task 0.8c** — ⭐ Ma trận test cách ly dữ liệu giữa các giảng viên (`CrossInstructorIsolationTests`)

## Chunk 3 — Job nền, lưu trữ file, frontend, CI

- [ ] **Task 0.9** — Hangfire (Postgres storage, queue `zip` riêng, dashboard có bảo vệ)
- [ ] **Task 0.10** — Trừu tượng lưu trữ file (local + S3/MinIO) qua bộ test hợp đồng chung
- [ ] **Task 0.11** — Frontend Next.js 15 + client TS sinh từ OpenAPI + BFF cookie httpOnly
- [ ] **Task 0.12** — CI trên GitHub Actions

## Nghiệm thu Phase 0 (thao tác tay, không phải test tự động)

- [ ] 1. `cd infra && docker compose up -d` → 2 container `healthy`
- [ ] 2. `dotnet run --project backend/src/TeachHub.Api` → API sống, migration tự chạy, seed tạo tài khoản
- [ ] 3. `http://localhost:5080/scalar/v1` → duyệt được toàn bộ endpoint
- [ ] 4. `GET /health` → **200, không phải 401**
- [ ] 5. `POST /api/auth/login` đúng → JSON có khóa `access_token` (snake_case)
- [ ] 6. `POST /api/auth/login` email sai định dạng → 400 `application/problem+json` có khóa `errors`
- [ ] 7. Log có dòng `LoggingBehavior` kèm tên command + số mili-giây
- [ ] 8. `http://localhost:3000/login` → vào dashboard; cookie `th_at` có cờ HttpOnly
- [ ] 9. `dotnet test backend/TeachHub.sln` → toàn bộ xanh, 0 cảnh báo
- [ ] 10. `grep -r "MediatR" backend/Directory.Packages.props` → vẫn đúng `12.5.0`
- [ ] 11. CI trên GitHub xanh cả 2 job
