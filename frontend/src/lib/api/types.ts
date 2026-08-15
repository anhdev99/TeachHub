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
export type EnrollmentDto = components["schemas"]["EnrollmentDto"];
export type MyOfferingDto = components["schemas"]["MyOfferingDto"];
export type MyProgressDto = components["schemas"]["MyProgressDto"];
export type MyChapterDto = components["schemas"]["MyChapterDto"];
export type MyLessonDto = components["schemas"]["MyLessonDto"];
export type MyComponentDto = components["schemas"]["MyComponentDto"];
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
