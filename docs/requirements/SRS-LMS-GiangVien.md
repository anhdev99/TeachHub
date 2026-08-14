# TÀI LIỆU ĐẶC TẢ HỆ THỐNG (SRS)
## Hệ thống Quản lý Dạy học Cá nhân — "TeachHub"

| | |
|---|---|
| **Phiên bản** | 1.0 |
| **Ngày** | 13/08/2026 |
| **Chủ đầu tư / Người dùng chính** | Giảng viên (chủ hệ thống) |
| **Phạm vi** | Quản lý môn học, lớp học phần, bài giảng, bài tập (nộp ZIP), trắc nghiệm, sổ điểm |

> **Tài liệu đi kèm (đọc cùng SRS này):**
> - [`docs/architecture/00-tech-stack-decisions.html`](../architecture/00-tech-stack-decisions.html) — kiến trúc kỹ thuật đang có hiệu lực (**.NET 10**), các quyết định ADR, và **phân tích 18 lỗ hổng của SRS này** kèm cách xử lý.
> - [`docs/superpowers/plans/2026-08-13-teachhub-roadmap.html`](../superpowers/plans/2026-08-13-teachhub-roadmap.html) — lộ trình triển khai theo phase và plan chi tiết từng phase.
>
> Khi SRS và tài liệu kiến trúc mâu thuẫn **về mặt kỹ thuật**, lấy tài liệu kiến trúc. SRS vẫn là nguồn chân lý về **nghiệp vụ**.

---

## MỤC LỤC

1. Giới thiệu chung
2. Vai trò & quyền hạn
3. Mô hình dữ liệu chi tiết
4. Danh sách Use Case
5. Đặc tả chức năng theo module
6. Đặc tả màn hình chính
7. Thiết kế API
8. Kiến trúc kỹ thuật
9. Yêu cầu phi chức năng
10. Lộ trình triển khai & Backlog theo Sprint
11. Rủi ro & phương án

---

## 1. GIỚI THIỆU CHUNG

### 1.1 Bối cảnh
Giảng viên hiện quản lý bài giảng, bài tập, điểm số rải rác qua nhiều công cụ (Drive, Zalo, Excel), dẫn đến khó theo dõi tiến độ sinh viên (SV), dễ sai sót khi chấm điểm và cộng điểm.

### 1.2 Mục tiêu hệ thống
- Tập trung hóa toàn bộ nội dung giảng dạy theo **môn học → chương → bài học**.
- Tổ chức lớp học theo **học kỳ** với danh sách SV rõ ràng, tránh nhầm lẫn giữa các đợt học.
- Số hóa việc **nộp bài (chỉ nhận file ZIP source code)**, chấm điểm theo rubric.
- Tăng **hứng thú học tập** và **khắc sâu kiến thức nền tảng** qua cấu trúc chương nhiều phần và cơ chế ôn tập ngắt quãng (spaced repetition).
- Tự động hóa **sổ điểm**, xuất báo cáo đúng mẫu nhà trường.
- Cho phép **cập nhật nội dung dạy học theo từng năm học** mà vẫn giữ lại lịch sử các phiên bản trước.

### 1.3 Phạm vi (Scope)
**Trong phạm vi:**
- Quản lý môn học, đề cương, chương, bài học (PPT/PDF/video-link).
- Quản lý lớp học phần theo học kỳ, import danh sách SV.
- Bài tập trong chương: nộp bài theo **format slot cấu hình động** (source code, hình ảnh, video...), hỗ trợ cá nhân hoặc nhóm, chấm theo rubric, phát hiện trùng lặp cơ bản với slot source code.
- Trắc nghiệm tự chấm.
- **Đợt chấm điểm cuối môn** (cấp lớp học phần): Trắc nghiệm hoặc Bài tập lớn với format nộp nhiều loại file (báo cáo, trình bày, source code, hình ảnh, video) — không triển khai loại Thi ở giai đoạn này.
- Sổ điểm có trọng số, điểm cộng, xuất Excel.
- Quản lý phiên bản nội dung dạy học theo năm/học kỳ (tạo bản mới, xuất bản, xem lịch sử).
- Cơ chế tạo động lực (XP, huy hiệu, streak) và ôn tập ngắt quãng.

**Ngoài phạm vi (giai đoạn này):**
- Chấm code tự động bằng test case (để P2).
- Video call / dạy trực tuyến real-time.
- Ứng dụng di động riêng (dùng web responsive trước).

### 1.4 Đối tượng người đọc
Giảng viên (chủ hệ thống, ra quyết định nghiệp vụ) và lập trình viên triển khai (chính giảng viên hoặc người được thuê).

---

## 2. VAI TRÒ & QUYỀN HẠN

| Vai trò | Mô tả | Quyền chính |
|---|---|---|
| **Instructor** (Giảng viên) | Chủ sở hữu nội dung | Tạo/sửa/xóa môn học, chương, bài học, bài tập, quiz; tạo lớp học phần; import SV; chấm điểm; cấu hình sổ điểm; xuất báo cáo |
| **Teaching Assistant** (Trợ giảng) ⚠️ **hoãn sang P2** | Hỗ trợ chấm bài | Xem nội dung, chấm bài theo rubric có sẵn, **không** được sửa cấu trúc môn/chương.<br>⚠️ Vai trò được khai báo sẵn nhưng **chưa cấp quyền gì ở MVP**: chưa có bảng gắn trợ giảng vào một lớp cụ thể, nên hoặc họ không làm được gì, hoặc làm được ở *mọi* lớp của *mọi* giảng viên. Bật lại cần bảng `OfferingStaff` — xem [ADR-013](../architecture/00-tech-stack-decisions.html). |
| **Student** (Sinh viên) | Người học | Xem bài giảng (theo tiến độ mở khóa), làm quiz, nộp bài ZIP, xem điểm & nhận xét của mình |

> **Nhiều giảng viên (chốt 13/08/2026):** hệ thống hỗ trợ **nhiều giảng viên độc lập** — mỗi người một khoang riêng, dữ liệu cách ly tuyệt đối, không cộng tác và không đồng sở hữu môn. Tài khoản tạo bằng **lời mời**, không mở đăng ký công khai; người đầu tiên tạo qua `POST /api/auth/bootstrap` chạy đúng một lần. Xem [ADR-012](../architecture/00-tech-stack-decisions.html).

**Nguyên tắc phân quyền:** Instructor có toàn quyền trên môn học do mình tạo; SV chỉ thấy dữ liệu của **lớp học phần mình đang tham gia**, không thấy dữ liệu SV khác (trừ bảng xếp hạng ẩn danh nếu bật).

---

## 3. MÔ HÌNH DỮ LIỆU CHI TIẾT

### 3.1 Sơ đồ quan hệ (mức khái niệm)

```
Instructor 1---N Subject 1---N SubjectVersion (draft/published/archived, theo năm)
                                      |
                                      1---N Chapter 1---N Lesson 1---N LessonComponent (0..N, tùy chọn)
                                                                        |
                                                              type=assignment → Assignment
                                                                                   1---N SubmissionSlot (format nộp động)
                                                              type=quiz       → Quiz 1---N QuizQuestion
                                                              type=recap/hook/checkpoint_quiz/
                                                                   code_along/reflection/custom_note
                                                                   → nội dung nhúng trong config_json

CourseOffering (Lớp học phần theo học kỳ) --- tham chiếu cố định tới 1 SubjectVersion
                    1---N Enrollment (SV trong lớp)
                    1---N StudentGroup 1---N GroupMember (khi bài tập làm nhóm)
                    1---N GradeColumn (Cột điểm, trọng số)
                    1---N FinalAssessment (Đợt chấm điểm cuối môn)
                              type=quiz          → Quiz (dùng lại engine Quiz)
                              type=final_project → SubmissionSlot (format nộp động, dùng chung cơ chế với Assignment)
                    1---N Submission (owner_type = assignment | final_assessment, submitted_by = cá nhân hoặc nhóm)
                              1---N SubmissionFile
                              0..N SubmissionMemberScore (điểm riêng từng thành viên nhóm, tùy chọn)
                    1---N QuizAttempt
                    1---N BonusPoint
                    1---N Attendance
                    1---N ReviewQueueItem (câu hỏi cần ôn lại)
```

### 3.2 Chi tiết các bảng chính

