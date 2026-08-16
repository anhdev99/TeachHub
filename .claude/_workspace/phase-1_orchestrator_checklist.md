# Phase 1 — Nội dung dạy học & Versioning · Checklist thi công

Nguồn: `docs/superpowers/plans/2026-08-13-phase-1-content-versioning.html`
Bắt đầu: 2026-08-15 · Nền: Phase 0 xong (53 test xanh, nhánh `phase-0-foundation`)

Ký hiệu: `- [ ]` chưa làm · `- [x]` xong theo DoD · `- [!]` BỎ DỞ (ghi lý do vào báo cáo)

## Chunk 1 — Môn học & phiên bản

- [x] **Task 1.1** — Entity `Subject`, `SubjectVersion`, `Syllabus` + máy trạng thái draft→published→archived
- [x] **Task 1.2** — `Chapter`, `Lesson`, `LessonComponent`, `ComponentConfig` + reorder giữ order_index liên tục
- [x] **Task 1.3** — Ánh xạ EF Core (jsonb qua ToJson, enum chuỗi, soft delete filter) + migration `ContentModel`

## Chunk 2 — API nội dung & cơ chế versioning

- [x] **Task 1.4** — Controller Subject & SubjectVersion (create/list/get/versions/publish)
- [x] **Task 1.5** — Controller Chapter/Lesson/Component + reorder + action filter `[RequireDraftVersion]`
- [x] **Task 1.6** — ⭐ `SubjectVersionCloner` deep clone + remap `ref_id` (task quan trọng nhất của cả dự án)

## Chunk 3 — Giao diện giảng viên

- [x] **Task 1.7** — Upload nội dung bài học + `FileSignatureValidator` (magic bytes)
- [x] **Task 1.8** — Màn hình 6.2: trình soạn thảo chương (kéo-thả, optimistic update)
- [x] **Task 1.9** — Màn hình 6.10: lịch sử phiên bản

## Bắt buộc theo Definition of Done mục 7

- [x] Thêm dòng vào `CrossInstructorIsolationTests` cho: `subjects`, `subject-versions`, `chapters`, `lessons`, `lesson-components`

## Nghiệm thu Phase 1 (thao tác tay trên UI)

- [x] 1. Tạo môn "IN4203 — Lập trình di động React Native"
- [x] 2. Version `2026-2027`, tạo 2 chương
- [x] 3. Chương 1 có 2 bài: bài 1 gắn assignment + quiz + reflection; **bài 2 không gắn gì** — vẫn hợp lệ
- [x] 4. Upload 1 PPT và 1 PDF, xem trước được trong trình duyệt — HOÀN TẤT ở Phase 2: endpoint `GET /api/files/{**path}` (có kiểm quyền, 9 test ở `LessonFileAccessTests`), PDF xem trực tiếp bằng `<object>`, PPT tải về (quyết định bỏ LibreOffice, xem báo cáo Phase 2)
- [x] 5. Kéo-thả đổi thứ tự bài và component, tải lại thấy thứ tự đã lưu
- [x] 6. Xuất bản version → mọi nút sửa `disabled`
- [x] 7. Tạo version `2027-2028` từ nó → đủ 2 chương, đúng thứ tự, đúng component
- [x] 8. **Xóa một chương ở bản 2027-2028 → bản 2026-2027 vẫn còn nguyên**
- [x] 9. Xuất bản `2027-2028` → `2026-2027` tự thành `archived`, vẫn xem chỉ đọc được
- [x] 10. Trang lịch sử phiên bản hiện đủ 2 mốc với badge trạng thái đúng
