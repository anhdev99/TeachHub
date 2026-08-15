# Phase 4a + 4b — Chấm điểm, Nhóm, Sổ điểm động · checklist

Ký hiệu: `- [x]` xong · `- [!]` bỏ dở (lý do ở báo cáo)

## Sai lệch so với plan — người dùng quyết định

Không viết integration test (yêu cầu của người dùng, để đi nhanh), thay bằng nghiệm thu
trực tiếp qua API thật + UI thật. **Ngoại lệ giữ nguyên test:** mọi hàm thuần tính điểm
(`GradebookCalculator`, `GradeAggregator`, `BonusLedger`, `GradeConditionEvaluator`,
`GroupAllocator`, `Rubric.Score`) — 121 test, chạy 110 ms. Một lỗi làm tròn ở đây không có
triệu chứng nào trên giao diện; nó chỉ hiện ra khi bảng điểm đã nộp cho phòng đào tạo.

## Phase 4a — Chấm điểm & Làm việc nhóm

- [x] 4a.1 StudentGroup, GroupMember, GroupAllocator (+7 test)
- [x] 4a.2 GradeSubmission, ReturnSubmission, GradeAuditLog (chỉ ghi thêm)
- [x] 4a.3 Màn hình 6.7 — trang chấm bài 3 cột, phím tắt Ctrl+Enter
- [x] 4a.4 FinalAssessment loại final_project, dùng lại nguyên vẹn slot của Phase 3
- [x] 4a.5 Màn hình 6.1 — dashboard giảng viên
- [x] Bổ sung ngoài plan: API + màn hình soạn rubric (4a cần rubric mới chấm được)

## Phase 4b — Sổ điểm động

- [x] 4b.1 GradeSchemeTemplate — thư viện bộ cột mẫu
- [x] 4b.2 Mở lớp = SAO CHÉP mẫu thành cột rồi cắt dây
- [x] 4b.3 GradeAggregator — 8 quy tắc gộp, chuẩn hóa mọi nguồn về thang 10
- [x] 4b.4 BonusLedger tất định + GradebookCalculator (+19 test)
- [x] 4b.5 GradeCondition — điều kiện điểm sàn
- [x] 4b.6 GradeColumn, GradeColumnSource, GradeRecord, GradeSnapshot + 4 CHECK constraint
- [x] 4b.7 Điểm cộng + thư viện lý do (ẩn chứ không xóa)
- [x] 4b.8 Sổ điểm + cấu hình cột + cấu hình điểm cộng/điểm sàn
- [x] 4b.9 Xuất bảng điểm theo mẫu tùy chỉnh (CSV có BOM UTF-8)
- [x] 4b.10 Màn hình 6.8 sổ điểm + popover điểm cộng dùng chung

## Nghiệm thu (chạy thật qua API + Chromium)

- [x] Chia nhóm ngẫu nhiên: cùng seed → cùng kết quả, không nhóm 1 người
- [x] Chấm theo rubric tính đúng tổng 8.0; vượt trần tiêu chí → 400
- [x] Chấm lại ghi GradeAuditLog đủ giá trị cũ → mới + người sửa + thời điểm
- [x] SV KHÔNG thấy điểm trước khi trả bài; thấy đủ điểm + nhận xét sau khi trả
- [x] Đổi 2 cột → 3 cột (20/30/50); tổng trọng số ≠ 100% bị chặn
- [x] Điểm bài tập chảy vào cột qua GradeColumnSource
- [x] Sửa tay ô điểm → ô có cờ manual override, chấm lại không ghi đè
- [x] Điều kiện cuối môn < 4 → tổng bị chặn ở 3.9
- [x] Điểm cộng thẳng điểm tổng KHÔNG kéo qua ngưỡng điểm sàn
- [x] Trần điểm cộng 1đ áp một lần trên toàn bộ (cấp 2đ chỉ ăn 1đ)
- [x] Điểm cộng âm → 400; cộng vào cột mà không chọn cột → 400
- [x] Thư viện lý do: ẩn rồi vẫn còn khi include_hidden=true
- [x] Xuất CSV mặc định và theo mẫu tùy chỉnh (MSSV,Họ và tên,Thi,TK)
- [x] 5 màn hình mở được trên Chromium, không lỗi console