#### `Subject` (Môn học — vỏ ngoài cố định, dùng lại nhiều học kỳ)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| code | string | VD: IN4203 |
| name | string | VD: Lập trình di động React Native |
| description | text | |
| instructor_id | UUID | FK → Instructor |
| current_version_id | UUID (nullable) | Trỏ tới `SubjectVersion` đang là bản mới nhất đã xuất bản (dùng làm mặc định khi tạo lớp mới) |
| created_at | timestamp | |

#### `SubjectVersion` (Phiên bản nội dung dạy học theo năm/học kỳ — **cốt lõi cơ chế versioning**)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| subject_id | UUID | FK → Subject |
| version_label | string | VD: `2025-2026`, `2026-2027 (v2)` — tự đặt, không bắt buộc theo mẫu cố định |
| status | enum | `draft` (đang soạn) \| `published` (đã dùng để dạy) \| `archived` (phiên bản cũ, chỉ đọc) |
| syllabus_json | jsonb | CĐR, số tín chỉ, số tiết, tài liệu tham khảo — **snapshot theo từng version** |
| cloned_from_version_id | UUID (nullable) | Version nguồn khi bấm "Tạo phiên bản mới từ bản hiện tại" |
| changelog | text | Giảng viên tự ghi chú thay đổi so với version trước (VD: "Cập nhật chương 5 theo React Native mới, bỏ chương Redux") |
| published_at | timestamp (nullable) | Thời điểm chuyển từ `draft` → `published` |
| created_at | timestamp | |

> **Nguyên tắc:** toàn bộ nội dung dạy học thực sự (`Chapter`, `Lesson`, `LessonComponent`, `Assignment`, `Quiz` dùng trong bài giảng) đều gắn với **`SubjectVersion`**, không gắn thẳng vào `Subject`. Khi giảng viên muốn cập nhật nội dung cho năm học mới: bấm **"Tạo phiên bản mới"** → hệ thống clone toàn bộ Chapter/Lesson/Component từ version hiện tại sang 1 `SubjectVersion` mới ở trạng thái `draft` → giảng viên chỉnh sửa thoải mái mà **không ảnh hưởng version cũ** → khi xong bấm **"Xuất bản"** → version cũ tự chuyển `archived` (vẫn xem được, chỉ không dùng cho lớp mới). Các lớp học phần (`CourseOffering`) đã tạo từ trước **giữ nguyên tham chiếu tới version tại thời điểm mở lớp**, nên sửa nội dung năm sau không làm thay đổi dữ liệu của các lớp đã dạy trước đó.

#### `Chapter` (Chương — thuộc về 1 SubjectVersion cụ thể)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| subject_version_id | UUID | FK → SubjectVersion |
| title | string | |
| order_index | int | Thứ tự sắp xếp |
| objective | text | Mục tiêu chương |
| foundation_tags | string[] | Các khái niệm nền tảng của chương (dùng cho ôn tập ngắt quãng) |
| unlock_rule | enum | `free` / `after_previous_completed` — áp dụng ở mức Lesson (xem `is_required_to_unlock_next`), Chapter chỉ quyết định có mở khóa toàn chương hay không |

#### `Lesson` (Bài học — đơn vị nội dung trong chương, số lượng tùy chương, KHÔNG ràng buộc gì bắt buộc)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| chapter_id | UUID | FK |
| order_index | int | Thứ tự trong chương |
| title | string | |
| content_type | enum | `ppt` \| `pdf` \| `video_embed` \| `text` \| `interactive` |
| file_url | string | Với ppt/pdf/video |
| duration_minutes | int | Ước lượng thời gian học |
| is_required_to_unlock_next | boolean | Có bắt buộc hoàn thành để mở bài tiếp theo không (giảng viên tự bật/tắt **cho từng bài**) |

> **Nguyên tắc cốt lõi (thay cho khuôn 8-phần cố định trước đây):** một chương gồm N `Lesson` theo bất kỳ số lượng nào giảng viên muốn (1, 2, 5...). Mỗi `Lesson` có thể gắn **0 hoặc nhiều** `LessonComponent` — hoàn toàn không bắt buộc. Ví dụ: Chương X có 2 bài → Bài 1 có kèm Assignment + Quiz, Bài 2 chỉ có nội dung học, không có gì kèm theo. Việc này cấu hình bằng cách **thêm/bớt component** cho từng Lesson, không có schema cứng quy định "chương phải có đủ 8 phần".

#### `LessonComponent` (Khối gắn kèm — động, 0..N cho mỗi Lesson)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| lesson_id | UUID | FK → Lesson |
| type | enum | `recap` \| `hook` \| `checkpoint_quiz` \| `code_along` \| `assignment` \| `quiz` \| `reflection` \| `custom_note` |
| order_index | int | Vị trí hiển thị trong bài (trước/sau nội dung chính) |
| ref_id | UUID (nullable) | Trỏ tới bản ghi chi tiết tương ứng — `Assignment.id` nếu type=`assignment`, `Quiz.id` nếu type=`quiz`, hoặc null nếu type mang nội dung nhúng trực tiếp (VD `reflection`, `custom_note`) |
| config_json | jsonb | Cấu hình riêng của từng loại (VD: `recap` → số câu random lấy từ chương nào; `checkpoint_quiz` → danh sách câu hỏi nhanh) |
| is_required | boolean | Có bắt buộc hoàn thành component này để đánh dấu Lesson "hoàn thành" không |

> **Cách hoạt động:** khi tạo 1 Lesson, giảng viên bắt đầu từ nội dung trống, rồi **thêm từng component tùy ý** qua nút "+ Thêm phần" (chọn loại: Bài tập / Trắc nghiệm / Ôn lại đầu bài / Code-along / Suy ngẫm cuối bài...). Không thêm gì thì Lesson chỉ đơn thuần là nội dung học. Đây chính là cơ chế đáp ứng yêu cầu "chương có 2 bài, 1 bài có bài tập+trắc nghiệm, bài kia không có gì".

#### `Assignment` (Bài tập — gắn qua `LessonComponent` type=`assignment`, KHÔNG khóa cứng vào 1 kiểu nộp bài)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| title, description | string/text | |
| submission_mode | enum | `individual` \| `group` — **cấu hình theo từng bài tập**, không phải setting toàn hệ thống |
| group_config_json | jsonb (nullable) | Khi `submission_mode=group`: số thành viên tối thiểu/tối đa, ai được tạo nhóm (SV tự tạo hay giảng viên chia), có cho đổi nhóm giữa chừng không |
| due_at | timestamp | |
| allow_late_submission | boolean | |
| late_penalty_percent_per_day | decimal | |
| hints | jsonb[] | Danh sách gợi ý mở dần, mỗi hint có `penalty_point` |
| rubric_id | UUID | FK → Rubric |
| max_score | decimal | |

> **Không còn field `submission_type` khóa cứng "zip".** Định dạng nộp bài (source code / hình ảnh / video / báo cáo...) giờ được khai báo qua **`SubmissionSlot`** — dùng chung 1 cơ chế với Bài tập lớn cuối môn (mục dưới), thay vì có 2 hệ thống nộp bài riêng biệt.

#### `Rubric` & `RubricCriterion`
| Trường | Kiểu | Ghi chú |
|---|---|---|
| rubric.id | UUID | PK |
| rubric.name | string | |
| criterion.description | string | VD: "Xử lý lỗi hợp lý" |
| criterion.max_points | decimal | |
| criterion.levels | jsonb | Thang mô tả từng mức điểm (VD: Xuất sắc/Khá/Trung bình/Yếu) |

#### `SubmissionSlot` (Cấu hình format nộp — **động, dùng chung cho cả Assignment trong chương lẫn Bài tập lớn cuối môn**)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| owner_type | enum | `assignment` \| `final_assessment` — slot này thuộc về loại nào |
| owner_id | UUID | FK → `Assignment.id` hoặc `FinalAssessment.id` tùy `owner_type` |
| order_index | int | Thứ tự hiển thị |
| slot_key | string | Tự đặt, VD: `source_code`, `images`, `video_demo`, `report` — không giới hạn danh sách cứng |
| label | string | Nhãn hiển thị cho SV, VD: "Source code (nén ZIP)" |
| accepted_file_types | string[] | VD: `[".zip"]`, `[".jpg",".png"]`, `[".mp4"]`, `[".pdf",".docx"]` |
| allow_link_instead_of_file | boolean | Cho phép dán link thay vì upload (thường dùng cho video) |
| max_files | int | Cho phép nộp nhiều file cùng slot (VD nhiều ảnh minh chứng) |
| max_file_size_mb | int | |
| is_required | boolean | Slot có bắt buộc phải nộp không |
| required_structure_json | jsonb (nullable) | Chỉ áp dụng khi slot chấp nhận `.zip` — VD bắt buộc có `README.md`, thư mục `src/` |

