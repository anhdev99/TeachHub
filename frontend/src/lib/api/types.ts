import type { components } from "./schema";

/**
 * Aliases for the schemas generated from /openapi/v1.json. Always derive them from
 * `components` rather than retyping them by hand: whenever the backend changes a DTO, running
 * `pnpm gen:api` makes TypeScript flag the error right at the place that uses it wrongly —
 * which is the entire reason the client-generation step exists.
 */
export type SubjectDto = components["schemas"]["SubjectDto"];
export type SubjectVersionDto = components["schemas"]["SubjectVersionDto"];
export type ChapterDto = components["schemas"]["ChapterDto"];
export type LessonDto = components["schemas"]["LessonDto"];
export type LessonComponentDto = components["schemas"]["LessonComponentDto"];
export type SubjectVersionStatus = components["schemas"]["SubjectVersionStatus"];
export type LessonComponentType = components["schemas"]["LessonComponentType"];

// Phase 2 — class offerings and the learning experience
export type OfferingDto = components["schemas"]["OfferingDto"];
/**
 * Result of closing an offering. `snapshot` is `null` when the class has no grade column to
 * freeze yet — in that case `snapshot_skipped_reason` carries a human-readable explanation,
 * and the UI MUST show it: closing a class while silently taking no snapshot is exactly the
 * kind of thing people only discover when they need the record to compare against.
 */
export type CloseOfferingResultDto = components["schemas"]["CloseOfferingResultDto"];
export type EnrollmentDto = components["schemas"]["EnrollmentDto"];
export type MyOfferingDto = components["schemas"]["MyOfferingDto"];
export type MyProgressDto = components["schemas"]["MyProgressDto"];
export type MyChapterDto = components["schemas"]["MyChapterDto"];
export type MyLessonDto = components["schemas"]["MyLessonDto"];
export type MyComponentDto = components["schemas"]["MyComponentDto"];
/** Inline question of a `checkpoint_quiz` component, DELIBERATELY without the correct
 * answers — see lesson-component.tsx. */
export type InlineQuestionDto = components["schemas"]["InlineQuestionDto"];
export type InlineAnswerDto = components["schemas"]["InlineAnswerDto"];
/**
 * Result of grading inline questions. `correct_indexes` and `explanation` appear ONLY here —
 * that is, only after the student has answered. They are deliberately absent from
 * `InlineQuestionDto` when the lesson loads, because shipping the answers down and hiding
 * them in the UI just means the whole class reads them in DevTools.
 */
export type CheckInlineQuestionsResultDto =
  components["schemas"]["CheckInlineQuestionsResultDto"];
export type InlineQuestionResultDto = components["schemas"]["InlineQuestionResultDto"];
export type LessonAccess = components["schemas"]["LessonAccess"];
export type LessonContentType = components["schemas"]["LessonContentType"];

// Phase 3 — assignments and submissions
export type AssignmentDto = components["schemas"]["AssignmentDto"];
export type SubmissionSlotDto = components["schemas"]["SubmissionSlotDto"];
export type SubmissionDto = components["schemas"]["SubmissionDto"];
export type SubmissionFileDto = components["schemas"]["SubmissionFileDto"];
export type MyAssignmentDto = components["schemas"]["MyAssignmentDto"];
export type MyAssignmentSubmissionDto = components["schemas"]["MyAssignmentSubmissionDto"];
export type FileTreeDto = components["schemas"]["FileTreeDto"];
export type FileProcessingStatus = components["schemas"]["FileProcessingStatus"];
export type SubmissionStatus = components["schemas"]["SubmissionStatus"];

// Phase 4a — grading, groups, final assessment, dashboard
export type GroupDto = components["schemas"]["GroupDto"];
export type GroupMemberDto = components["schemas"]["GroupMemberDto"];
export type ToGradeItemDto = components["schemas"]["ToGradeItemDto"];
export type GradedSubmissionDto = components["schemas"]["GradedSubmissionDto"];
export type FinalAssessmentDto = components["schemas"]["FinalAssessmentDto"];
export type InstructorDashboardDto = components["schemas"]["InstructorDashboardDto"];
export type RubricDto = components["schemas"]["RubricDto"];

// Phase 4b — dynamic gradebook
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

// Phase 5 — quizzes
export type QuizDto = components["schemas"]["QuizDto"];
export type QuizQuestionDto = components["schemas"]["QuizQuestionDto"];
export type MyQuizDto = components["schemas"]["MyQuizDto"];
export type TakeAttemptDto = components["schemas"]["TakeAttemptDto"];
export type TakeQuestionDto = components["schemas"]["TakeQuestionDto"];
export type TakeAnswerDto = components["schemas"]["TakeAnswerDto"];
export type QuestionType = components["schemas"]["QuestionType"];

export const QUESTION_TYPE_LABELS: Record<QuestionType, string> = {
  single_choice: "Chọn một",
  multiple_choice: "Chọn nhiều",
  true_false: "Đúng / Sai",
  fill_blank: "Điền khuyết",
};

export const AGGREGATION_KIND_LABELS: Record<AggregationKind, string> = {
  manual: "Nhập tay",
  average: "Trung bình",
  weighted_average: "Trung bình có trọng số",
  sum: "Cộng dồn",
  max: "Cao nhất",
  min: "Thấp nhất",
  drop_lowest_n: "Bỏ N thấp nhất",
  best_n_of_m: "Lấy N tốt nhất",
};

export const SUBMISSION_STATUS_LABELS: Record<SubmissionStatus, string> = {
  draft: "Bản nháp",
  submitted: "Đã nộp",
  grading: "Đang chấm",
  graded: "Đã chấm",
  returned: "Đã trả bài",
};

export const VERSION_STATUS_LABELS: Record<SubjectVersionStatus, string> = {
  draft: "Bản nháp",
  published: "Đã xuất bản",
  archived: "Đã lưu trữ",
};

export const COMPONENT_TYPE_LABELS: Record<LessonComponentType, string> = {
  recap: "Ôn lại",
  hook: "Dẫn nhập",
  checkpoint_quiz: "Trắc nghiệm nhanh",
  code_along: "Code cùng",
  assignment: "Bài tập",
  quiz: "Trắc nghiệm",
  reflection: "Suy ngẫm",
  custom_note: "Ghi chú",
};

/** Of the eight component types, these two must point at an existing record. */
export const TYPES_REQUIRING_REF: LessonComponentType[] = ["assignment", "quiz"];
