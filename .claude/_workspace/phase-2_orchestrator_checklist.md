# Phase 2 — Lớp học phần & Trải nghiệm học · Checklist thi công

Nguồn: `docs/superpowers/plans/2026-08-13-phase-2-offering-enrollment.html`
Bắt đầu: 2026-08-15 · Nền: Phase 0 + Phase 1 xong (136 test xanh)

Ký hiệu: `- [ ]` chưa làm · `- [x]` xong theo DoD · `- [!]` BỎ DỞ (ghi lý do vào báo cáo)

## Chunk 1 — Lớp học phần & danh sách sinh viên

- [x] **Task 2.1** — `CourseOffering` + `JoinCode` + ghim `subject_version_id` cứng khi mở lớp
- [x] **Task 2.2** — Import danh sách SV từ Excel, báo lỗi theo từng dòng, nhập được bao nhiêu nhập bấy nhiêu
- [x] **Task 2.3** — Kích hoạt tài khoản SV (join bằng mã lớp, đổi mật khẩu lần đầu, middleware chặn)

## Chunk 2 — Tiến độ học & engine mở khóa

- [x] **Task 2.4** — ⭐ `UnlockEngine` + `LessonProgress`/`ComponentProgress` (bảng SRS còn thiếu)

## Chunk 3 — Trang học của sinh viên

- [x] **Task 2.5** — Màn hình 6.3: bố cục trang học, bài khoá nêu rõ lý do, responsive
- [x] **Task 2.6** — Xem trước PDF trong trình duyệt + danh sách nguồn video được phép nhúng

## Bắt buộc theo Definition of Done mục 7

- [x] Thêm dòng vào `CrossInstructorIsolationTests`: `offerings`, `enrollments`, `my-progress`

## Nghiệm thu Phase 2

- [x] 1. Mở lớp DA24TIN01 từ môn đã xuất bản; lớp ghim đúng version
- [x] 2. Import Excel 40 SV có 2 dòng trùng MSSV + 1 dòng thiếu họ tên → nhập 37, liệt kê đúng 3 dòng lỗi
- [x] 3. Sửa 3 dòng rồi import lại → đủ 40, không ai trùng
- [x] 4. SV đăng ký + nhập mã lớp vào được; người không có trong danh sách nhập đúng mã vẫn bị từ chối
- [x] 5. Chương 1 `after_previous_completed`, bài 1 bắt buộc → SV thấy bài 2 khoá kèm lời giải thích
- [x] 6. Học xong bài 1 → bài 2 mở, % hoàn thành cập nhật
- [x] 7. Trang học trên điện thoại đọc được, không cuộn ngang — sidebar thu vào drawer dưới 768px
- [x] 8. Xem trước PDF trong trình duyệt, không phải tải về