> **Mặc định gợi ý khi tạo Assignment mới** (giảng viên tự sửa/xóa/thêm, không bắt buộc): Source code (`.zip`, bắt buộc, có kiểm tra cấu trúc), Hình ảnh minh chứng (`.jpg/.png`, nhiều file, tùy chọn), Video minh chứng (`.mp4` hoặc link, tùy chọn). Bài tập đơn giản (chỉ cần nộp code) thì giảng viên chỉ giữ lại 1 slot `source_code`, xóa các slot còn lại — hoàn toàn linh hoạt theo từng bài, đúng như ví dụ "1 bài kèm hình ảnh+video, bài kia chỉ cần source".

#### `StudentGroup` & `GroupMember` (Nhóm sinh viên — dùng khi Assignment có `submission_mode=group`)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| group.id | UUID | PK |
| group.offering_id | UUID | FK → CourseOffering |
| group.name | string | VD: "Nhóm 3" |
| group.created_by | enum | `instructor` \| `student` — theo `group_config_json` của Assignment |
| member.group_id | UUID | FK |
| member.enrollment_id | UUID | FK → Enrollment |
| member.role | enum | `leader` \| `member` |

> Một `StudentGroup` có thể được tái sử dụng cho nhiều Assignment khác nhau trong cùng lớp (không bắt buộc tạo nhóm mới mỗi bài), hoặc giảng viên/SV có thể tạo nhóm riêng cho từng bài — tùy `group_config_json`.

#### `Submission` & `SubmissionFile` (Bài nộp — **dùng chung cho Assignment trong chương lẫn Bài tập lớn cuối môn**)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| submission.id | UUID | PK |
| submission.owner_type | enum | `assignment` \| `final_assessment` |
| submission.owner_id | UUID | FK tương ứng |
| submission.offering_id | UUID | FK → CourseOffering |
| submission.submitted_by_type | enum | `individual` \| `group` |
| submission.student_id | UUID (nullable) | Khi `submitted_by_type=individual` |
| submission.group_id | UUID (nullable) | Khi `submitted_by_type=group` — điểm/nhận xét áp dụng chung cho cả nhóm theo mặc định |
| submission.submitted_at | timestamp | |
| submission.is_late | boolean | |
| submission.hints_used | int | Chỉ có ý nghĩa với Assignment có cấu hình hint |
| submission.status | enum | `submitted` \| `grading` \| `graded` \| `returned` |
| submission.score | decimal | Điểm mặc định áp dụng cho cá nhân hoặc cả nhóm |
| submission.rubric_scores_json | jsonb | Điểm từng tiêu chí |
| submission.feedback | text | |
| submission.graded_by, graded_at | | |
| file.id | UUID | PK |
| file.submission_id | UUID | FK |
| file.slot_id | UUID | FK → SubmissionSlot |
| file.file_path, file_name, file_size | string/string/int | |
| file.link_url | string (nullable) | Nếu slot cho phép nộp link |
| file.extracted_path, file_tree_json | string/jsonb (nullable) | Chỉ áp dụng cho file `.zip` — giải nén để xem trực tiếp trên trình duyệt |
| file.structure_check_passed | boolean (nullable) | Chỉ áp dụng cho slot có `required_structure_json` |
| file.similarity_score | decimal (nullable) | Chỉ áp dụng cho slot chấp nhận `.zip` — % giống bài khác trong cùng lớp |
| file.uploaded_at | timestamp | |

#### `SubmissionMemberScore` (Điều chỉnh điểm riêng từng thành viên trong nhóm — tùy chọn, không bắt buộc dùng)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| submission_id | UUID | FK → Submission (owner group) |
| enrollment_id | UUID | FK → thành viên cụ thể |
| score_override | decimal (nullable) | Nếu null thì thành viên nhận đúng `submission.score` chung của nhóm |
| note | text | Lý do điều chỉnh (VD: "Không đóng góp phần backend") |

> Mặc định mọi thành viên trong nhóm nhận cùng 1 điểm (`submission.score`). Nếu giảng viên muốn chấm khác nhau theo đóng góp thực tế, chỉ cần thêm bản ghi `SubmissionMemberScore` cho thành viên đó — cơ chế hoàn toàn tùy chọn, không bắt buộc cấu hình từ đầu.

#### `Quiz`, `QuizQuestion`, `QuizAttempt`
| Trường | Kiểu | Ghi chú |
|---|---|---|
| quiz.id | UUID | PK |
| quiz.type | enum | `checkpoint` (không tính điểm, chèn giữa bài) \| `graded` (tính điểm) |
| quiz.time_limit_minutes | int | |
| quiz.max_attempts | int | |
| quiz.shuffle_questions, shuffle_answers | boolean | |
| question.type | enum | `single_choice` \| `multiple_choice` \| `true_false` \| `fill_blank` |
| question.foundation_tag | string | Gắn với khái niệm nền tảng để phục vụ ôn tập |
| attempt.answers_json | jsonb | |
| attempt.score | decimal | |
| attempt.wrong_question_ids | UUID[] | Đưa vào hàng đợi ôn lại |

#### `CourseOffering` (Lớp học phần — 1 đợt dạy cụ thể)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| subject_id | UUID | FK → Subject (để biết thuộc môn nào, dùng cho báo cáo/nhóm lớp) |
| subject_version_id | UUID | FK → SubjectVersion — **phiên bản nội dung cố định dùng cho lớp này**, không tự đổi theo khi giảng viên cập nhật version mới sau này |
| semester | string | VD: HK1 2026-2027 |
| class_code | string | VD: DA24TIN01 |
| join_code | string | Mã để SV tự ghi danh (tùy chọn) |
| status | enum | `draft` \| `active` \| `closed` |

#### `Enrollment` (Sinh viên trong lớp)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| offering_id | UUID | FK |
| student_code | string | MSSV |
| full_name, email | string | |
| status | enum | `active` \| `dropped` |
| xp_points, streak_days, badges | int/int/string[] | Cơ chế động lực |

#### `FinalAssessment` (Đợt chấm điểm cuối môn — ở cấp CourseOffering, tách biệt với Lesson trong chương)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| offering_id | UUID | FK → CourseOffering |
| type | enum | `quiz` (Trắc nghiệm) \| `final_project` (Bài tập lớn) — **loại `exam` (Thi) không triển khai giai đoạn này** |
| title, description | string/text | |
| weight_percent | decimal | Trọng số đóng góp vào điểm cuối môn (liên kết tới `GradeColumn`) |
| due_at | timestamp | |
| submission_mode | enum | `individual` \| `group` — dùng chung field/nghĩa với `Assignment.submission_mode` |
| group_config_json | jsonb (nullable) | Giống `Assignment.group_config_json` |
| ref_quiz_id | UUID (nullable) | Trỏ tới `Quiz.id` nếu type=`quiz` — tái sử dụng nguyên hạ tầng Quiz đã có |

> Vì type=`exam` bị bỏ qua, hệ thống chỉ cần xử lý 2 nhánh: **`quiz`** (dùng lại engine Quiz sẵn có, chỉ khác là gắn ở cấp môn thay vì cấp Lesson) và **`final_project`** — dùng đúng **`SubmissionSlot` + `Submission`/`SubmissionFile`** ở trên với `owner_type=final_assessment`, không cần bảng riêng nữa.

> **Validate khi nộp:** hệ thống kiểm tra từng `is_required=true` slot đã có ít nhất 1 file/link trước khi cho phép SV bấm "Nộp bài chính thức"; SV có thể lưu nháp (upload từng phần) trước khi nộp chính thức.



#### `GradeColumn` & `GradeRecord`
| Trường | Kiểu | Ghi chú |
|---|---|---|
| grade_column.offering_id | UUID | FK |
| grade_column.name | string | VD: Chuyên cần, Bài tập, Giữa kỳ, Cuối kỳ |
| grade_column.weight_percent | decimal | Tổng các cột = 100% |
| grade_column.source_type | enum | `manual` \| `auto_from_assignment` \| `auto_from_quiz` |
| grade_record.enrollment_id | UUID | FK |
| grade_record.column_id | UUID | FK |
| grade_record.score | decimal | |

