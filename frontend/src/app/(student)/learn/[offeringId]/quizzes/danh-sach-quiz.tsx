"use client";

import { useMutation } from "@tanstack/react-query";
import { ClipboardList, ListChecks, Timer, Trophy } from "lucide-react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/shell/page-shell";
import { apiFetch } from "@/lib/api/fetcher";
import { formatScore } from "@/lib/format";
import type { MyQuizDto, TakeAttemptDto } from "@/lib/api/types";
import { cn } from "@/lib/utils";

export function DanhSachQuiz({
  offeringId,
  banDau,
}: {
  offeringId: string;
  banDau: MyQuizDto[];
}) {
  const router = useRouter();

  const batDau = useMutation({
    mutationFn: (quizId: string) =>
      apiFetch<TakeAttemptDto>(`/api/quizzes/${quizId}/attempts`, { method: "POST" }),
    onSuccess: (luot) => router.push(`/quizzes/${luot.id}?offering=${offeringId}`),
    onError: (e: Error) => toast.error(e.message),
  });

  if (banDau.length === 0) {
    return (
      <EmptyState
        Icon={ClipboardList}
        title="Lớp này chưa có bài trắc nghiệm nào"
        description="Bài trắc nghiệm sẽ hiện ở đây ngay khi giảng viên mở."
      />
    );
  }

  return (
    <ul className="flex flex-col gap-3">
      {banDau.map((q) => {
        // `!= null` chứ không `!== null`: API bỏ hẳn khoá khỏi JSON khi giá trị rỗng, nên ở
        // client giá trị là `undefined`. Phép so sánh nghiêm ngặt cho `undefined !== null` là
        // đúng, và màn hình từng hiện "Đã làm 0/undefined lượt" vì đúng lý do đó.
        const gioiHanLuot = q.max_attempts ?? null;
        const hetLuot = gioiHanLuot !== null && q.attempts_used >= gioiHanLuot;
        const chuaCoCauHoi = q.question_count === 0;

        return (
          <li
            key={q.id}
            className="bg-card border-border flex flex-col gap-3 rounded-xl border p-3 sm:p-4"
          >
            <div className="flex flex-col gap-2">
              <div className="flex flex-wrap items-center gap-2">
                <h2 className="min-w-0 flex-1 font-medium text-pretty">{q.title}</h2>
                {q.is_graded ? (
                  <Badge variant="secondary">Tính điểm</Badge>
                ) : (
                  <Badge variant="outline">Luyện tập</Badge>
                )}
              </div>

              {q.description && (
                <p className="text-muted-foreground text-sm text-pretty">{q.description}</p>
              )}
            </div>

            <div className="text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1.5 text-sm">
              <span className="inline-flex items-center gap-1.5">
                <ListChecks className="size-3.5 shrink-0" aria-hidden />
                {q.question_count} câu
              </span>

              {q.time_limit_minutes != null && (
                <span className="inline-flex items-center gap-1.5">
                  <Timer className="size-3.5 shrink-0" aria-hidden />
                  {q.time_limit_minutes} phút
                </span>
              )}

              <span className="tabular-nums">
                Đã làm {q.attempts_used}
                {gioiHanLuot !== null ? `/${gioiHanLuot}` : ""} lượt
              </span>

              {q.best_normalized_score != null && (
                // Chỉ tô xanh khi điểm thật sự có gì để mừng: "Cao nhất 0" màu xanh lá đọc
                // như một lời khen cho đúng kết quả tệ nhất có thể.
                <span
                  className={cn(
                    "inline-flex items-center gap-1.5 font-medium tabular-nums",
                    q.best_normalized_score > 0 && "text-ok",
                  )}
                >
                  <Trophy className="size-3.5 shrink-0" aria-hidden />
                  Cao nhất {formatScore(q.best_normalized_score)}
                </span>
              )}
            </div>

            {q.in_progress_attempt_id ? (
              <div className="flex flex-wrap items-center gap-2">
                <Button
                  className="min-h-11 w-full sm:w-fit"
                  onClick={() =>
                    router.push(`/quizzes/${q.in_progress_attempt_id}?offering=${offeringId}`)
                  }
                >
                  Làm tiếp lượt đang dở
                </Button>
              </div>
            ) : (
              <div className="flex flex-col gap-1.5">
                <Button
                  disabled={hetLuot || chuaCoCauHoi || batDau.isPending}
                  onClick={() => batDau.mutate(q.id)}
                  className="min-h-11 w-full sm:w-fit"
                >
                  {batDau.isPending ? "Đang mở…" : hetLuot ? "Hết lượt làm" : "Bắt đầu làm"}
                </Button>

                {/* Nút xám mà không nói vì sao là chỗ người dùng bấm đi bấm lại rồi báo lỗi. */}
                {chuaCoCauHoi && (
                  <p className="text-muted-foreground text-xs">
                    Bài này chưa có câu hỏi nào — chờ giảng viên soạn xong.
                  </p>
                )}
                {hetLuot && !chuaCoCauHoi && (
                  <p className="text-muted-foreground text-xs">
                    Bạn đã dùng hết {gioiHanLuot} lượt làm của bài này.
                  </p>
                )}
              </div>
            )}
          </li>
        );
      })}
    </ul>
  );
}
