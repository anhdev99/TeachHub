import { TriangleAlert } from "lucide-react";
import type { ReactNode } from "react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";

/**
 * Dòng cảnh báo bắt buộc của mọi màn hình trùng lặp (Rủi ro #3 của SRS).
 *
 * Nằm ở một component dùng chung chứ không chép sang từng trang: hai bản chép sẽ trôi xa nhau,
 * và bản nhẹ lời hơn sẽ là bản người dùng nhớ. Con số phần trăm là gợi ý để ĐỌC bài, không phải
 * bằng chứng — bài nhập môn dùng chung mã khởi đầu trùng 70–90% một cách hoàn toàn chính đáng.
 *
 * MÀU HỔ PHÁCH (`warn`), CỐ Ý KHÔNG PHẢI ĐỎ. Đỏ trong hệ màu này nghĩa là lỗi và nguy hiểm —
 * dùng nó ở đây là ra một phán quyết bằng màu sắc, nói ngược lại đúng câu chữ bên trong khung.
 * Đây cũng là màu của phần tô sáng đoạn trùng trong `globals.css`, nên khung cảnh báo và vệt
 * vàng trong mã nguồn đọc như cùng một tín hiệu.
 */
export function CanhBaoTrungLap({ children }: { children?: ReactNode }) {
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
