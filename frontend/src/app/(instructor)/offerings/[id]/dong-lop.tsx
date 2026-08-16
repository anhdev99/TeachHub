"use client";

import { useMutation } from "@tanstack/react-query";
import { Lock } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { apiFetch } from "@/lib/api/fetcher";
import type { CloseOfferingResultDto, OfferingDto } from "@/lib/api/types";

/**
 * Kết thúc lớp: đổi trạng thái sang `closed` và chốt một bản sổ điểm trong cùng một lệnh.
 *
 * Câu chữ trên nút và trong hộp xác nhận nói ĐÚNG những gì xảy ra, không hơn. Hiện tại trạng
 * thái `Closed` **chưa khoá thao tác nào** — sau khi đóng, giảng viên vẫn sửa được điểm và
 * sinh viên vẫn nộp được bài. Giá trị thật của thao tác này là **bản chốt**: một bản sao bảng
 * điểm kèm mã băm SHA-256, để sau này đối chiếu khi có khiếu nại. Viết "khoá lớp" lên nút là
 * hứa một thứ hệ thống chưa làm, và người tin vào lời hứa đó sẽ không kiểm lại gì nữa.
 */
export function DongLop({ lop }: { lop: OfferingDto }) {
  const router = useRouter();
  const [ketQua, setKetQua] = useState<CloseOfferingResultDto | null>(null);

  const dong = useMutation({
    mutationFn: () =>
      apiFetch<CloseOfferingResultDto>(`/api/offerings/${lop.id}/close`, { method: "POST" }),
    onSuccess: (kq) => {
      setKetQua(kq);
      // Không chốt được bản nào là điều PHẢI nói ra. Đóng lớp mà lặng lẽ không có bản đối
      // chiếu là đúng thứ người ta chỉ phát hiện vào lúc cần nó nhất.
      if (kq.snapshot) toast.success("Đã kết thúc lớp và chốt bảng điểm.");
      else toast.warning(kq.snapshot_skipped_reason ?? "Đã kết thúc lớp, chưa chốt bảng điểm.");
      router.refresh();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  if (lop.status === "closed" || ketQua) {
    return (
      <Badge variant="secondary" className="shrink-0 gap-1.5">
        <Lock className="size-3.5" aria-hidden />
        Đã kết thúc
      </Badge>
    );
  }

  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button variant="outline" size="sm" disabled={dong.isPending}>
          <Lock className="size-4" aria-hidden />
          {dong.isPending ? "Đang kết thúc…" : "Kết thúc lớp"}
        </Button>
      </AlertDialogTrigger>

      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Kết thúc lớp {lop.class_code}?</AlertDialogTitle>
          <AlertDialogDescription>
            Hệ thống sẽ chốt một bản sao bảng điểm hiện tại kèm mã băm, để về sau đối chiếu
            được nếu có khiếu nại điểm. Lớp chuyển sang trạng thái đã kết thúc.
            <br />
            <br />
            {/* Nói thẳng giới hạn. Người dùng đọc "kết thúc" sẽ mặc định là đã khoá; để họ
                phát hiện ra bằng cách vô tình sửa được điểm thì mất niềm tin vào cả cái nhãn. */}
            <strong>Chưa mở lại được</strong> sau khi kết thúc, và thao tác này{" "}
            <strong>chưa khoá việc sửa điểm hay nộp bài</strong> — nó chỉ chốt bản đối chiếu.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Huỷ</AlertDialogCancel>
          <AlertDialogAction onClick={() => dong.mutate()}>Kết thúc lớp</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
