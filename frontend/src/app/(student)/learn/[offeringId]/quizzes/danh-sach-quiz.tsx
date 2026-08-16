"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { apiFetch } from "@/lib/api/fetcher";
import type { MyQuizDto, TakeAttemptDto } from "@/lib/api/types";

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
    return <p className="text-muted-foreground text-sm">Lớp này chưa có bài trắc nghiệm nào.</p>;
  }

  return (
    <ul className="flex flex-col gap-3">
      {banDau.map((q) => {
        const hetLuot = q.max_attempts !== null && q.attempts_used >= q.max_attempts;

        return (
          <li key={q.id}>
            <Card>
              <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
                <CardTitle className="text-base">{q.title}</CardTitle>
                <div className="flex flex-wrap gap-2">
                  {q.is_graded ? <Badge>Tính điểm</Badge> : <Badge variant="secondary">Luyện tập</Badge>}
                  {q.best_normalized_score !== null && (
                    <Badge variant="outline">Cao nhất: {q.best_normalized_score}</Badge>
                  )}
                </div>
              </CardHeader>

              <CardContent className="flex flex-wrap items-center justify-between gap-3">
                <span className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-1 text-sm">
                  <span>{q.question_count} câu</span>
                  {q.time_limit_minutes !== null && <span>{q.time_limit_minutes} phút</span>}
                  <span>
                    Đã làm {q.attempts_used}
                    {q.max_attempts !== null && `/${q.max_attempts}`} lượt
                  </span>
                </span>

                {q.in_progress_attempt_id ? (
                  <Button
                    size="sm"
                    onClick={() =>
                      router.push(`/quizzes/${q.in_progress_attempt_id}?offering=${offeringId}`)
                    }
                  >
                    Làm tiếp
                  </Button>
                ) : (
                  <Button
                    size="sm"
                    disabled={hetLuot || batDau.isPending || q.question_count === 0}
                    onClick={() => batDau.mutate(q.id)}
                  >
                    {hetLuot ? "Hết lượt" : "Bắt đầu làm"}
                  </Button>
                )}
              </CardContent>
            </Card>
          </li>
        );
      })}
    </ul>
  );
}
