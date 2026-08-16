# Phase 6 + 7 + 8 · checklist

Ký hiệu: `- [ ]` chưa làm · `- [x]` xong · `- [!]` BỎ DỞ (ghi lý do vào báo cáo)

## Cách điều phối lần này

Người dùng yêu cầu chia agent và làm hết, không dừng lại hỏi. Ba phase này **độc lập về
nghiệp vụ** nên chạy song song được:

| Phase | Phụ thuộc | Chạy song song? |
|---|---|---|
| 6 — Liêm chính & chấm nâng cao | Phase 3 (đã xong) | ✔ |
| 7 — Gamification | không phụ thuộc gì | ✔ (plan ghi rõ "chạy song song hoàn toàn") |
| 8 — Ôn tập ngắt quãng | Phase 5 (đã xong) | ✔ |

**Điểm phải nối tiếp, không được song song: MIGRATION.** EF migration dùng chung
`AppDbContextModelSnapshot`; ba agent cùng chạy `dotnet ef migrations add` là hỏng snapshot
chắc chắn, và migration kế tiếp sẽ sinh lệnh sai trên DB đã có điểm thật. Vì thế mỗi agent
bị cấm chạm `AppDbContext`/`IAppDbContext` và cấm sinh migration; orchestrator gom lại làm
MỘT migration sau khi cả ba xong.

Vẫn theo yêu cầu "không viết integration test" — giữ test cho hàm thuần.

## Phase 6 — Liêm chính học thuật & Chấm nâng cao

- [x] 6a.1 `SourceNormalizer` — bỏ chú thích, bỏ khoảng trắng, đổi mọi định danh thành một token
- [x] 6a.2 Winnowing: k-gram (k=5) → rolling hash → chọn cửa sổ (w=4)
- [x] 6a.3 `SourceFingerprint` + index trên hash (Gap #14)
- [x] 6a.4 So sánh trong phạm vi cùng bài tập, cùng lớp; ngưỡng cấu hình được
- [x] 6a.5 CẢNH BÁO, tuyệt đối không tự trừ điểm (Rủi ro #3)
- [x] 6b.1 `HintUnlock` — mở gợi ý trừ điểm, ghi log (Gap #11)
- [x] 6b.2 Phạt nộp trễ qua `TimeProvider`, không đẩy điểm xuống âm, trễ 1 phút = 1 ngày
- [x] 6b.3 `SubmissionMemberScore` — điểm riêng thành viên thắng thế điểm chung (UC-10b)

## Phase 7 — Gamification

- [x] 7.1 `XpRule` cấu hình được, không hard-code con số
- [x] 7.2 Huy hiệu điều kiện khai báo dạng dữ liệu
- [x] 7.3 `StreakCalculator` theo múi giờ của lớp — ca vượt nửa đêm phải đúng
- [x] 7.4 Bảng xếp hạng ẩn danh MẶC ĐỊNH, bật tên thật theo từng lớp
- [x] 7.5 API tiến độ cá nhân

## Phase 8 — Ôn tập ngắt quãng

- [x] 8.1 `ReviewSchedule`: 3 ngày → 1 tuần → 2 tuần → mastered; sai thì về đầu chu kỳ
- [x] 8.2 `ReviewQueueItem` gắn `user_id` KHÔNG phải `enrollment_id` (Gap #2)
- [x] 8.3 Sinh mục ôn tập từ `QuizAttempt.WrongQuestionIds`
- [x] 8.4 Hangfire recurring job quét hằng ngày
- [x] 8.5 API "Ôn tập hôm nay"
- [x] 8.6 Component `recap` chọn 3–5 câu theo `foundation_tags`

## Việc của orchestrator sau khi ba agent xong

- [x] Khai DbSet vào `IAppDbContext` + `AppDbContext`
- [x] MỘT migration gom cả ba phase; đọc file sinh ra, kiểm không có `Drop` ngoài ý muốn
- [x] Apply lên DB đang có dữ liệu thật
- [x] `dotnet build` 0 cảnh báo · toàn bộ test Domain xanh
- [x] Sinh lại client TS · giao diện cho ba phase
- [x] Nghiệm thu trên Chromium thật
- [x] Báo cáo chốt

## Không thi công: Phase 9+

Plan ghi rõ **"P2 — chỉ lập kế hoạch chi tiết khi đã tới lúc"**, và phần lớn hạng mục cần
quyết định nghiệp vụ trước khi code (điểm danh QR chưa có đặc tả — Gap #13; chấm code tự động
cần sandbox thực thi code không tin cậy). Xây bây giờ là đoán yêu cầu.
