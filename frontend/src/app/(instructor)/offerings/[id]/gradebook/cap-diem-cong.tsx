"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { apiFetch } from "@/lib/api/fetcher";
import type { BonusReasonTemplateDto, BonusTarget, GradeColumnDto } from "@/lib/api/types";

/**
 * Component dùng chung cho CẢ sổ điểm lẫn màn hình chấm bài. Viết hai bản "tạm dùng" là cách
 * chắc chắn nhất để ô điểm cộng ở hai màn hình hiển thị lệch nhau, và người dùng mất niềm tin
 * vào con số.
 */
export function CapDiemCong({
  offeringId,
  enrollmentId,
  tenSinhVien,
  cot,
  macDinhTarget,
  macDinhColumnId,
  onXong,
}: {
  offeringId: string;
  enrollmentId: string;
  tenSinhVien: string;
  cot: GradeColumnDto[];
  macDinhTarget: BonusTarget;
  macDinhColumnId: string | null;
  onXong: () => void;
}) {
  const [mo, setMo] = useState(false);
  const [diem, setDiem] = useState("0.5");
  const [lyDo, setLyDo] = useState("");
  const [target, setTarget] = useState<BonusTarget>(macDinhTarget);
  const [columnId, setColumnId] = useState<string | null>(macDinhColumnId ?? cot[0]?.id ?? null);
  const [templateId, setTemplateId] = useState<string | null>(null);

  const { data: mauLyDo } = useQuery({
    queryKey: ["bonus-reasons"] as const,
    queryFn: () => apiFetch<BonusReasonTemplateDto[]>("/api/instructor/bonus-reasons"),
    enabled: mo,
  });

  const cap = useMutation({
    mutationFn: () =>
      apiFetch("/api/bonus-points", {
        method: "POST",
        body: JSON.stringify({
          enrollment_id: enrollmentId,
          target,
          target_column_id: target === "grade_column" ? columnId : null,
          points: Number(diem),
          // Lý do gửi lên là VĂN BẢN, không phải chỉ template_id: bản ghi phải giữ được câu
          // chữ tại thời điểm cấp, kể cả khi sau này nhãn template bị sửa hoặc bị ẩn.
          reason: lyDo,
          template_id: templateId,
        }),
      }),
    onSuccess: () => {
      toast.success(`Đã cộng ${diem} cho ${tenSinhVien}.`);
      setMo(false);
      setLyDo("");
      setTemplateId(null);
      onXong();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  if (!mo) {
    return (
      <Button size="sm" variant="ghost" onClick={() => setMo(true)}>
        + Điểm cộng
      </Button>
    );
  }

  return (
    <div className="border-border bg-background absolute z-20 flex w-80 flex-col gap-2 rounded-lg border p-3 shadow-lg">
      <p className="text-sm font-medium">Cộng điểm cho {tenSinhVien}</p>

      {(mauLyDo?.length ?? 0) > 0 && (
        <div className="flex flex-wrap gap-1">
          {mauLyDo!.map((m) => (
            <Button
              key={m.id} size="sm" variant="outline" className="h-7 text-xs"
              onClick={() => {
                setLyDo(m.label);
                setDiem(String(m.default_points));
                setTemplateId(m.id);
                if (m.default_target) setTarget(m.default_target);
                if (m.default_column_key) {
                  const c = cot.find((x) => x.key === m.default_column_key);
                  if (c) setColumnId(c.id);
                }
              }}
            >
              {m.label} +{m.default_points}
            </Button>
          ))}
        </div>
      )}

      <div className="flex gap-2">
        <div className="flex flex-col gap-1">
          <Label htmlFor="bonus_points">Điểm</Label>
          <Input
            id="bonus_points" type="number" step="0.25" min={0.25} max={10}
            className="w-20" value={diem} onChange={(e) => setDiem(e.target.value)}
          />
        </div>

        <div className="flex flex-1 flex-col gap-1">
          <Label>Cộng vào</Label>
          <Select value={target} onValueChange={(v) => setTarget(v as BonusTarget)}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="grade_column">Một cột điểm</SelectItem>
              <SelectItem value="final_total">Thẳng điểm tổng</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      {target === "grade_column" && (
        <div className="flex flex-col gap-1">
          <Label>Cột</Label>
          <Select value={columnId ?? ""} onValueChange={setColumnId}>
            <SelectTrigger><SelectValue placeholder="Chọn cột" /></SelectTrigger>
            <SelectContent>
              {cot.map((c) => (
                <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      <div className="flex flex-col gap-1">
        <Label htmlFor="bonus_reason">Lý do</Label>
        <Input
          id="bonus_reason" value={lyDo} placeholder="Thuyết trình tự tin"
          onChange={(e) => { setLyDo(e.target.value); setTemplateId(null); }}
        />
      </div>

      <div className="flex gap-2">
        <Button
          size="sm"
          disabled={cap.isPending || !lyDo || Number(diem) <= 0}
          onClick={() => cap.mutate()}
        >
          {cap.isPending ? "Đang lưu…" : "Cộng điểm"}
        </Button>
        <Button size="sm" variant="ghost" onClick={() => setMo(false)}>Hủy</Button>
      </div>
    </div>
  );
}
