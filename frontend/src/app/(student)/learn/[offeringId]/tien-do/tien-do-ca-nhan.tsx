"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { apiFetch } from "@/lib/api/fetcher";
import type { components } from "@/lib/api/schema";

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
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <TheSo nhan="Tổng XP" so={me.total_xp} />
        <TheSo nhan="Chuỗi ngày" so={me.current_streak_days} phu={`dài nhất ${me.longest_streak_days}`} />
        <TheSo nhan="Huy hiệu" so={me.badges.length} />
        <TheSo
          nhan="Xếp hạng"
          so={me.rank ?? 0}
          phu={me.total_participants > 0 ? `trên ${me.total_participants}` : undefined}
        />
      </div>

      {onTap && onTap.total_due > 0 && (
        <Card className="border-primary">
          <CardHeader>
            <CardTitle className="text-base">Ôn tập hôm nay ({onTap.total_due})</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <p className="text-muted-foreground text-sm">
              Đây là những câu bạn từng trả lời sai. Trả lời đúng thì lần ôn sau giãn ra; sai
              thì quay lại sau 3 ngày.
            </p>

            {mucDangLam ? (
              <div className="border-border flex flex-col gap-3 rounded-lg border p-3">
                <p className="text-sm font-medium">{mucDangLam.question?.text}</p>
                <p className="text-muted-foreground text-sm">
                  Tự chấm: bạn có nhớ câu trả lời không?
                </p>
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    disabled={traLoi.isPending}
                    onClick={() => traLoi.mutate({ id: mucDangLam.id, dung: true })}
                  >
                    Nhớ
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={traLoi.isPending}
                    onClick={() => traLoi.mutate({ id: mucDangLam.id, dung: false })}
                  >
                    Chưa nhớ
                  </Button>
                  <Button size="sm" variant="ghost" onClick={() => setMucDangLam(null)}>
                    Để sau
                  </Button>
                </div>
              </div>
            ) : (
              <ul className="flex flex-col gap-2">
                {onTap.items.map((m) => (
                  <li
                    key={m.id}
                    className="border-border flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3"
                  >
                    <span className="flex-1 text-sm">{m.question?.text ?? "(câu đã bị xóa)"}</span>
                    <Badge variant="outline">{m.stage}</Badge>
                    <Button size="sm" onClick={() => setMucDangLam(m)}>Ôn</Button>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      )}

      {me.badges.length > 0 && (
        <Card>
          <CardHeader><CardTitle className="text-base">Huy hiệu đã đạt</CardTitle></CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {me.badges.map((b) => (
              <Badge key={b.badge_id} className="text-sm">{b.icon} {b.name}</Badge>
            ))}
          </CardContent>
        </Card>
      )}

      {bangXepHang && bangXepHang.rows.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Bảng xếp hạng lớp</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-1">
            {/* Tên người khác do MÁY CHỦ ẩn, không phải giao diện ẩn: payload không chứa tên
                thật, nên mở tab Network cũng không thấy gì. */}
            {bangXepHang.rows.map((r) => (
              <div
                key={r.rank}
                className={`flex items-center justify-between gap-2 rounded px-2 py-1.5 text-sm ${
                  r.is_me ? "bg-muted font-medium" : ""
                }`}
              >
                <span className="text-muted-foreground w-8 tabular-nums">#{r.rank}</span>
                <span className="flex-1">{r.display_name}</span>
                {r.streak_days > 0 && (
                  <span className="text-muted-foreground text-xs">{r.streak_days} ngày</span>
                )}
                <span className="tabular-nums">{r.total_xp} XP</span>
              </div>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function TheSo({ nhan, so, phu }: { nhan: string; so: number; phu?: string }) {
  return (
    <Card>
      <CardHeader className="pb-1">
        <CardTitle className="text-muted-foreground text-xs font-normal">{nhan}</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="text-2xl font-semibold tabular-nums">{so}</div>
        {phu && <div className="text-muted-foreground text-xs">{phu}</div>}
      </CardContent>
    </Card>
  );
}