#### `BonusPointReasonTemplate` (Thư viện lý do điểm cộng — chuẩn bị trước, tái sử dụng nhiều lần)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| instructor_id | UUID | FK → Instructor — thuộc về giảng viên, dùng lại được cho mọi môn/lớp (không giới hạn theo 1 offering) |
| label | string | VD: "Thuyết trình tự tin", "Trả lời đúng câu hỏi khó", "Phát biểu xây dựng bài" |
| default_points | decimal | Điểm gợi ý khi chọn nhanh — vẫn sửa được lúc cấp |
| order_index | int | Thứ tự hiển thị trong danh sách nút bấm nhanh |
| is_active | boolean | Ẩn bớt lý do không dùng nữa mà không cần xóa hẳn |

> Danh sách này **cấu hình động** — giảng viên tự thêm/sửa/xóa/sắp xếp lại bao nhiêu lý do tùy ý, không giới hạn số lượng hay nội dung cố định.

#### `BonusPoint`
| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | UUID | PK |
| enrollment_id | UUID | FK — luôn gắn vào **1 sinh viên cụ thể** (kể cả khi cấp trong bối cảnh chấm nhóm, giảng viên chọn đúng thành viên nào được cộng) |
| context_type | enum (nullable) | `assignment` \| `final_assessment` \| `attendance` \| `none` — bối cảnh phát sinh điểm cộng, dùng để lọc/thống kê, không bắt buộc |
| context_id | UUID (nullable) | FK tương ứng theo `context_type`, VD: gắn với đúng `Submission`/buổi báo cáo đang chấm |
| template_id | UUID (nullable) | FK → `BonusPointReasonTemplate` nếu chọn từ danh sách có sẵn; để trống nếu gõ lý do mới ngay lúc đó |
| points | decimal | Lấy từ `template.default_points` (có thể sửa) hoặc nhập tay nếu không dùng template |
| reason | string | Lấy từ `template.label` (có thể sửa) hoặc gõ tự do |
| granted_by, granted_at | | |

#### `ReviewQueueItem` (Ôn tập ngắt quãng)
| Trường | Kiểu | Ghi chú |
|---|---|---|
| student_id | UUID | FK |
| source_question_id | UUID | FK → QuizQuestion đã trả lời sai |
| next_review_at | timestamp | Tính theo lịch 3 ngày / 1 tuần / 2 tuần |
| review_count | int | |
| status | enum | `pending` \| `mastered` |

---

## 4. DANH SÁCH USE CASE

| Mã | Use case | Tác nhân |
|---|---|---|
| UC-01 | Tạo môn học & nhập đề cương | Instructor |
| UC-02 | Tạo chương, thêm N bài học (Lesson) tùy ý | Instructor |
| UC-03 | Gắn/gỡ component tùy chọn (bài tập, quiz, recap...) cho từng Lesson | Instructor |
| UC-04 | Tạo lớp học phần theo học kỳ | Instructor |
| UC-05 | Import danh sách SV từ Excel | Instructor |
| UC-06 | Tạo phiên bản nội dung mới (versioning) cho môn học ở năm/học kỳ mới | Instructor |
| UC-06b | Mở lớp học phần chọn phiên bản nội dung cụ thể để dạy | Instructor |
| UC-06c | Xem lại lịch sử các phiên bản cũ của môn học (chỉ đọc) | Instructor |
| UC-07 | Tạo bài tập, cấu hình rubric, hint, cấu hình format nộp (slot) và chế độ cá nhân/nhóm | Instructor |
| UC-07b | Chia nhóm sinh viên (giảng viên chia hoặc SV tự lập nhóm/tự ghép) | Instructor/Student |
| UC-08 | Sinh viên (hoặc nhóm) nộp bài theo từng slot đã cấu hình (source code, hình ảnh, video, ...) | Student |
| UC-09 | Hệ thống kiểm tra cấu trúc file (.zip) & phát hiện trùng lặp cho slot source code | System |
| UC-10 | Giảng viên chấm bài theo rubric, xem theo từng slot đã nộp | Instructor/TA |
| UC-10b | Giảng viên điều chỉnh điểm riêng cho từng thành viên trong nhóm (tùy chọn) | Instructor |
| UC-11 | Tạo ngân hàng câu hỏi & quiz | Instructor |
| UC-12 | Sinh viên làm quiz (checkpoint & graded) | Student |
| UC-13 | Hệ thống đưa câu sai vào hàng đợi ôn tập | System |
| UC-14 | Sinh viên ôn tập theo lịch ngắt quãng | Student |
| UC-15 | Cấu hình cột điểm & trọng số | Instructor |
| UC-21 | Tạo đợt chấm điểm cuối môn (Trắc nghiệm hoặc Bài tập lớn) | Instructor |
| UC-22 | Cấu hình format nộp bài cho Bài tập lớn (thêm/sửa/xóa slot nộp file) — dùng chung UI với UC-07 | Instructor |
| UC-23 | Sinh viên (hoặc nhóm) nộp Bài tập lớn theo từng slot | Student |
| UC-24 | Giảng viên chấm Bài tập lớn cuối môn | Instructor |
| UC-16 | Hệ thống tự tổng hợp điểm từ bài tập/quiz | System |
| UC-17 | Giảng viên cộng điểm thưởng có lý do — chọn nhanh từ thư viện lý do soạn sẵn hoặc gõ lý do mới tại chỗ | Instructor |
| UC-17b | Giảng viên quản lý thư viện lý do điểm cộng (thêm/sửa/xóa/ẩn) | Instructor |
| UC-25 | Giảng viên mở màn hình chấm điểm nhóm ngay sau khi nhóm báo cáo — xem danh sách thành viên, chấm điểm và cấp điểm cộng riêng từng người trong cùng 1 màn hình | Instructor |
| UC-18 | Xuất bảng điểm ra Excel | Instructor |
| UC-19 | Sinh viên xem tiến độ, XP, huy hiệu, streak | Student |
| UC-20 | Giảng viên xem dashboard lớp (SV chưa nộp, bài chưa chấm, SV nguy cơ) | Instructor |

### Đặc tả chi tiết UC-08 (nộp bài theo slot động, hỗ trợ cá nhân/nhóm)

**Tiền điều kiện:** SV đã đăng nhập, đã vào đúng lớp học phần, bài tập đang trong hạn (hoặc cho phép nộp trễ). Nếu `submission_mode=group`, SV phải thuộc 1 `StudentGroup` hợp lệ cho bài tập đó.

**Luồng chính:**
1. SV mở Assignment → hệ thống hiển thị đúng các `SubmissionSlot` đã cấu hình (VD: Source code, Hình ảnh, Video — hoặc chỉ 1 slot Source code nếu giảng viên không thêm gì khác).
2. Với mỗi slot, giao diện chỉ nhận đúng `accepted_file_types` đã khai báo; slot cho phép link (thường là video) hiện thêm ô nhập URL.
3. SV chọn/tải file cho từng slot → hệ thống kiểm tra dung lượng ≤ `max_file_size_mb` của slot đó.
4. Với slot chấp nhận `.zip`: hệ thống giải nén tạm để kiểm tra `required_structure_json` (nếu slot có cấu hình) → nếu thiếu, cảnh báo cho SV nhưng **vẫn cho nộp**, gắn `structure_check_passed = false` để giảng viên thấy.
5. Với slot chấp nhận `.zip`: hệ thống tính `similarity_score` so với các bài khác đã nộp trong cùng lớp/bài tập.
6. SV có thể **lưu nháp** (nộp từng slot một, chưa hoàn tất) — hệ thống chỉ khóa "Nộp chính thức" khi mọi slot `is_required=true` đã có dữ liệu.
7. Nếu `submitted_by_type=group`: chỉ cần 1 thành viên (thường là leader, nhưng cấu hình được ai được nộp) bấm "Nộp chính thức" là tính cho cả nhóm; tất cả thành viên đều xem được bài đã nộp.
8. Ghi nhận `submitted_at`, tính `is_late` nếu quá `due_at`. SV/nhóm có thể nộp lại (ghi đè, giữ lịch sử) trước hạn.

**Luồng phụ:** File vượt dung lượng hoặc sai định dạng slot → báo lỗi ngay tại bước 3, không upload lên server.

---

## 5. ĐẶC TẢ CHỨC NĂNG THEO MODULE

