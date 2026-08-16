import { TriangleAlert } from "lucide-react";
import type { ReactNode } from "react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";

/**
 * The mandatory disclaimer on every similarity screen (SRS Risk #3).
 *
 * It lives in one shared component instead of being copied into each page: two copies drift
 * apart, and the softer-worded one is the one users will remember. The percentage is a hint to
 * go READ the code, not evidence — intro-level assignments sharing the same starter code hit
 * 70–90% similarity entirely legitimately.
 *
 * AMBER (`warn`), DELIBERATELY NOT RED. In this color system red means error and danger —
 * using it here delivers a verdict through color, contradicting the exact words inside the
 * box. Amber is also the color of the matched-range highlight in `globals.css`, so the alert
 * box and the yellow streaks in the source code read as one signal.
 */
export function SimilarityNotice({ children }: { children?: ReactNode }) {
  return (
    <Alert className="bg-warn-soft border-warn/25 items-start px-3.5 py-3">
      <TriangleAlert className="text-warn" aria-hidden />
      <AlertTitle className="text-warn text-pretty">
        Đây là gợi ý để đọc kỹ, không phải kết luận
      </AlertTitle>
      <AlertDescription className="text-foreground/80 text-pretty">
        Hệ thống <strong className="font-medium">không bao giờ tự trừ điểm</strong> và không tự
        kết luận sao chép. Mã khởi đầu do chính bạn phát, thư viện chung hay đoạn mẫu trong bài
        giảng đều làm hai bài trùng cao một cách chính đáng — hãy mở mã ra đọc trước khi quyết
        định.
        {children}
      </AlertDescription>
    </Alert>
  );
}
