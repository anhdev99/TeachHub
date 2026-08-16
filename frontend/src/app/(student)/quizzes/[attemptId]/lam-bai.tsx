"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { apiFetch } from "@/lib/api/fetcher";
import type { TakeAnswerDto, TakeAttemptDto } from "@/lib/api/types";

export function LamBai({ banDau }: { banDau: TakeAttemptDto }) {
  const qc = useQueryClient();
  const khoa = ["quiz-attempt", banDau.id] as const;

  const { data: luot } = useQuery({
    queryKey: khoa,
    queryFn: () => apiFetch<TakeAttemptDto>(`/api/quiz-attempts/${banDau.id}`),
    initialData: banDau,
  });

  const daNop = luot.status !== "in_progress";

  const [tra, setTra] = useState<Record<string, TakeAnswerDto>>(
    Object.fromEntries(luot.answers.map((a) => [a.question_id, a])),
  );

  // Đồng hồ chạy ở client chỉ để HIỂN THỊ. Máy chủ mới là nguồn chân lý về thời gian —
  // sửa đồng hồ máy hay đổi biến trong DevTools không kéo dài được một giây nào.
  const [conLai, setConLai] = useState<number | null>(luot.seconds_remaining ?? null);

  useEffect(() => {
    if (daNop || conLai === null) return;
    const t = setInterval(() => setConLai((v) => (v === null ? null : Math.max(0, v - 1))), 1000);
    return () => clearInterval(t);
  }, [daNop, conLai]);

  const luuNhap = useMutation({
    mutationFn: (ds: TakeAnswerDto[]) =>
      apiFetch(`/api/quiz-attempts/${luot.id}/answers`, {
        method: "PUT",
        body: JSON.stringify({ answers: ds }),
      }),
    onError: (e: Error) => {
      // Hết giờ giữa chừng: máy chủ đã chấm phần đã lưu, nên tải lại để thấy kết quả thật.
      toast.error(e.message);
      qc.invalidateQueries({ queryKey: khoa });
    },
  });

  const nop = useMutation({
    mutationFn: () =>
      apiFetch<TakeAttemptDto>(`/api/quiz-attempts/${luot.id}/submit`, { method: "POST" }),
    onSuccess: (kq) => {
      qc.setQueryData(khoa, kq);
      toast.success(
        kq.status === "auto_submitted" ? "Hết giờ — bài đã được chấm tự động." : "Đã nộp bài.",
      );
    },
    onError: (e: Error) => toast.error(e.message),
  });

  // Tự lưu sau MỖI thay đổi, gom nhóm 800 ms. Đợi tới lúc bấm nộp thì sinh viên rớt wifi
  // giữa bài sẽ mất trắng, và không có cách nào kiểm chứng để cho làm lại.
  const hen = useRef<ReturnType<typeof setTimeout> | null>(null);

  const datTra = useCallback(
    (moi: Record<string, TakeAnswerDto>) => {
      setTra(moi);
      if (hen.current) clearTimeout(hen.current);
      hen.current = setTimeout(() => luuNhap.mutate(Object.values(moi)), 800);
    },
    [luuNhap],
  );

  // Hết giờ ở phía hiển thị thì tự bấm nộp — máy chủ vẫn tự quyết định là Submitted hay
  // AutoSubmitted dựa trên mốc thời gian thật của nó.
  useEffect(() => {
    if (conLai === 0 && !daNop && !nop.isPending) nop.mutate();
  }, [conLai, daNop, nop]);

  const soDaTraLoi = Object.values(tra).filter(
    (a) => a.selected_option_ids.length > 0 || (a.text ?? "").trim().length > 0,
  ).length;

  return (
    <div className="flex flex-col gap-4">
      <div className="border-border flex flex-wrap items-center gap-3 rounded-lg border p-3">
        <span className="text-sm">Lượt {luot.attempt_no}</span>
        <span className="text-muted-foreground text-sm">
          Đã trả lời {soDaTraLoi}/{luot.questions.length}
        </span>

        {conLai !== null && !daNop && (
          <Badge variant={conLai < 60 ? "destructive" : "secondary"} className="tabular-nums">
            Còn {Math.floor(conLai / 60)}:{String(conLai % 60).padStart(2, "0")}
          </Badge>
        )}

        {daNop && luot.normalized_score !== null && (
          <Badge className="ml-auto">
            {luot.raw_score}/{luot.max_score} → {luot.normalized_score} điểm
          </Badge>
        )}

        {luuNhap.isPending && (
          <span className="text-muted-foreground ml-auto text-xs">Đang lưu…</span>
        )}
      </div>

      {luot.questions.map((q, i) => {
        const cua = tra[q.id] ?? { question_id: q.id, selected_option_ids: [], text: null };
        const xemLai = luot.review.find((r) => r.question_id === q.id);

        return (
          <Card key={q.id} className={xemLai ? (xemLai.is_correct ? "border-green-500" : "border-destructive") : undefined}>
            <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-2">
              <CardTitle className="text-base font-normal">
                <span className="text-muted-foreground">Câu {i + 1}. </span>
                {q.text}
              </CardTitle>
              <Badge variant="outline">{q.points} điểm</Badge>
            </CardHeader>

            <CardContent className="flex flex-col gap-2">
              {q.type === "fill_blank" ? (
                <Input
                  value={cua.text ?? ""}
                  disabled={daNop}
                  placeholder="Nhập câu trả lời"
                  onChange={(e) =>
                    datTra({ ...tra, [q.id]: { ...cua, text: e.target.value } })
                  }
                />
              ) : (
                q.options.map((o) => {
                  const chon = cua.selected_option_ids.includes(o.id);
                  const dung = xemLai?.correct_option_ids.includes(o.id);

                  return (
                    <label
                      key={o.id}
                      className={`flex items-center gap-2 rounded px-2 py-1.5 text-sm ${
                        dung ? "bg-green-500/10" : ""
                      } ${daNop ? "" : "hover:bg-muted cursor-pointer"}`}
                    >
                      <Checkbox
                        checked={chon}
                        disabled={daNop}
                        onCheckedChange={(c) => {
                          // Chọn-một và Đúng/Sai: tích ô này là bỏ mọi ô khác. Gửi lên hai
                          // lựa chọn cho câu chọn-một thì máy chủ chấm 0 điểm.
                          const ids =
                            q.type === "multiple_choice"
                              ? c === true
                                ? [...cua.selected_option_ids, o.id]
                                : cua.selected_option_ids.filter((x) => x !== o.id)
                              : c === true
                                ? [o.id]
                                : [];

                          datTra({ ...tra, [q.id]: { ...cua, selected_option_ids: ids } });
                        }}
                      />
                      {o.text}
                      {dung && <span className="text-xs text-green-600">đáp án đúng</span>}
                    </label>
                  );
                })
              )}

              {xemLai && (
                <div className="border-border mt-1 flex flex-col gap-1 border-t pt-2 text-sm">
                  <span className={xemLai.is_correct ? "text-green-600" : "text-destructive"}>
                    {xemLai.is_correct ? "✓ Đúng" : "✗ Sai"} · {xemLai.earned}/{xemLai.max} điểm
                  </span>
                  {xemLai.accepted_answers.length > 0 && (
                    <span className="text-muted-foreground">
                      Đáp án: {xemLai.accepted_answers.join(" · ")}
                    </span>
                  )}
                  {xemLai.explanation && (
                    <span className="text-muted-foreground">{xemLai.explanation}</span>
                  )}
                </div>
              )}
            </CardContent>
          </Card>
        );
      })}

      {!daNop && (
        <div className="flex flex-wrap items-center justify-between gap-3">
          <span className="text-muted-foreground text-sm">
            Đáp án được lưu tự động — tắt trình duyệt rồi vào lại vẫn còn nguyên.
          </span>
          <Button disabled={nop.isPending} onClick={() => nop.mutate()}>
            {nop.isPending ? "Đang nộp…" : "Nộp bài"}
          </Button>
        </div>
      )}
    </div>
  );
}