### 5.1 Module Nội dung môn học (cấu trúc động)
- Trình soạn thảo chương: thêm **bao nhiêu Lesson tùy ý** (1, 2, 5...), không có giới hạn hay khuôn cố định.
- Với mỗi Lesson, giảng viên bấm **"+ Thêm phần"** để gắn kèm 0 hoặc nhiều `LessonComponent`, chọn từ danh sách loại: Ôn lại đầu bài (`recap`), Mở đầu gây tò mò (`hook`), Chốt nhanh giữa bài (`checkpoint_quiz`), Làm cùng nhau (`code_along`), **Bài tập** (`assignment`), **Trắc nghiệm** (`quiz`), Suy ngẫm cuối bài (`reflection`), hoặc Ghi chú tự do (`custom_note`).
- **Không có ràng buộc bắt buộc phải có component nào** — một Lesson có thể chỉ là nội dung học thuần túy (PPT/video), không kèm gì cả. Việc chương có "1 bài kèm bài tập + trắc nghiệm, bài kia không có gì" chỉ đơn giản là: Lesson A có 2 component (assignment + quiz), Lesson B có 0 component.
- Kéo-thả để sắp xếp thứ tự component trong 1 Lesson (VD: đặt `recap` trước nội dung, `reflection` sau cùng — nhưng đây là lựa chọn, không phải quy tắc cứng).
- Khuyến nghị (không bắt buộc): chia nội dung học thành các Lesson ngắn **8–15 phút** — hệ thống chỉ hiển thị cảnh báo nhẹ nếu ước lượng > 20 phút, giảng viên vẫn có thể bỏ qua.
- Preview PPT/PDF ngay trong trình duyệt (không bắt SV tải về).
- Component `recap`: khi bật, hệ thống tự động chọn ngẫu nhiên 3–5 câu hỏi từ các chương trước (dựa trên `foundation_tags`), cấu hình số lượng qua `config_json`.
- Component `reflection`: ô nhập tự do (VD "3 điều em nhớ nhất") — nội dung câu hỏi gợi ý có thể tùy chỉnh trong `config_json`, giảng viên xem được trong dashboard.
- **Thư viện component tái sử dụng:** một Assignment/Quiz đã tạo có thể được gắn (`ref_id`) vào nhiều Lesson khác nhau nếu cần, không bị khóa 1-1 với 1 bài học.

### 5.2 Module Versioning nội dung theo năm/học kỳ
- Mỗi Subject có thể có **nhiều `SubjectVersion`** theo thời gian (VD: `2024-2025`, `2025-2026`, `2026-2027`), phản ánh việc nội dung dạy học thường được cập nhật mỗi năm (đổi công nghệ, cập nhật đề cương, bỏ/thêm chương...).
- **Tạo phiên bản mới:** giảng viên bấm "Tạo phiên bản mới" từ 1 version bất kỳ (thường là version mới nhất) → hệ thống **clone toàn bộ** Chapter/Lesson/LessonComponent/Assignment/Quiz sang version mới ở trạng thái `draft` → chỉnh sửa tự do mà không ảnh hưởng dữ liệu version gốc.
- **Xuất bản:** khi giảng viên hài lòng, bấm "Xuất bản" → version chuyển `published`, version trước đó (nếu đang `published`) tự chuyển `archived`. Có thể ghi `changelog` mô tả thay đổi so với version trước để dễ tra cứu sau này.
- **Không đổi dữ liệu ngược:** một `CourseOffering` (lớp đã mở) **luôn giữ nguyên** `subject_version_id` đã chọn lúc tạo lớp — sửa/tạo version mới sau đó **không** làm thay đổi nội dung SV đang học trong lớp đang chạy. Muốn áp dụng version mới cho lớp đang chạy phải thao tác thủ công (không tự động), tránh phá vỡ tiến độ giữa học kỳ.
- **Xem lịch sử:** trang "Lịch sử phiên bản" liệt kê tất cả version của 1 Subject theo thời gian, mỗi version chỉ đọc (read-only) trừ bản đang `draft`, xem được changelog và danh sách lớp học phần nào từng dùng version đó.
- **So sánh version (tùy chọn, có thể làm ở P1/P2):** hiển thị khác biệt số chương/bài học giữa 2 version để giảng viên nhanh chóng thấy đã thay đổi gì.

### 5.3 Module Lớp học phần & Sinh viên
- Import Excel theo mẫu cột: MSSV, Họ tên, Email, Lớp.
- Kiểm tra trùng MSSV khi import, báo lỗi dòng cụ thể.
- Chế độ mở khóa chương: `free` (mở hết) hoặc `after_previous_completed` (yêu cầu hoàn thành các phần bắt buộc của chương trước).

### 5.4 Module Bài tập & Nộp bài (format động, dùng chung cho chương và cuối môn)
- Khi tạo Assignment, giảng viên khai báo **danh sách `SubmissionSlot`** cần nộp — không giới hạn ở ZIP nữa. Có thể có 1 slot duy nhất (chỉ source code) hoặc nhiều slot (source code + hình ảnh + video), tùy từng bài, đúng ví dụ "bài này cần source+ảnh+video, bài kia chỉ cần source".
- Có thể bấm "Dùng mẫu gợi ý" để tự điền nhanh bộ slot phổ biến (Source code `.zip`, Hình ảnh `.jpg/.png`, Video `.mp4`/link) rồi chỉnh sửa/xóa bớt theo ý.
- Xem chi tiết luồng nộp bài ở UC-08 phía trên.
- Với slot chấp nhận `.zip`: hệ thống vẫn giữ nguyên các kiểm tra như trước — giải nén xem cây thư mục, kiểm tra cấu trúc bắt buộc, tính `similarity_score` cảnh báo trùng lặp (chỉ cảnh báo, không tự trừ điểm).
- Hint: SV mở từng hint, mỗi lần mở trừ điểm theo cấu hình `penalty_point`, hệ thống ghi log số hint đã dùng.
- Màn hình chấm bài (xem mục 6.5): hiển thị **tất cả slot đã nộp trong 1 màn hình**, giảng viên click qua từng slot để xem/tải (code tô màu cú pháp, ảnh xem trực tiếp, video nhúng phát được), chấm theo rubric ngay bên cạnh — không phải chuyển màn hình qua lại giữa các loại minh chứng.

### 5.5 Module Làm việc nhóm
- **Cấu hình theo từng Assignment/FinalAssessment** (`submission_mode = individual | group`) — không phải cài đặt chung toàn hệ thống, một môn có thể vừa có bài tập cá nhân vừa có bài tập nhóm.
- Khi bật chế độ nhóm, giảng viên cấu hình thêm (`group_config_json`): số thành viên tối thiểu/tối đa, ai được tạo nhóm — **giảng viên tự chia** hoặc **SV tự lập nhóm/tự ghép** (kèm giới hạn thời gian chốt danh sách nhóm nếu cần).
- Một `StudentGroup` có thể **dùng lại** cho nhiều bài tập trong cùng lớp (nhóm cố định cả kỳ) hoặc tạo mới riêng cho từng bài — tùy giảng viên chọn khi cấu hình.
- Khi nộp bài nhóm: 1 thành viên (thường leader) nộp thay cả nhóm, mọi thành viên đều xem được bài đã nộp và điểm.
- Khi chấm bài nhóm: giảng viên chấm 1 lần cho cả nhóm (`Submission.score`); nếu muốn phân hóa theo đóng góp thực tế, có thể **thêm điều chỉnh riêng** cho từng thành viên qua `SubmissionMemberScore` (không bắt buộc, chỉ dùng khi cần) — màn hình chấm bài hiển thị danh sách thành viên kèm ô điểm override + ghi chú lý do.
- **Chấm điểm ngay sau khi nhóm báo cáo (thuyết trình trực tiếp trên lớp):** giảng viên mở đúng `Submission` của nhóm đó (từ danh sách "Chưa chấm" trên dashboard hoặc tìm theo tên nhóm) → màn hình hiện ngay danh sách thành viên + rubric chấm điểm + khu vực cấp điểm cộng nhanh (xem mục 5.8 và màn hình 6.7) — tất cả trong 1 màn hình duy nhất, không phải chuyển qua lại giữa trang chấm bài và trang sổ điểm.

### 5.6 Module Trắc nghiệm
- Ngân hàng câu hỏi gắn `foundation_tags`.
- Checkpoint quiz: chèn giữa `core_lesson`, không tính điểm, hiện giải thích ngay sau khi trả lời.
- Graded quiz: giới hạn thời gian, số lần làm, random câu hỏi/đáp án.
- Trả lời sai ở graded quiz → tự động tạo `ReviewQueueItem` với lịch ôn 3 ngày / 1 tuần / 2 tuần.

