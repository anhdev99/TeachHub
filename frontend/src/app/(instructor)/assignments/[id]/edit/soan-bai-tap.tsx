"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { apiFetch } from "@/lib/api/fetcher";
import type { AssignmentDto, SubmissionSlotDto } from "@/lib/api/types";

const DINH_DANG_GOI_Y = [".zip", ".pdf", ".docx", ".jpg", ".jpeg", ".png", ".mp4"];

export function SoanBaiTap({ banDau }: { banDau: AssignmentDto }) {
  const qc = useQueryClient();
  const khoa = ["assignment", banDau.id] as const;

  const { data: bai } = useQuery({
    queryKey: khoa,
    queryFn: () => apiFetch<AssignmentDto>(`/api/assignments/${banDau.id}`),
    initialData: banDau,
  });

  const lamMoi = () => qc.invalidateQueries({ queryKey: khoa });

  const dungMau = useMutation({
    mutationFn: () =>
      apiFetch<SubmissionSlotDto[]>(`/api/assignments/${banDau.id}/slots/suggested`, {
        method: "POST",
      }),
    onSuccess: () => {
      toast.success("Đã thêm các phần nộp theo mẫu gợi ý.");
      lamMoi();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const xoaSlot = useMutation({
    mutationFn: (slotId: string) =>
      apiFetch(`/api/submission-slots/${slotId}`, { method: "DELETE" }),
    onSuccess: () => {
      toast.success("Đã xóa phần nộp.");
      lamMoi();
    },
    // Backend trả 409 kèm số tệp đã nộp. Hiện nguyên câu đó chứ không nuốt thành
    // "đã có lỗi xảy ra" — nó nói đúng thứ giảng viên cần biết để quyết định.
    onError: (e: Error) => toast.error(e.message),
  });

  const tongDiemSlot = bai.slots.filter((s) => s.is_required).length;

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3">
          <CardTitle className="text-base">
            Các phần nộp ({bai.slots.length})
          </CardTitle>
          <div className="flex flex-wrap gap-2">
            <Button
              size="sm"
              variant="outline"
              disabled={dungMau.isPending}
              onClick={() => dungMau.mutate()}
            >
              {dungMau.isPending ? "Đang thêm…" : "Dùng mẫu gợi ý"}
            </Button>
            <ThemSlot assignmentId={bai.id} onXong={lamMoi} />
          </div>
        </CardHeader>

        <CardContent className="flex flex-col gap-3">
          {bai.slots.length === 0 ? (
            <p className="text-muted-foreground text-sm">
              Chưa có phần nộp nào. Sinh viên sẽ không nộp được bài cho tới khi bạn thêm ít nhất một phần.
            </p>
          ) : (
            <>
              <p className="text-muted-foreground text-sm">
                {tongDiemSlot === 0
                  ? "Không có phần nào bắt buộc — sinh viên sẽ không nộp được vì không đủ điều kiện."
                  : `${tongDiemSlot} phần bắt buộc phải có tệp thì mới nộp chính thức được.`}
              </p>

              <ul className="flex flex-col gap-2">
                {bai.slots.map((s) => (
                  <li
                    key={s.id}
                    className="border-border flex flex-col gap-2 rounded-lg border p-3"
                  >
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-medium">{s.label}</span>
                      <code className="text-muted-foreground text-xs">{s.slot_key}</code>
                      {s.is_required && <Badge variant="secondary">Bắt buộc</Badge>}
                      {s.allow_link_instead_of_file && <Badge variant="outline">Cho nộp link</Badge>}

                      <Button
                        size="sm"
                        variant="ghost"
                        className="ml-auto"
                        disabled={xoaSlot.isPending}
                        onClick={() => xoaSlot.mutate(s.id)}
                      >
                        Xóa
                      </Button>
                    </div>

                    <p className="text-muted-foreground text-sm">
                      {s.accepted_file_types.join(", ")} · tối đa {s.max_files} tệp ·{" "}
                      {s.max_file_size_mb} MB mỗi tệp
                    </p>

                    {(s.required_files.length > 0 || s.required_folders.length > 0) && (
                      <p className="text-muted-foreground text-sm">
                        Cấu trúc bắt buộc trong ZIP:{" "}
                        {[...s.required_files, ...s.required_folders.map((f) => `${f}/`)].join(", ")}
                      </p>
                    )}
                  </li>
                ))}
              </ul>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function ThemSlot({ assignmentId, onXong }: { assignmentId: string; onXong: () => void }) {
  const [mo, setMo] = useState(false);
  const [key, setKey] = useState("");
  const [label, setLabel] = useState("");
  const [dinhDang, setDinhDang] = useState<string[]>([".zip"]);
  const [batBuoc, setBatBuoc] = useState(true);
  const [soFile, setSoFile] = useState(1);
  const [dungLuong, setDungLuong] = useState(50);

  const them = useMutation({
    mutationFn: () =>
      apiFetch<SubmissionSlotDto>(`/api/assignments/${assignmentId}/slots`, {
        method: "POST",
        body: JSON.stringify({
          slot_key: key,
          label,
          accepted_file_types: dinhDang,
          allow_link_instead_of_file: false,
          max_files: soFile,
          max_file_size_mb: dungLuong,
          is_required: batBuoc,
          required_files: [],
          required_folders: [],
        }),
      }),
    onSuccess: () => {
      toast.success("Đã thêm phần nộp.");
      setMo(false);
      setKey("");
      setLabel("");
      onXong();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  if (!mo) {
    return (
      <Button size="sm" onClick={() => setMo(true)}>
        + Thêm phần nộp
      </Button>
    );
  }

  return (
    <div className="border-border flex w-full flex-col gap-3 rounded-lg border p-3">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div className="flex flex-col gap-1">
          <Label htmlFor="slot_key">Khóa (chữ thường, không dấu)</Label>
          <Input
            id="slot_key"
            value={key}
            placeholder="report"
            onChange={(e) => setKey(e.target.value)}
          />
        </div>
        <div className="flex flex-col gap-1">
          <Label htmlFor="slot_label">Nhãn hiển thị</Label>
          <Input
            id="slot_label"
            value={label}
            placeholder="Báo cáo (PDF)"
            onChange={(e) => setLabel(e.target.value)}
          />
        </div>
      </div>

      <div className="flex flex-col gap-1">
        <Label>Định dạng nhận</Label>
        <div className="flex flex-wrap gap-2">
          {DINH_DANG_GOI_Y.map((d) => (
            <label key={d} className="flex items-center gap-1.5 text-sm">
              <Checkbox
                checked={dinhDang.includes(d)}
                onCheckedChange={(c) =>
                  setDinhDang((cu) => (c === true ? [...cu, d] : cu.filter((x) => x !== d)))
                }
              />
              <code>{d}</code>
            </label>
          ))}
        </div>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div className="flex flex-col gap-1">
          <Label htmlFor="max_files">Số tệp tối đa</Label>
          <Input id="max_files" type="number" min={1} max={50} value={soFile}
                 onChange={(e) => setSoFile(Number(e.target.value))} />
        </div>
        <div className="flex flex-col gap-1">
          <Label htmlFor="max_size">Dung lượng mỗi tệp (MB)</Label>
          <Input id="max_size" type="number" min={1} max={500} value={dungLuong}
                 onChange={(e) => setDungLuong(Number(e.target.value))} />
        </div>
      </div>

      <label className="flex items-center gap-2 text-sm">
        <Checkbox checked={batBuoc} onCheckedChange={(c) => setBatBuoc(c === true)} />
        Bắt buộc phải nộp phần này
      </label>

      <div className="flex gap-2">
        <Button size="sm" disabled={them.isPending} onClick={() => them.mutate()}>
          {them.isPending ? "Đang lưu…" : "Thêm"}
        </Button>
        <Button size="sm" variant="ghost" onClick={() => setMo(false)}>
          Hủy
        </Button>
      </div>
    </div>
  );
}
