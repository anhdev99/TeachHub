import {
  ClipboardList,
  Code2,
  FileText,
  ListChecks,
  MessageSquareQuote,
  RotateCcw,
  Sparkles,
  StickyNote,
} from "lucide-react";
import type { LessonComponentType } from "@/lib/api/types";

/**
 * Icon cho từng loại phần trong bài học.
 *
 * Dùng chung cho CẢ hai vai. Trước đây bảng này chỉ tồn tại ở màn hình sinh viên, nên trình
 * soạn thảo của giảng viên hiện toàn chữ — nhìn một chương 20 bài không phân biệt nổi bài nào
 * có bài tập, bài nào có trắc nghiệm. Và nếu chép sang thành bản thứ hai thì một ngày nào đó
 * cùng một loại phần sẽ mang hai icon khác nhau ở hai màn hình.
 */
export const ICON_LOAI: Record<
  LessonComponentType,
  React.ComponentType<{ className?: string }>
> = {
  recap: RotateCcw,
  hook: Sparkles,
  checkpoint_quiz: ListChecks,
  code_along: Code2,
  assignment: FileText,
  quiz: ClipboardList,
  reflection: MessageSquareQuote,
  custom_note: StickyNote,
};
