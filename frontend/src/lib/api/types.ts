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
