"use client";

import { useMutation } from "@tanstack/react-query";
import { Award, Flame, Medal, RotateCcw, Zap } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { apiFetch } from "@/lib/api/fetcher";
import type { components } from "@/lib/api/schema";
import { cn } from "@/lib/utils";

type MyGamificationDto = components["schemas"]["MyGamificationDto"];
type LeaderboardDto = components["schemas"]["LeaderboardDto"];
type TodayReviewDto = components["schemas"]["TodayReviewDto"];
type ReviewItemDto = components["schemas"]["ReviewItemDto"];

export function TienDoCaNhan({
  me,
  bangXepHang,
  onTap,
}: {
  me: MyGamificationDto;
  bangXepHang: LeaderboardDto | null;
  onTap: TodayReviewDto | null;
}) {
  const router = useRouter();
  const [mucDangLam, setMucDangLam] = useState<ReviewItemDto | null>(null);

  const traLoi = useMutation({
    mutationFn: (v: { id: string; dung: boolean }) =>
      apiFetch(`/api/review/items/${v.id}/answer`, {
        method: "POST",
        body: JSON.stringify({ is_correct: v.dung }),
      }),
    onSuccess: (_d, v) => {
      toast.success(
        v.dung ? "Đúng — lịch ôn kế tiếp giãn ra." : "Sai — câu này sẽ quay lại sau 3 ngày.",
      );
      setMucDangLam(null);
      router.refresh();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <div className="flex flex-col gap-5">
      {/* Hai cột ở 390px, bốn cột từ sm: bốn thẻ trên một hàng điện thoại thì con số phải thu
          nhỏ tới mức không còn là "con số nổi bật" nữa. */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <TheSo nhan="Tổng XP" so={me.total_xp} Icon={Zap} />
        <TheSo
          nhan="Chuỗi ngày"
          so={me.current_streak_days}
          Icon={Flame}
          phu={`dài nhất ${me.longest_streak_days}`}
        />
        <TheSo nhan="Huy hiệu" so={me.badges.length} Icon={Award} />
        <TheSo
          nhan="Xếp hạng"
          so={me.rank ?? "—"}
          Icon={Medal}
          phu={me.total_participants > 0 ? `trên ${me.total_participants}` : undefined}
        />
      </div>

      {onTap && onTap.total_due > 0 && (
        <Card className="ring-primary/40 ring-2">
          <CardHeader>
            <CardTitle className="flex flex-wrap items-center gap-2 text-base">
              <RotateCcw className="size-4 shrink-0" aria-hidden />
              Ôn tập hôm nay
              <Badge variant="secondary" className="tabular-nums">
                {onTap.total_due} câu
              </Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <p className="text-muted-foreground text-sm text-pretty">
              Đây là những câu bạn từng trả lời sai. Trả lời đúng thì lần ôn sau giãn ra; sai
              thì quay lại sau 3 ngày.
            </p>

            {mucDangLam ? (
              <div className="border-border flex flex-col gap-3 rounded-xl border p-3">
                <p className="font-medium text-pretty">{mucDangLam.question?.text}</p>
                <p className="text-muted-foreground text-sm">
                  Tự chấm: bạn có nhớ câu trả lời không?
                </p>
                <div className="flex flex-col gap-2 sm:flex-row">
                  <Button
                    disabled={traLoi.isPending}
                    onClick={() => traLoi.mutate({ id: mucDangLam.id, dung: true })}
                    className="min-h-11 sm:flex-1"
                  >
                    Nhớ
                  </Button>
                  <Button
                    variant="outline"
                    disabled={traLoi.isPending}
                    onClick={() => traLoi.mutate({ id: mucDangLam.id, dung: false })}
                    className="min-h-11 sm:flex-1"
                  >
                    Chưa nhớ
                  </Button>
                  <Button
                    variant="ghost"
                    onClick={() => setMucDangLam(null)}
                    className="min-h-11 sm:w-fit"
                  >
                    Để sau
                  </Button>
                </div>
              </div>
            ) : (
              <ul className="flex flex-col gap-2">
                {onTap.items.map((m) => (
                  <li
                    key={m.id}
                    className="border-border flex flex-col gap-2 rounded-xl border p-3 sm:flex-row sm:items-center"
                  >
                    <span className="min-w-0 flex-1 text-sm text-pretty">
                      {m.question?.text ?? "(câu đã bị xóa)"}
                    </span>
                    <div className="flex items-center gap-2">
                      <Badge variant="outline" className="shrink-0">
                        {m.stage}
                      </Badge>
                      <Button
                        onClick={() => setMucDangLam(m)}
                        className="min-h-11 flex-1 sm:flex-none"
                      >
                        Ôn
                      </Button>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      )}

      {me.badges.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Huy hiệu đã đạt</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {me.badges.map((b) => (
              <span
                key={b.badge_id}
                className="bg-ok-soft text-ok border-ok/25 inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-sm font-medium"
              >
                <span aria-hidden>{b.icon}</span>
                {b.name}
              </span>
            ))}
          </CardContent>
        </Card>
      )}

      {bangXepHang && bangXepHang.rows.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Bảng xếp hạng lớp</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-0.5">
            {/* Tên người khác do MÁY CHỦ ẩn, không phải giao diện ẩn: payload không chứa tên
                thật, nên mở tab Network cũng không thấy gì. */}
            {bangXepHang.rows.map((r) => (
              <div
                key={r.rank}
                className={cn(
                  "flex min-h-11 items-center gap-2 rounded-lg px-2 text-sm",
                  r.is_me && "bg-accent text-accent-foreground font-medium",
                )}
              >
                <span className="text-muted-foreground w-7 shrink-0 text-right tabular-nums">
                  {r.rank}
                </span>
                <span className="min-w-0 flex-1 truncate">{r.display_name}</span>
                {r.streak_days > 0 && (
                  <span className="text-muted-foreground inline-flex shrink-0 items-center gap-1 text-xs tabular-nums">
                    <Flame className="size-3.5" aria-hidden />
                    {r.streak_days}
                  </span>
                )}
                <span className="w-20 shrink-0 text-right tabular-nums">{r.total_xp} XP</span>
              </div>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function TheSo({
  nhan,
  so,
  Icon,
  phu,
}: {
  nhan: string;
  so: number | string;
  Icon: React.ComponentType<{ className?: string }>;
  phu?: string;
}) {
  return (
    <Card className="h-full gap-0 py-4">
      <CardHeader className="px-3.5 pb-0">
        <CardTitle className="text-muted-foreground flex items-center gap-1.5 text-xs font-normal">
          <Icon className="size-3.5 shrink-0" />
          <span className="text-pretty">{nhan}</span>
        </CardTitle>
      </CardHeader>
      <CardContent className="px-3.5 pt-1">
        {/* `tabular-nums` để bốn thẻ số thẳng cột với nhau; thiếu nó thì bề rộng chữ số khác
            nhau và cả hàng trông xô lệch. */}
        {/* Không tô màu ngữ nghĩa cho các con số này: hổ phách trong hệ thống nghĩa là "cần
            chú ý", nên một chuỗi ngày học đang tốt mà tô hổ phách thì đọc như một cảnh báo. */}
        <span className="text-2xl font-semibold tabular-nums">{so}</span>
        {phu && <p className="text-muted-foreground text-xs">{phu}</p>}
      </CardContent>
    </Card>
  );
}
