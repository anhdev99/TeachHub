# Phase 3 — Bài tập & Nộp bài · checklist

Ký hiệu: `- [ ]` chưa làm · `- [x]` xong · `- [!]` BỎ DỞ (ghi lý do vào báo cáo)

## Sai lệch so với plan — người dùng quyết định (2026-08-15)

Người dùng yêu cầu **không viết test** cho phase này để đi nhanh, thay bằng
**kiểm chứng trực tiếp trên UI thật** (Chromium điều khiển bằng Playwright).
Vì thế Definition of Done mục 1 và 3 KHÔNG áp dụng cho phase này.

**Một ngoại lệ giữ nguyên test: Task 3.3 (SafeZipReader).** Zip-slip và zip bomb
không có biểu hiện nào trên giao diện — bấm thử trên UI không phân biệt được bộ đọc
an toàn với bộ đọc thủng. Đây cũng là mục plan ghi rõ "nếu eo hẹp thì cắt tính năng
khác, tuyệt đối không cắt". Test của riêng task này chạy trong mili-giây.

## Chunk 0 — Vá khoảng trống Phase 2 (phát hiện khi chạy UI thật)

- [x] 3.0a Trang tiến độ trả về danh sách phần của bài + UI hoàn thành từng phần
- [ ] 3.0b Màn hình giảng viên cho lớp học phần (`/offerings`) — cửa vào của mọi màn hình Phase 3

## Chunk 1 — Bài tập & cấu hình format nộp

- [ ] 3.1 Assignment (mở rộng), Rubric, RubricCriterion, AssignmentHint, GroupConfig
- [ ] 3.2 SubmissionSlot động + SlotTemplates + RequiredStructure + CHECK constraint owner

## Chunk 2 — Nộp bài & xử lý file

- [ ] 3.3 SafeZipReader + ZipLimits + FileTree 🔒 **giữ test**
- [ ] 3.4 Submission, SubmissionFile, upload theo slot, lưu nháp / nộp chính thức
- [ ] 3.5 Pipeline xử lý file nền (Hangfire) + xem cây thư mục / nội dung entry

## Chunk 3 — Giao diện

- [ ] 3.6 Màn hình 6.4: cấu hình bài tập & slot (giảng viên)
- [ ] 3.7 Màn hình 6.6: trang nộp bài (sinh viên)

## Nghiệm thu (chạy tay trên UI thật)

- [ ] Tạo bài tập 3 slot bằng "Dùng mẫu gợi ý", sửa nhãn tiếng Việt
- [ ] Tạo bài tập thứ hai, xóa còn duy nhất 1 slot source code
- [ ] SV nộp ảnh trước → "Nộp chính thức" vẫn khóa, ghi rõ thiếu "Source code"
- [ ] Nộp thêm ZIP → nút bật, nộp thành công
- [ ] Xem cây thư mục ZIP, mở một file .tsx
- [ ] ZIP thiếu README.md → vẫn nộp được, checklist đỏ
- [ ] ZIP nén cả thư mục gốc (BaiTap1/README.md) → checklist xanh
- [ ] File .txt đổi đuôi .zip → bị từ chối
- [ ] Upload 30 MB → phản hồi < 2 giây
- [ ] Nộp lại lần hai → thấy 2 attempt, chỉ bản mới là hiện hành
- [ ] Tài khoản SV khác không mở được bài nộp
