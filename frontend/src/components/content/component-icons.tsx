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
 * Icon for each kind of component inside a lesson.
 *
 * Shared by BOTH roles. This table used to live only on the student screens, so the
 * instructor's editor showed nothing but text — looking at a 20-lesson chapter, you could not
 * tell which lesson carried an assignment and which carried a quiz. And if it were copied
 * into a second table, one day the same component type would end up with two different icons
 * on two different screens.
 */
export const COMPONENT_TYPE_ICONS: Record<
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
