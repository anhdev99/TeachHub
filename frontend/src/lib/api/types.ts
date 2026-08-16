import type { components } from "./schema";

/**
 * Bí danh cho các schema sinh tự động từ /openapi/v1.json. Luôn dẫn xuất từ `components`
 * chứ không gõ tay lại: mỗi lần backend đổi DTO, chạy `pnpm gen:api` là TypeScript báo lỗi
 * ngay tại chỗ dùng sai — đó là toàn bộ lý do tồn tại của bước sinh client.
 */
export type SubjectDto = components["schemas"]["SubjectDto"];
export type SubjectVersionDto = components["schemas"]["SubjectVersionDto"];
export type ChapterDto = components["schemas"]["ChapterDto"];
export type LessonDto = components["schemas"]["LessonDto"];
export type LessonComponentDto = components["schemas"]["LessonComponentDto"];
export type SubjectVersionStatus = components["schemas"]["SubjectVersionStatus"];
export type LessonComponentType = components["schemas"]["LessonComponentType"];

// Phase 2 — lớp học phần và trải nghiệm học
export type OfferingDto = components["schemas"]["OfferingDto"];
/**
 * Kết quả đóng lớp. `snapshot` là `null` khi lớp chưa có cột điểm nào để chốt — lúc đó
 * `snapshot_skipped_reason` mang câu giải thích đọc được, và giao diện PHẢI hiện nó ra:
 * đóng lớp mà lặng lẽ không chốt bản nào là đúng thứ người ta chỉ phát hiện khi cần đối chiếu.
 */
export type CloseOfferingResultDto = components["schemas"]["CloseOfferingResultDto"];
export type EnrollmentDto = components["schemas"]["EnrollmentDto"];
export type MyOfferingDto = components["schemas"]["MyOfferingDto"];
export type MyProgressDto = components["schemas"]["MyProgressDto"];
export type MyChapterDto = components["schemas"]["MyChapterDto"];
export type MyLessonDto = components["schemas"]["MyLessonDto"];
export type MyComponentDto = components["schemas"]["MyComponentDto"];
/** Câu hỏi nhúng của phần `checkpoint_quiz`, CỐ Ý không kèm đáp án đúng — xem phan-bai-hoc.tsx. */
export type InlineQuestionDto = components["schemas"]["InlineQuestionDto"];
export type InlineAnswerDto = components["schemas"]["InlineAnswerDto"];
/**
 * Kết quả chấm câu hỏi nhúng. `correct_indexes` và `explanation` CHỈ có ở đây — tức là sau
 * khi sinh viên đã trả lời. Chúng cố ý vắng mặt trong `InlineQuestionDto` lúc tải bài, vì
 * gửi đáp án xuống rồi ẩn trên giao diện nghĩa là cả lớp mở DevTools ra đọc.
 */
export type CheckInlineQuestionsResultDto =
  components["schemas"]["CheckInlineQuestionsResultDto"];
export type InlineQuestionResultDto = components["schemas"]["InlineQuestionResultDto"];
export type LessonAccess = components["schemas"]["LessonAccess"];
export type LessonContentType = components["schemas"]["LessonContentType"];

// Phase 3 — bài tập và nộp bài
export type AssignmentDto = components["schemas"]["AssignmentDto"];
export type SubmissionSlotDto = components["schemas"]["SubmissionSlotDto"];
export type SubmissionDto = components["schemas"]["SubmissionDto"];
export type SubmissionFileDto = components["schemas"]["SubmissionFileDto"];
export type MyAssignmentDto = components["schemas"]["MyAssignmentDto"];
export type MyAssignmentSubmissionDto = components["schemas"]["MyAssignmentSubmissionDto"];
export type FileTreeDto = components["schemas"]["FileTreeDto"];
export type FileProcessingStatus = components["schemas"]["FileProcessingStatus"];
export type SubmissionStatus = components["schemas"]["SubmissionStatus"];

// Phase 4a — chấm điểm, nhóm, đợt cuối môn, dashboard
export type GroupDto = components["schemas"]["GroupDto"];
export type GroupMemberDto = components["schemas"]["GroupMemberDto"];
export type ToGradeItemDto = components["schemas"]["ToGradeItemDto"];
export type GradedSubmissionDto = components["schemas"]["GradedSubmissionDto"];
export type FinalAssessmentDto = components["schemas"]["FinalAssessmentDto"];
export type InstructorDashboardDto = components["schemas"]["InstructorDashboardDto"];
export type RubricDto = components["schemas"]["RubricDto"];

// Phase 4b — sổ điểm động
export type GradebookDto = components["schemas"]["GradebookDto"];
export type GradeColumnDto = components["schemas"]["GradeColumnDto"];
export type GradeRowDto = components["schemas"]["GradeRowDto"];
export type GradeCellDto = components["schemas"]["GradeCellDto"];
export type BonusPointDto = components["schemas"]["BonusPointDto"];
export type BonusReasonTemplateDto = components["schemas"]["BonusReasonTemplateDto"];
export type GradeSchemeTemplateDto = components["schemas"]["GradeSchemeTemplateDto"];
export type GradeExportTemplateDto = components["schemas"]["GradeExportTemplateDto"];
export type AggregationKind = components["schemas"]["AggregationKind"];
export type BonusTarget = components["schemas"]["BonusTarget"];

// Phase 5 — trắc nghiệm
export type QuizDto = components["schemas"]["QuizDto"];
export type QuizQuestionDto = components["schemas"]["QuizQuestionDto"];
export type MyQuizDto = components["schemas"]["MyQuizDto"];
export type TakeAttemptDto = components["schemas"]["TakeAttemptDto"];
export type TakeQuestionDto = components["schemas"]["TakeQuestionDto"];
export type TakeAnswerDto = components["schemas"]["TakeAnswerDto"];
export type QuestionType = components["schemas"]["QuestionType"];

export const NHAN_LOAI_CAU_HOI: Record<QuestionType, string> = {
  single_choice: "Chọn một",
  multiple_choice: "Chọn nhiều",
  true_false: "Đúng / Sai",
  fill_blank: "Điền khuyết",
};

export const NHAN_QUY_TAC_GOP: Record<AggregationKind, string> = {
  manual: "Nhập tay",
  average: "Trung bình",
  weighted_average: "Trung bình có trọng số",
  sum: "Cộng dồn",
  max: "Cao nhất",
  min: "Thấp nhất",
  drop_lowest_n: "Bỏ N thấp nhất",
  best_n_of_m: "Lấy N tốt nhất",
};

export const NHAN_TRANG_THAI_NOP: Record<SubmissionStatus, string> = {
  draft: "Bản nháp",
  submitted: "Đã nộp",
  grading: "Đang chấm",
  graded: "Đã chấm",
  returned: "Đã trả bài",
};

export const NHAN_TRANG_THAI: Record<SubjectVersionStatus, string> = {
  draft: "Bản nháp",
  published: "Đã xuất bản",
  archived: "Đã lưu trữ",
};

export const NHAN_LOAI_COMPONENT: Record<LessonComponentType, string> = {
  recap: "Ôn lại",
  hook: "Dẫn nhập",
  checkpoint_quiz: "Trắc nghiệm nhanh",
  code_along: "Code cùng",
  assignment: "Bài tập",
  quiz: "Trắc nghiệm",
  reflection: "Suy ngẫm",
  custom_note: "Ghi chú",
};

/** Tám loại component; hai loại đầu bảng phải trỏ tới một bản ghi có sẵn. */
export const LOAI_CAN_REF: LessonComponentType[] = ["assignment", "quiz"];
