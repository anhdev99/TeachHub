"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { apiFetch } from "@/lib/api/fetcher";
import type { GroupDto } from "@/lib/api/types";

export function ChiaNhom({
  offeringId,
  banDau,
  siSo,
}: {
  offeringId: string;
  banDau: GroupDto[];
  siSo: number;
}) {
  const qc = useQueryClient();
  const khoa = ["groups", offeringId] as const;

  const { data: nhom } = useQuery({
    queryKey: khoa,
    queryFn: () => apiFetch<GroupDto[]>(`/api/offerings/${offeringId}/groups`),
    initialData: banDau,
  });

  const [soNguoi, setSoNguoi] = useState(4);
  const [seed, setSeed] = useState(2026);
  const [xemTruoc, setXemTruoc] = useState<GroupDto[] | null>(null);

  const chia = useMutation({
    mutationFn: (preview: boolean) =>
      apiFetch<GroupDto[]>(`/api/offerings/${offeringId}/groups/auto-generate`, {
        method: "POST",
        body: JSON.stringify({ group_size: soNguoi, seed, preview }),
      }),
    onSuccess: (ds, preview) => {
      if (preview) {
        setXemTruoc(ds);
        toast.success("Đây là kết quả xem trước — bấm “Xác nhận” để lưu.");
      } else {
        setXemTruoc(null);
        toast.success("Đã lưu danh sách nhóm.");
        qc.invalidateQueries({ queryKey: khoa });
      }
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const dangHien = xemTruoc ?? nhom;

  return (
    <div className="flex flex-col gap-5">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Chia ngẫu nhiên</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <p className="text-muted-foreground text-sm">
            Lớp có {siSo} sinh viên. Phần dư được rải sang các nhóm sẵn có — không để lại nhóm
            một người.
          </p>

          <div className="flex flex-wrap items-end gap-3">
            <div className="flex flex-col gap-1">
              <Label htmlFor="group_size">Số người mỗi nhóm</Label>
              <Input
                id="group_size" type="number" min={2} max={20} className="w-28"
                value={soNguoi} onChange={(e) => setSoNguoi(Number(e.target.value))}
              />
            </div>

            <div className="flex flex-col gap-1">
              <Label htmlFor="seed">Seed</Label>
              {/* Cùng seed cho cùng kết quả: giảng viên xem trước rồi mới xác nhận, và danh
                  sách không được đổi giữa hai lần đó. */}
              <Input
                id="seed" type="number" className="w-32"
                value={seed} onChange={(e) => setSeed(Number(e.target.value))}
              />
            </div>

            <Button variant="outline" disabled={chia.isPending} onClick={() => chia.mutate(true)}>
              Xem trước
            </Button>

            <Button disabled={chia.isPending} onClick={() => chia.mutate(false)}>
              {chia.isPending ? "Đang chia…" : "Xác nhận & lưu"}
            </Button>
          </div>

          {xemTruoc && (
            <p className="text-sm">
              Đang hiển thị <strong>bản xem trước</strong>, chưa lưu. Bấm “Xác nhận & lưu” để
              ghi lại đúng cách chia này.
            </p>
          )}
        </CardContent>
      </Card>

      {dangHien.length === 0 ? (
        <p className="text-muted-foreground text-sm">Chưa chia nhóm.</p>
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {dangHien.map((g, i) => (
            <Card key={g.id === "00000000-0000-0000-0000-000000000000" ? `tam-${i}` : g.id}>
              <CardHeader className="flex flex-row items-center justify-between gap-2">
                <CardTitle className="text-base">{g.name}</CardTitle>
                <Badge variant="secondary">{g.members.length} người</Badge>
              </CardHeader>
              <CardContent className="flex flex-col gap-1 text-sm">
                {g.members.map((m) => (
                  <div key={m.enrollment_id} className="flex justify-between gap-2">
                    <span>{m.full_name}</span>
                    <span className="text-muted-foreground font-mono text-xs">
                      {m.student_code}
                    </span>
                  </div>
                ))}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
