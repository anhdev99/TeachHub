"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, CircleAlert, Timer, X } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Progress } from "@/components/ui/progress";
import { apiFetch } from "@/lib/api/fetcher";
import { formatScore } from "@/lib/format";
import type { TakeAnswerDto, TakeAttemptDto } from "@/lib/api/types";
import { cn } from "@/lib/utils";

/** `9:05` — phút có thể vượt 60 với bài dài, và như thế vẫn đọc đúng. */
function dongHo(giay: number): string {
  return `${Math.floor(giay / 60)}:${String(giay % 60).padStart(2, "0")}`;
}

function daTraLoi(a: TakeAnswerDto | undefined): boolean {
  if (!a) return false;
  return a.selected_option_ids.length > 0 || (a.text ?? "").trim().length > 0;
}

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

  const [hoiNop, setHoiNop] = useState(false);

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

  // Giữ bản mới nhất của câu trả lời ngoài vòng render, để lần lưu hẹn giờ luôn gửi đúng
  // trạng thái cuối cùng chứ không phải bản chụp lúc lập lịch.
  const traRef = useRef(tra);

  /**
   * Cập nhật MỘT câu trả lời bằng hàm biến đổi, không nhận sẵn cả bảng.
   *
   * Bản trước nhận nguyên `Record` đã dựng sẵn, nên nơi gọi phải viết
   * `datTra({ ...tra, [q.id]: … })` — và `tra` ở đó là biến bắt được từ closure của LẦN
   * RENDER HIỆN TẠI. Hai câu trả lời trong cùng một khung render thì câu thứ hai dựng bảng
   * từ bản `tra` cũ, ghi đè mất câu thứ nhất; bản ghi đè đó còn là thứ được gửi lên máy chủ,
   * nên câu trả lời biến mất **cả trên giao diện lẫn trong bài đã nộp**.
   *
   * Không phải lỗi lý thuyết: với câu CHỌN NHIỀU, tích nhanh hai đáp án là đủ tái hiện.
   * Sinh viên thấy mình chọn hai ô, nộp xong được chấm như chỉ chọn một.
   */
  const datTra = useCallback(
    (questionId: string, bienDoi: (cu: TakeAnswerDto) => TakeAnswerDto) => {
      setTra((cu) => {
        const truoc = cu[questionId] ?? {
          question_id: questionId,
          selected_option_ids: [],
          text: null,
        };
        const moi = { ...cu, [questionId]: bienDoi(truoc) };
        // Gán vào ref là thao tác lũy đẳng nên an toàn khi React gọi updater hai lần
        // ở StrictMode: cùng `cu` vào thì cùng `moi` ra.
        traRef.current = moi;
        return moi;
      });

      if (hen.current) clearTimeout(hen.current);
      hen.current = setTimeout(() => luuNhap.mutate(Object.values(traRef.current)), 800);
    },
    [luuNhap],
  );

  // Hết giờ ở phía hiển thị thì tự bấm nộp — máy chủ vẫn tự quyết định là Submitted hay
  // AutoSubmitted dựa trên mốc thời gian thật của nó.
  useEffect(() => {
    if (conLai === 0 && !daNop && !nop.isPending) nop.mutate();
  }, [conLai, daNop, nop]);

  const soDaTraLoi = luot.questions.filter((q) => daTraLoi(tra[q.id])).length;
  const soBoTrong = luot.questions.length - soDaTraLoi;

  // Dưới 1 phút là đỏ, dưới 5 phút là hổ phách. Đổi màu ngay từ đầu thì màu cảnh báo mất
  // nghĩa; chỉ đổi lúc hết giờ thì báo quá muộn để làm được gì.
  const sacDongHo = conLai === null ? null : conLai < 60 ? "danger" : conLai < 300 ? "warn" : null;

  return (
    <div className="flex flex-col gap-4">
      {/* ── Thanh trạng thái dính ─────────────────────────────────────────────────────────
       * Đồng hồ phải LUÔN thấy được. Ở bản cũ nó nằm trong một khối cuộn mất từ câu hỏi thứ
       * hai trở đi, nên câu hỏi thật của sinh viên — "còn bao lâu?" — chỉ trả lời được bằng
       * cách cuộn ngược lên đầu trang. `top-14` = ngay dưới thanh trên dùng chung. */}
      <div className="bg-card/95 border-border sticky top-14 z-20 flex flex-col gap-2 rounded-xl border p-3 backdrop-blur">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
          <span className="text-muted-foreground text-sm">Lượt {luot.attempt_no}</span>

          <span className="text-sm tabular-nums">
            Đã trả lời <strong>{soDaTraLoi}</strong>/{luot.questions.length}
          </span>

          {luuNhap.isPending && (
            <span className="text-muted-foreground text-xs">Đang lưu…</span>
          )}

          {conLai !== null && !daNop && (
            <span
              className={cn(
                "ml-auto inline-flex min-h-8 items-center gap-1.5 rounded-lg border px-2.5 text-base font-semibold tabular-nums",
                sacDongHo === "danger"
                  ? "bg-danger-soft text-danger border-danger/25 animate-pulse"
                  : sacDongHo === "warn"
                    ? "bg-warn-soft text-warn border-warn/25"
                    : "border-border text-foreground",
              )}
              // Trình đọc màn hình không cần đọc lại từng giây; chỉ cần biết đây là đồng hồ.
              aria-label={`Thời gian còn lại ${dongHo(conLai)}`}
            >
              <Timer className="size-4 shrink-0" aria-hidden />
              {dongHo(conLai)}
            </span>
          )}

          {daNop && luot.normalized_score != null && (
            // Điểm tổng cố ý KHÔNG tô màu ngữ nghĩa. Xanh lá nghĩa là "đạt", mà hệ thống này
            // không có điểm sàn nào để nói ai đạt ai không — tô xanh cho "0/4 → 0 điểm" là
            // khen ngược, tô đỏ là tự đặt ra một mức trượt không tồn tại. Đúng/Sai từng câu
            // bên dưới đã mang màu rồi.
            <span className="border-border ml-auto rounded-lg border px-2.5 py-1 text-sm font-semibold tabular-nums">
              {formatScore(luot.raw_score)}/{formatScore(luot.max_score)} → {formatScore(luot.normalized_score)} điểm
            </span>
          )}
        </div>

        <Progress value={(soDaTraLoi / Math.max(1, luot.questions.length)) * 100} className="h-1" />

        {/* Bảng số câu: câu đã trả lời tô đặc, câu bỏ trống để rỗng. Không có nó thì "còn câu
            nào chưa làm" chỉ trả lời được bằng cách cuộn hết bài một lượt. */}
        {!daNop && luot.questions.length > 1 && (
          <div className="flex flex-wrap gap-1.5">
            {luot.questions.map((q, i) => {
              const xong = daTraLoi(tra[q.id]);
              return (
                <a
                  key={q.id}
                  href={`#cau-${i + 1}`}
                  aria-label={`Câu ${i + 1}${xong ? " — đã trả lời" : " — chưa trả lời"}`}
                  className={cn(
                    "grid size-11 place-items-center rounded-lg border text-sm font-medium tabular-nums transition-colors",
                    xong
                      ? "bg-primary text-primary-foreground border-primary"
                      : "border-border text-muted-foreground hover:bg-muted",
                  )}
                >
                  {i + 1}
                </a>
              );
            })}
          </div>
        )}
      </div>

      {luot.questions.map((q, i) => {
        const cua = tra[q.id] ?? { question_id: q.id, selected_option_ids: [], text: null };
        const xemLai = luot.review.find((r) => r.question_id === q.id);

        return (
          <Card
            key={q.id}
            id={`cau-${i + 1}`}
            // `scroll-mt` để khi nhảy từ bảng số câu, tiêu đề câu không chui xuống dưới hai
            // thanh dính phía trên.
            className={cn(
              "scroll-mt-40",
              xemLai && (xemLai.is_correct ? "ring-ok/40 ring-2" : "ring-danger/40 ring-2"),
            )}
          >
            <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-2">
              <CardTitle className="min-w-0 flex-1 text-base font-normal text-pretty">
                <span className="text-muted-foreground tabular-nums">Câu {i + 1}. </span>
                {q.text}
              </CardTitle>
              <Badge variant="outline" className="shrink-0 tabular-nums">
                {formatScore(q.points)} điểm
              </Badge>
            </CardHeader>

            <CardContent className="flex flex-col gap-2">
              {q.type === "fill_blank" ? (
                <Input
                  value={cua.text ?? ""}
                  disabled={daNop}
                  placeholder="Nhập câu trả lời"
                  className="h-11"
                  onChange={(e) =>
                    datTra(q.id, (cu) => ({ ...cu, text: e.target.value }))
                  }
                />
              ) : (
                q.options.map((o) => {
                  const chon = cua.selected_option_ids.includes(o.id);
                  const dung = xemLai?.correct_option_ids.includes(o.id);

                  return (
                    <label
                      key={o.id}
                      className={cn(
                        "flex min-h-11 items-center gap-2.5 rounded-lg border px-3 py-2 text-sm transition-colors",
                        dung
                          ? "bg-ok-soft border-ok/30"
                          : chon
                            ? "border-primary bg-accent"
                            : "border-border",
                        daNop ? "" : "hover:bg-muted cursor-pointer",
                      )}
                    >
                      <Checkbox
                        checked={chon}
                        disabled={daNop}
                        onCheckedChange={(c) => {
                          // Danh sách lựa chọn phải tính TỪ BÊN TRONG hàm biến đổi, dựa vào
                          // `cu` mà React đưa vào — không phải từ `cua` bắt được lúc render.
                          // Tích nhanh hai ô của một câu chọn nhiều là hai lần gọi trong cùng
                          // một khung render, và bản dựng từ `cua` sẽ nuốt mất ô trước.
                          datTra(q.id, (cu) => ({
                            ...cu,
                            // Chọn-một và Đúng/Sai: tích ô này là bỏ mọi ô khác. Gửi lên hai
                            // lựa chọn cho câu chọn-một thì máy chủ chấm 0 điểm.
                            selected_option_ids:
                              q.type === "multiple_choice"
                                ? c === true
                                  ? [...cu.selected_option_ids, o.id]
                                  : cu.selected_option_ids.filter((x) => x !== o.id)
                                : c === true
                                  ? [o.id]
                                  : [],
                          }));
                        }}
                      />
                      <span className="flex-1 text-pretty">{o.text}</span>
                      {dung && (
                        <span className="text-ok shrink-0 text-xs font-medium">đáp án đúng</span>
                      )}
                    </label>
                  );
                })
              )}

              {xemLai && (
                <div className="border-border mt-1 flex flex-col gap-1 border-t pt-2 text-sm">
                  <span
                    className={cn(
                      "inline-flex items-center gap-1.5 font-medium",
                      xemLai.is_correct ? "text-ok" : "text-danger",
                    )}
                  >
                    {xemLai.is_correct ? (
                      <Check className="size-4 shrink-0" aria-hidden />
                    ) : (
                      <X className="size-4 shrink-0" aria-hidden />
                    )}
                    {xemLai.is_correct ? "Đúng" : "Sai"} · {formatScore(xemLai.earned)}/{formatScore(xemLai.max)}{" "}
                    điểm
                  </span>
                  {xemLai.accepted_answers.length > 0 && (
                    <span className="text-muted-foreground text-pretty">
                      Đáp án: {xemLai.accepted_answers.join(" · ")}
                    </span>
                  )}
                  {xemLai.explanation && (
                    <span className="text-muted-foreground text-pretty">{xemLai.explanation}</span>
                  )}
                </div>
              )}
            </CardContent>
          </Card>
        );
      })}

      {!daNop && (
        <div className="flex flex-col gap-2.5">
          <p className="text-muted-foreground text-sm text-pretty">
            Đáp án được lưu tự động — tắt trình duyệt rồi vào lại vẫn còn nguyên.
          </p>
          <Button
            disabled={nop.isPending}
            // Còn câu bỏ trống thì HỎI trước. Bấm nhầm nút nộp lúc đang vội là mất luôn lượt
            // làm, và không có đường hoàn tác nào.
            onClick={() => (soBoTrong > 0 ? setHoiNop(true) : nop.mutate())}
            className="min-h-11 w-full sm:w-fit sm:self-start"
          >
            {nop.isPending ? "Đang nộp…" : "Nộp bài"}
          </Button>
        </div>
      )}

      <Dialog open={hoiNop} onOpenChange={setHoiNop}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <CircleAlert className="text-warn size-5 shrink-0" aria-hidden />
              Còn {soBoTrong} câu chưa trả lời
            </DialogTitle>
            <DialogDescription>
              Câu bỏ trống được tính 0 điểm và không sửa lại được sau khi nộp. Bạn vẫn muốn nộp
              bài chứ?
            </DialogDescription>
          </DialogHeader>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button
              variant="outline"
              onClick={() => setHoiNop(false)}
              className="min-h-11 w-full sm:w-fit"
            >
              Quay lại làm tiếp
            </Button>
            <Button
              disabled={nop.isPending}
              onClick={() => {
                setHoiNop(false);
                nop.mutate();
              }}
              className="min-h-11 w-full sm:w-fit"
            >
              Vẫn nộp
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
