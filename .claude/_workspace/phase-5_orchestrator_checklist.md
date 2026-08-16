# Phase 5 — Trắc nghiệm (Quiz engine) · checklist

Ký hiệu: `- [x]` xong · `- [!]` bỏ dở (lý do ở báo cáo)

## Lưu ý về plan

Phase 5 nằm trong file backlog (`phases-5-9-backlog.html`) ở mức PHẠM VI, không có plan
từng bước như Phase 0–4b. Thiết kế chi tiết do tôi tự dựng theo quy ước dự án; các quyết
định phát sinh ghi ở báo cáo.

Vẫn theo yêu cầu "không viết integration test", giữ test cho hàm thuần chấm điểm:
`QuizGraderTests` 24 test, chạy 34 ms.

## Phạm vi plan yêu cầu

- [x] Ngân hàng câu hỏi cấp giảng viên, gắn foundation_tag, tái sử dụng qua nhiều môn
- [x] 4 loại câu: single_choice, multiple_choice, true_false, fill_blank
- [x] Quiz có subject_version_id HOẶC offering_id + CHECK "đúng một trong hai" (Gap #3)
- [x] Checkpoint quiz (không tính điểm) và graded quiz
- [x] Giới hạn thời gian, số lần làm, xáo trộn câu hỏi và đáp án
- [x] Chấm tự động + QuizAttempt lưu answers (jsonb) và wrong_question_ids
- [x] FinalAssessment loại quiz — mở khóa nhánh đã chặn ở Phase 4a
- [x] Điểm quiz chảy vào sổ điểm qua GradeSourceType.Quiz, chuẩn hóa về thang 10
- [!] Component `quiz` và `checkpoint_quiz` hoạt động TRONG bài học — xem báo cáo

## Chỗ khó plan nêu đích danh

- [x] Chấm fill_blank tiếng Việt: chuẩn hóa NFC bắt buộc, cả đáp án mẫu lẫn bài làm
- [x] Hết giờ giữa chừng: máy chủ là nguồn chân lý, ân hạn 30 giây, chấm phần đã tự lưu
- [x] Mất mạng giữa bài: tự lưu sau mỗi thay đổi (PUT answers), không đợi lúc nộp
- [x] Xáo trộn nhưng chấm đúng: đáp án gửi kèm id thật, không dùng chỉ số vị trí
- [x] multiple_choice: mặc định tất-cả-hoặc-không, cấu hình được theo từng câu

## Nghiệm thu

- [x] Tạo quiz đủ 4 loại câu, giới hạn 15 phút, 2 lượt, xáo trộn
- [x] Tắt trình duyệt giữa chừng rồi vào lại — đáp án đã trả lời vẫn còn
- [x] Nộp bài chấm tự động, quy về thang 10 đúng (4/4 → 10)
- [x] Lượt thứ ba bị chặn khi max_attempts = 2
- [x] Câu "Hà Nội" chấm đúng cả khi gõ "hà nội" và "  Hà   Nội "
- [x] Điểm quiz chạy đúng vào cột sổ điểm (Trắc nghiệm = 10.0)
- [x] Đề KHÔNG lộ đáp án đúng trước khi nộp; sau khi nộp mới hiện giải thích
- [x] Quiz thuộc cả hai chủ sở hữu bị chặn (Gap #3)