### 5.7 Module Đợt chấm điểm cuối môn
- Nằm ở cấp **CourseOffering** (một đợt dạy cụ thể), tách biệt hoàn toàn khỏi Lesson trong chương — vì đây là mốc đánh giá tổng kết môn, không thuộc riêng chương nào.
- Khi tạo, giảng viên chọn loại: **Trắc nghiệm** hoặc **Bài tập lớn** (loại **Thi** hiển thị nhưng bị vô hiệu hóa/không triển khai ở giai đoạn này — để dành chỗ cho tương lai nếu cần).
- **Loại Trắc nghiệm:** dùng lại toàn bộ engine Quiz đã có (mục 5.6) — ngân hàng câu hỏi, giới hạn thời gian, random câu hỏi. Khác biệt duy nhất là gắn vào `FinalAssessment` cấp môn thay vì `LessonComponent` cấp bài học.
- **Loại Bài tập lớn:** dùng đúng cơ chế `SubmissionSlot` + chế độ nhóm ở mục 5.4/5.5 (không có hệ thống riêng nữa) — giảng viên khai báo slot (Báo cáo, File trình bày, Source code, Hình ảnh, Video...) và chọn cá nhân hay nhóm, y hệt cách cấu hình Assignment trong chương.
- Chấm điểm: dùng chung màn hình/cơ chế Rubric như bài tập trong chương (mục 6.5/6.10).

### 5.8 Module Sổ điểm & Điểm cộng
- Cấu hình cột điểm + trọng số, tổng phải = 100% (hệ thống validate).
- Cột `auto_from_assignment` / `auto_from_quiz` tự động cập nhật khi có điểm mới, giảng viên vẫn có thể sửa tay (ghi log ai sửa).
- **Thư viện lý do điểm cộng (`BonusPointReasonTemplate`):** giảng viên soạn trước danh sách lý do hay dùng (VD: "Thuyết trình tự tin", "Trả lời đúng câu khó", "Phát biểu xây dựng bài"), mỗi lý do có điểm gợi ý mặc định — soạn 1 lần, dùng lại cho mọi lớp/mọi học kỳ, có thể thêm/sửa/ẩn bất cứ lúc nào.
- **Cấp điểm cộng:** khi thao tác, giảng viên chọn 1 SV → chọn nhanh 1 lý do từ thư viện (tự điền điểm + lý do, vẫn sửa được) **hoặc** bỏ qua thư viện, gõ lý do mới + điểm tự do ngay tại chỗ — cả 2 cách đều được, không bắt buộc dùng template.
- Mỗi điểm cộng có thể gắn `context_type`/`context_id` (VD: gắn đúng vào buổi báo cáo/bài nộp đang chấm) để sau này lọc "điểm cộng phát sinh từ đâu" trong sổ điểm — nhưng đây là thông tin tùy chọn, cấp điểm cộng độc lập (không thuộc bài tập nào) vẫn hợp lệ.
- Điểm cộng cộng trực tiếp vào tổng điểm cuối hoặc vào 1 cột riêng (tùy cấu hình).
- Xuất Excel: theo template có thể tùy chỉnh (đúng mẫu phòng đào tạo).

### 5.9 Module Động lực học tập (Gamification)
- XP: cộng khi hoàn thành section, nộp bài đúng hạn, làm quiz đạt điểm cao.
- Huy hiệu: "Nộp sớm", "Không sai câu nào", "Hoàn thành 5 chương liên tiếp", v.v. — danh sách điều kiện cấu hình được.
- Streak: số ngày học liên tiếp (có hoạt động trong hệ thống).
- Bảng xếp hạng: ẩn danh theo mặc định, giảng viên bật/tắt hiển thị tên thật cho từng lớp.

### 5.10 Module Ôn tập ngắt quãng
- Job chạy hằng ngày quét `ReviewQueueItem` có `next_review_at <= now` và `status = pending`.
- Sinh viên thấy mục "Ôn tập hôm nay" ở trang chủ, gồm các câu hỏi cần ôn.
- Trả lời đúng → tăng cấp độ ôn tiếp theo (VD 3 ngày → 1 tuần → 2 tuần → `mastered`); trả lời sai → quay lại chu kỳ đầu.

---

## 6. ĐẶC TẢ MÀN HÌNH CHÍNH

### 6.1 Dashboard Giảng viên
- Thẻ tổng quan: số lớp đang dạy, số bài chưa chấm, số SV chưa nộp bài gần hạn.
- Danh sách "Cần xử lý" sắp theo hạn gần nhất.
- Biểu đồ tiến độ từng lớp (% SV hoàn thành từng chương).

### 6.2 Trang quản lý Chương (Instructor) — dạng "trình soạn thảo động"
- Danh sách các Lesson trong chương theo thứ tự, mỗi Lesson là 1 khối có thể mở rộng (expand/collapse).
- Trong mỗi Lesson: khu vực nội dung chính (upload PPT/PDF/video) + danh sách component đã gắn (hiển thị dạng thẻ nhỏ, có icon theo loại) + nút **"+ Thêm phần"** mở dropdown chọn loại component muốn thêm.
- Mỗi thẻ component có nút xóa/sửa/kéo-thả đổi vị trí riêng — hoàn toàn độc lập giữa các Lesson (Lesson này thêm gì không ảnh hưởng Lesson khác).
- Khi thêm component loại `assignment` hoặc `quiz`: cho phép **tạo mới** hoặc **chọn từ thư viện** (Assignment/Quiz đã tạo trước đó) để tái sử dụng.

### 6.3 Trang học của Sinh viên (Student — trải nghiệm kiểu Udemy)
- Sidebar: danh sách chương, có icon khóa/mở, % hoàn thành.
- Nội dung chính: hiển thị section hiện tại (PPT/video/quiz/challenge) full màn hình, nút "Tiếp theo".
- Thanh trên cùng: XP hiện tại, streak, nút "Ôn tập hôm nay" nếu có bài đến hạn.

### 6.4 Trang cấu hình Bài tập (Instructor) — format nộp & chế độ nhóm
- Khi tạo/sửa Assignment: danh sách `SubmissionSlot` dạng kéo-thả, mỗi dòng: Tên slot | Loại file chấp nhận | Bắt buộc? | Cho phép nhiều file? | Cho phép nộp link?
- Nút "+ Thêm slot" thêm dòng trống; nút "Dùng mẫu gợi ý" tự điền nhanh bộ slot phổ biến (Source code/Hình ảnh/Video) để chỉnh sửa thay vì gõ từ đầu — giảng viên có thể xóa bớt nếu bài chỉ cần 1 slot.
- Toggle "Làm theo nhóm" — khi bật, hiện thêm cấu hình số thành viên tối thiểu/tối đa và ai được tạo nhóm.

### 6.5 Trang chia nhóm (Instructor/Student) — chỉ hiện khi Assignment bật chế độ nhóm
- **Instructor:** bảng kéo-thả SV vào từng nhóm, hoặc nút "Chia ngẫu nhiên" theo sĩ số đã cấu hình; có thể chọn dùng lại nhóm đã tạo từ bài tập trước trong cùng lớp.
- **Student:** nếu cấu hình cho SV tự lập nhóm — màn hình "Tạo nhóm" / "Tham gia nhóm" (nhập mã nhóm hoặc chọn từ danh sách nhóm còn chỗ trống).

### 6.6 Trang nộp bài (Student) — theo slot động, dùng chung cho Assignment và Bài tập lớn cuối môn
- Hiển thị đúng các `SubmissionSlot` đã cấu hình cho bài tập đó, mỗi slot 1 vùng upload riêng (hoặc ô nhập link nếu slot cho phép) — có thể chỉ 1 slot (source code) hoặc nhiều slot (source code + hình ảnh + video).
- Đèn trạng thái từng slot: chưa nộp (xám) / đã nộp (xanh) / thiếu bắt buộc (đỏ).
- Nếu là bài tập nhóm: hiển thị banner "Nộp thay cho Nhóm X" kèm danh sách thành viên; các thành viên khác vào xem được bài đã nộp (không nộp trùng).
- Với slot `.zip`: hiển thị checklist cấu trúc bắt buộc, tick xanh/đỏ theo `required_structure_json`.
- Nút "Xem gợi ý" (nếu Assignment có hint) hiện cảnh báo trừ điểm trước khi mở.
- Nút "Lưu nháp" (nộp từng phần) và "Nộp chính thức" (chỉ bật khi mọi slot bắt buộc đã có dữ liệu).

