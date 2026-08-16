"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { ArrowRight, Plus } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Popover, PopoverContent, PopoverTitle, PopoverTrigger,
} from "@/components/ui/popover";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { formatScore as dinhDangDiem } from "@/lib/format";
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
  const [formatScore, setDiem] = useState("0.5");
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
          points: Number(formatScore),
          // Lý do gửi lên là VĂN BẢN, không phải chỉ template_id: bản ghi phải giữ được câu
          // chữ tại thời điểm cấp, kể cả khi sau này nhãn template bị sửa hoặc bị ẩn.
          reason: lyDo,
          template_id: templateId,
        }),
      }),
    onSuccess: () => {
      toast.success(`Đã cộng ${formatScore} cho ${tenSinhVien}.`);
      setMo(false);
      setLyDo("");
      setTemplateId(null);
      onXong();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const cotDich = cot.find((c) => c.id === columnId) ?? null;
  const soDiem = Number(formatScore);
  const hopLe = Number.isFinite(soDiem) && soDiem > 0;

  /**
   * Tác động THẬT lên điểm tổng (ADR-008, yêu cầu bắt buộc của màn hình này).
   *
   * Cộng vào một cột khác hẳn cộng thẳng vào tổng: bonus vào cột còn phải đi qua trọng số,
   * nên +0,5 vào cột "Thường kỳ" trọng số 20% chỉ làm điểm tổng tăng 0,1. Không nói ra thì
   * giảng viên tưởng vừa cộng nửa điểm, cộng tiếp cho "đủ", và sai lệch tích luỹ trên cả lớp.
   *
   * Đây là XEM TRƯỚC tác động của đúng một lần cấp, không phải tính lại điểm: điểm luôn do
   * GradebookCalculator phía Domain tính, và con số cuối cùng còn chịu kẹp [0,10] lẫn điều
   * kiện điểm sàn ở bước sau.
   */
  const tacDong =
    !hopLe
      ? null
      : target === "final_total"
        ? soDiem
        : cotDich
          ? (soDiem * Number(cotDich.weight_percent)) / 100
          : null;

  return (
    <Popover open={mo} onOpenChange={setMo}>
      <PopoverTrigger asChild>
        <Button
          size="sm"
          variant="ghost"
          className="text-muted-foreground hover:text-foreground whitespace-nowrap"
          aria-label={`Cộng điểm cho ${tenSinhVien}`}
        >
          <Plus className="size-4" aria-hidden />
          Điểm cộng
        </Button>
      </PopoverTrigger>

      <PopoverContent align="end" className="w-80 p-3">
        <PopoverTitle className="text-sm">Cộng điểm cho {tenSinhVien}</PopoverTitle>

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
              className="w-20 tabular-nums" value={formatScore}
              onChange={(e) => setDiem(e.target.value)}
            />
          </div>

          <div className="flex flex-1 flex-col gap-1">
            <Label>Cộng vào</Label>
            <Select value={target} onValueChange={(v) => setTarget(v as BonusTarget)}>
              <SelectTrigger className="w-full"><SelectValue /></SelectTrigger>
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
              <SelectTrigger className="w-full"><SelectValue placeholder="Chọn cột" /></SelectTrigger>
              <SelectContent>
                {cot.map((c) => (
                  <SelectItem key={c.id} value={c.id}>
                    {c.name} · {c.weight_percent}%
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}

        {/* Dòng tác động thật — cập nhật lại mỗi khi đổi số điểm hoặc đổi đích. */}
        <div className="bg-accent text-accent-foreground flex flex-col gap-0.5 rounded-lg px-3 py-2">
          <span className="flex items-center gap-1.5 text-sm font-medium tabular-nums">
            <ArrowRight className="size-4 shrink-0" aria-hidden />
            Điểm tổng: {tacDong === null ? "—" : `+${dinhDangDiem(tacDong)}`}
          </span>
          {target === "grade_column" && cotDich && hopLe && (
            <span className="text-muted-foreground text-xs text-pretty">
              +{dinhDangDiem(soDiem)} vào “{cotDich.name}” (trọng số {cotDich.weight_percent}%).
              Điểm sàn và mức kẹp [0–10] vẫn áp sau cùng.
            </span>
          )}
          {target === "final_total" && hopLe && (
            <span className="text-muted-foreground text-xs text-pretty">
              Cộng thẳng vào tổng, không đi qua trọng số cột nào.
            </span>
          )}
        </div>

        <div className="flex flex-col gap-1">
          <Label htmlFor="bonus_reason">
            Lý do <span className="text-muted-foreground font-normal">(bắt buộc)</span>
          </Label>
          <Input
            id="bonus_reason" value={lyDo} placeholder="Thuyết trình tự tin"
            onChange={(e) => { setLyDo(e.target.value); setTemplateId(null); }}
          />
          {!lyDo && (
            // Lý do là thứ trả lời cho khiếu nại điểm sáu tháng sau, nên nút lưu khoá cho
            // tới khi có chữ — nói rõ vì sao khoá, đừng để nút xám không giải thích.
            <p className="text-muted-foreground text-xs">
              Nhập lý do thì mới cộng được — bản ghi này phải giải thích được về sau.
            </p>
          )}
        </div>

        <div className="flex gap-2">
          <Button
            size="sm"
            disabled={cap.isPending || !lyDo || !hopLe}
            onClick={() => cap.mutate()}
          >
            {cap.isPending ? "Đang lưu…" : "Cộng điểm"}
          </Button>
          <Button size="sm" variant="ghost" onClick={() => setMo(false)}>Hủy</Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}