### 6.7 Trang chấm bài (Instructor/TA) — dùng chung cho Assignment và Bài tập lớn cuối môn, kể cả chấm trực tiếp khi nhóm báo cáo
- Layout 3 cột: **danh sách slot đã nộp** (click từng slot để xem — cây thư mục ZIP có tô màu cú pháp cho source code, xem ảnh trực tiếp, video nhúng phát được, PDF/Word preview) | nội dung slot đang xem | rubric chấm điểm + khung nhận xét.
- Cảnh báo trực quan nếu `similarity_score` (của slot zip) vượt ngưỡng, có nút "Xem bài so sánh".
- **Nếu là bài nhóm:** ngay đầu trang hiện rõ **tên nhóm + danh sách đầy đủ thành viên** (avatar, họ tên, MSSV) — đúng nhu cầu "báo cáo xong thì list ra nhóm đó có ai". Mỗi thành viên có ô "Điểm riêng (tùy chọn)" — để trống thì nhận điểm chung của nhóm, điền vào thì override kèm ô lý do.
- **Khu vực Điểm cộng nhanh** ngay trong màn hình chấm bài này (không cần rời sang trang Sổ điểm): với mỗi thành viên trong danh sách, có nút nhỏ "+ Điểm cộng" mở popover gồm:
  - Dãy nút bấm nhanh lấy từ `BonusPointReasonTemplate` (VD: "Thuyết trình tự tin +0.5", "Trả lời đúng câu khó +0.5") — bấm 1 phát là điền sẵn điểm + lý do, có thể sửa lại trước khi lưu.
  - Ô "Lý do khác..." để gõ tự do nếu không có sẵn trong thư viện — nhập lý do + điểm rồi lưu như bình thường, không bắt buộc phải chọn từ danh sách.
  - Điểm cộng lưu ngay lập tức, hiện thành nhãn nhỏ cạnh tên SV (VD: "+0.5 · Trả lời đúng câu khó"), có thể click để sửa/xóa.

### 6.7b Trang quản lý Thư viện lý do điểm cộng (Instructor)
- Danh sách các `BonusPointReasonTemplate` dạng bảng: Lý do | Điểm mặc định | Trạng thái (Đang dùng/Đã ẩn) | kéo-thả đổi thứ tự.
- Nút "+ Thêm lý do" thêm dòng mới; nút ẩn (không xóa hẳn) để giữ lịch sử các điểm cộng cũ đã dùng lý do đó.
- Soạn 1 lần, dùng lại cho mọi lớp — không cần cấu hình lại mỗi học kỳ.

### 6.8 Trang Sổ điểm (Instructor)
- Bảng: hàng = SV, cột = GradeColumn, dòng tổng có điểm cuối cùng và điểm chữ quy đổi.
- Nút "Xuất Excel", nút "Thêm điểm cộng" mở cùng 1 popover chọn nhanh từ thư viện / gõ lý do mới như mô tả ở mục 6.7 (dùng chung component, không làm 2 UI khác nhau).

### 6.9 Trang Đợt chấm điểm cuối môn (Instructor)
- Danh sách các `FinalAssessment` đã tạo cho lớp, mỗi thẻ hiện loại (Trắc nghiệm/Bài tập lớn), trọng số, hạn nộp, số SV/nhóm đã nộp/chưa nộp.
- Nút "Tạo đợt mới" → chọn loại: **Trắc nghiệm** | **Bài tập lớn** | ~~Thi~~ (hiển thị mờ, ghi chú "Chưa hỗ trợ").
- Khi chọn Bài tập lớn: mở đúng màn hình cấu hình slot + chế độ nhóm (mục 6.4) và màn hình chia nhóm (mục 6.5) — dùng chung UI với Assignment trong chương, không xây riêng.

> **Bài tập lớn cuối môn dùng chung màn hình nộp bài (6.6) và chấm bài (6.7) với Assignment trong chương** — không cần xây UI riêng, vì cả hai đều dựa trên `SubmissionSlot`/`Submission` chung.

### 6.10 Trang Lịch sử phiên bản môn học (Instructor)
- Timeline dọc, mỗi mốc là 1 `SubjectVersion`: nhãn version, trạng thái (badge màu: xanh=`published`, xám=`archived`, vàng=`draft`), ngày xuất bản, changelog rút gọn.
- Click 1 version cũ → xem read-only toàn bộ Chapter/Lesson bên trong (không cho sửa).
- Nút "Tạo phiên bản mới từ đây" trên mỗi version → tạo bản `draft` clone từ version đó.
- Trong danh sách này, mỗi version hiện luôn danh sách các `CourseOffering` (lớp học phần) đã/đang dùng version đó, để giảng viên biết version nào còn "sống" trong lớp đang dạy.

---

## 7. THIẾT KẾ API (RESTful, tóm tắt các nhóm chính)

```
Auth
  POST   /api/auth/login
  POST   /api/auth/refresh

Subjects & Versioning
  GET    /api/subjects
  POST   /api/subjects
  GET    /api/subjects/{id}/versions                    (lịch sử phiên bản)
  POST   /api/subjects/{id}/versions                     { cloned_from_version_id?, version_label }  (tạo draft mới, có thể clone hoặc tạo trắng)
  PUT    /api/subject-versions/{id}                       { changelog, syllabus_json, ... }  (chỉ sửa được khi status=draft)
  POST   /api/subject-versions/{id}/publish               (draft → published, version published cũ → archived)

Chapters & Lessons (thuộc 1 SubjectVersion cụ thể)
  GET    /api/subject-versions/{id}/chapters
  POST   /api/subject-versions/{id}/chapters
  POST   /api/chapters/{id}/lessons                    { title, content_type, ... }
  PUT    /api/lessons/{id}/reorder
  POST   /api/lessons/{id}/components                  { type, ref_id?, config_json }
  DELETE /api/lesson-components/{id}
  PUT    /api/lesson-components/{id}/reorder
  GET    /api/assignments?reusable=true                 (thư viện để gắn lại vào Lesson khác)
  GET    /api/quizzes?reusable=true

Offerings & Enrollment
  POST   /api/offerings                         { subject_id, subject_version_id, semester, class_code }
  POST   /api/offerings/{id}/enrollments/import  (multipart Excel)
  GET    /api/offerings/{id}/enrollments

Groups (làm việc nhóm — dùng chung cho Assignment và FinalAssessment)
  GET    /api/offerings/{id}/groups
  POST   /api/offerings/{id}/groups                    { name, member_enrollment_ids }
  POST   /api/offerings/{id}/groups/auto-generate       { group_size }
  POST   /api/groups/{id}/join                          (SV tự tham gia, nếu cấu hình cho phép)
  PUT    /api/groups/{id}/members

Assignments & Submissions (format nộp động, dùng chung cho Assignment và FinalAssessment)
  POST   /api/assignments                              { title, submission_mode: "individual"|"group", group_config_json?, rubric_id, ... }
  POST   /api/assignments/{id}/slots                    { slot_key, label, accepted_file_types, is_required, max_files, allow_link_instead_of_file, required_structure_json? }
  PUT    /api/submission-slots/{id}
  DELETE /api/submission-slots/{id}
  POST   /api/assignments/{id}/submissions/draft         (multipart theo từng slot, lưu nháp; cũng dùng owner_type=final_assessment tương tự)
  POST   /api/assignments/{id}/submissions/submit        (khóa nộp chính thức, validate slot bắt buộc)
  GET    /api/submissions/{id}
  GET    /api/submissions/{id}/files/{slot_id}/file-tree  (chỉ áp dụng cho file .zip)
  GET    /api/submissions/{id}/files/{slot_id}/similarity (chỉ áp dụng cho file .zip)
  POST   /api/submissions/{id}/grade                     { rubric_scores, feedback }
  POST   /api/submissions/{id}/member-scores              { enrollment_id, score_override, note }  (chỉ khi submitted_by_type=group)

Quizzes
  POST   /api/quizzes
  POST   /api/quizzes/{id}/questions
  POST   /api/quizzes/{id}/attempts
  POST   /api/attempts/{id}/submit

Final Assessments (Đợt chấm điểm cuối môn)
  GET    /api/offerings/{id}/final-assessments
  POST   /api/offerings/{id}/final-assessments        { type: "quiz" | "final_project", title, weight_percent, due_at, submission_mode?, group_config_json? }
  # type=quiz  → dùng nhóm API "Quizzes" ở trên, gán qua ref_quiz_id
  # type=final_project → dùng nhóm API "Assignments & Submissions" ở trên với owner_type=final_assessment

Grades & Bonus Points
  GET    /api/offerings/{id}/gradebook
  PUT    /api/offerings/{id}/grade-columns
  GET    /api/instructor/bonus-point-templates
  POST   /api/instructor/bonus-point-templates             { label, default_points }
  PUT    /api/bonus-point-templates/{id}
  DELETE /api/bonus-point-templates/{id}                    (thực chất set is_active=false)
  POST   /api/enrollments/{id}/bonus-points                 { template_id?, points, reason, context_type?, context_id? }
  GET    /api/offerings/{id}/export-excel

Review Queue
  GET    /api/students/{id}/review-queue
  POST   /api/review-items/{id}/answer

Dashboard
  GET    /api/instructor/dashboard
  GET    /api/students/{id}/progress
```

**Quy tắc chung:** tất cả endpoint yêu cầu JWT; endpoint `submissions` validate MIME type + đuôi file `.zip` ở cả client lẫn server (không tin client).

---

## 8. KIẾN TRÚC KỸ THUẬT

> **⚠️ Cập nhật 13/08/2026 — nền tảng đã chuyển sang .NET 10 (LTS).**
> Mục này giữ lại làm tham chiếu lịch sử. Kiến trúc đang có hiệu lực (bao gồm lý do chuyển
> .NET 8 → .NET 10, các quyết định ADR, và phân tích lỗ hổng của chính tài liệu SRS này)
> nằm ở **`docs/architecture/00-tech-stack-decisions.html`**. Kế hoạch triển khai chi tiết
> theo từng phase nằm ở **`docs/superpowers/plans/`**.
>
> Tóm tắt thay đổi: .NET 8 → **.NET 10 (LTS)**; **Controllers + MediatR (CQRS)**, giữ Clean
> Architecture 4 tầng (Domain ← Application ← Infrastructure ← Api); validation đầu vào dùng
> **FluentValidation** chạy trong pipeline behavior của MediatR;
> thêm **EF Core 10** (named query filter, `ToJson()` cho cột jsonb), **UUIDv7** cho khóa chính,
> **OpenAPI 3.1** (sinh client TypeScript tự động cho frontend), **ProblemDetails RFC 9457**,
> **`TimeProvider`** cho mọi logic phụ thuộc thời gian. PostgreSQL, Hangfire, ClosedXML,
> Next.js + Tailwind + shadcn/ui giữ nguyên như mô tả bên dưới.
>
> Hợp đồng API ở mục 7 **không đổi** — cùng đường dẫn, cùng payload, cùng mã lỗi; chỉ đổi
> cách hiện thực bên trong.

```
┌─────────────────────────┐        ┌──────────────────────────┐
│  Next.js (App Router)   │  HTTPS │   .NET 10 Web API         │
│  TypeScript + Tailwind  │◄──────►│   Clean Architecture      │
│  shadcn/ui              │        │   (Controllers /          │
└─────────────────────────┘        │    MediatR / EF Core 10)  │
                                     └──────────┬────────────────┘
                                                │
                          ┌─────────────────────┼─────────────────────┐
                          ▼                     ▼                     ▼
                 ┌────────────────┐   ┌──────────────────┐   ┌────────────────┐
                 │ PostgreSQL      │   │ File Storage      │   │ Background Job │
                 │ (EF Core)       │   │ (local/S3/MinIO)  │   │ (Hangfire)     │
                 │ dữ liệu nghiệp vụ│   │ ZIP, PPT, PDF     │   │ giải nén ZIP,   │
                 └────────────────┘   └──────────────────┘   │ similarity check,│
                                                               │ review queue job │
                                                               └────────────────┘
```

- **Xử lý file nộp bài:** upload theo từng slot → lưu file gốc → job nền xử lý riêng theo loại (giải nén + build `file_tree_json` + kiểm tra cấu trúc + similarity **chỉ với slot `.zip`**; các slot ảnh/video/tài liệu chỉ cần lưu trữ + tạo thumbnail/preview) → cập nhật trạng thái submission khi tất cả slot xử lý xong (tránh block request upload).
- **Similarity check:** so sánh mã nguồn dùng thuật toán hash theo khối (MinHash/Winnowing) — đủ dùng ở mức cảnh báo, không cần chính xác tuyệt đối.
- **Auth:** JWT, refresh token; SV đăng nhập bằng email trường hoặc MSSV + mật khẩu mặc định (đổi lần đầu).
- **Xuất Excel:** ClosedXML phía .NET, có thể define template theo mẫu nhà trường.

---

## 9. YÊU CẦU PHI CHỨC NĂNG

| Loại | Yêu cầu |
|---|---|
| Hiệu năng | Trang học (PPT/video) tải < 3s; xử lý ZIP nền, không chặn UI |
| Bảo mật | Validate loại file thực tế (không chỉ đuôi file) để chặn upload mã độc trong ZIP; giới hạn dung lượng chống DoS |
| Khả dụng | Chạy ổn định trong giờ hành chính, backup DB hằng ngày |
| Khả mở rộng | Dễ thêm loại `ChapterSection` mới, dễ thêm tiêu chí rubric |
| Khả bảo trì | Tách rõ Subject (nội dung) khỏi CourseOffering (dữ liệu theo học kỳ) để clone dễ dàng |
| Khả năng dùng | Giao diện SV tối giản, mobile-responsive vì SV hay xem trên điện thoại |

---

## 10. LỘ TRÌNH TRIỂN KHAI & BACKLOG THEO SPRINT

### MVP — Giai đoạn 1 (Dùng được ngay trong học kỳ)
| Sprint | Nội dung |
|---|---|
| S1 | Auth, CRUD Subject + SubjectVersion (draft/publish), CRUD Chapter/Lesson trong 1 version, upload PPT/PDF |
| S2 | CourseOffering, import Enrollment từ Excel, trang học của SV (xem tuần tự) |
| S3 | Assignment: cấu hình `SubmissionSlot` động, nộp bài theo slot (ưu tiên slot `.zip` trước — giải nén, checklist cấu trúc, xem file trên browser), sau đó mở rộng slot ảnh/video |
| S4 | Chấm bài theo rubric (đọc theo từng slot, hiện danh sách thành viên nếu bài nhóm), trả bài + nhận xét, Gradebook cơ bản (nhập tay + auto từ assignment), xuất Excel, StudentGroup cơ bản (chia nhóm, nộp bài nhóm), điểm cộng cơ bản (nhập tay lý do) |

### P1 — Giai đoạn 2
| Sprint | Nội dung |
|---|---|
| S5 | Quiz engine (checkpoint + graded), ngân hàng câu hỏi |
| S6 | Similarity check cho slot `.zip`, hint có trừ điểm, late penalty, điểm riêng từng thành viên nhóm (`SubmissionMemberScore`), Thư viện lý do điểm cộng (`BonusPointReasonTemplate`) + nút chọn nhanh trong màn hình chấm bài |
| S7 | Gamification: XP, huy hiệu, streak, bảng xếp hạng ẩn danh |
| S8 | Review queue (ôn tập ngắt quãng), recap section tự động |

### P2 — Giai đoạn 3
| Sprint | Nội dung |
|---|---|
| S9 | Dashboard nâng cao (SV nguy cơ, thống kê CĐR) |
| S10 | Chấm code tự động bằng test case (cho các bài lập trình có input/output rõ ràng) |
| S11 | Thông báo email/nhắc hạn, điểm danh QR |
| S12 | Tối ưu mobile, PWA |

---

## 11. RỦI RO & PHƯƠNG ÁN

| Rủi ro | Ảnh hưởng | Phương án |
|---|---|---|
| SV upload ZIP chứa mã độc | Ảnh hưởng server khi giải nén | Giải nén trong sandbox/container riêng, giới hạn tài nguyên, quét virus cơ bản |
| Dung lượng lưu trữ tăng nhanh (nhiều ZIP + PPT) | Chi phí lưu trữ | Nén thêm, định kỳ archive các học kỳ cũ ra cold storage |
| Similarity check false positive | Gây hiểu lầm đạo bài | Chỉ cảnh báo, giảng viên tự quyết định, không tự động trừ điểm |
| Giảng viên là người vừa dùng vừa code một mình | Thiếu thời gian | Ưu tiên nghiêm ngặt theo MVP, không làm P1/P2 trước khi MVP chạy ổn định 1 học kỳ |

---

*Hết tài liệu. Tài liệu này là cơ sở để triển khai — khi code, nên bắt đầu từ mô hình dữ liệu (mục 3) và API (mục 7) trước, sau đó dựng UI theo mục 6.*
