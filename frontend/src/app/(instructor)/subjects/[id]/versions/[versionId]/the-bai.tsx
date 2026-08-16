"use client";

import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ChevronDown, GripVertical, Paperclip, Plus, X } from "lucide-react";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { apiFetch } from "@/lib/api/fetcher";
import {
  LOAI_CAN_REF,
  NHAN_LOAI_COMPONENT,
  type LessonComponentType,
  type LessonDto,
} from "@/lib/api/types";

/** Tám loại component của SRS; loại cần ref_id bị loại khỏi menu nhanh vì phải chọn bản ghi đích. */
const LOAI_THEM_NHANH = (Object.keys(NHAN_LOAI_COMPONENT) as LessonComponentType[]).filter(
  (t) => !LOAI_CAN_REF.includes(t),
);

export function TheBai({
  bai,
  suaDuoc,
  chapterId,
}: {
  bai: LessonDto;
  suaDuoc: boolean;
  chapterId: string;
}) {
  const qc = useQueryClient();
  const khoa = ["lessons", chapterId] as const;
  const [mo, setMo] = useState(false);
  const [loaiChon, setLoaiChon] = useState<LessonComponentType>("reflection");
  const [loi, setLoi] = useState<string | null>(null);

  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: bai.id,
    disabled: !suaDuoc,
  });

  const themComponent = useMutation({
    mutationFn: (type: LessonComponentType) =>
      apiFetch(`/api/lessons/${bai.id}/components`, {
        method: "POST",
        body: JSON.stringify({ type }),
      }),
    onSuccess: () => {
      setLoi(null);
      qc.invalidateQueries({ queryKey: khoa });
    },
    onError: (e: Error) => setLoi(e.message),
  });

  const xoaComponent = useMutation({
    mutationFn: (id: string) => apiFetch(`/api/lesson-components/${id}`, { method: "DELETE" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: khoa }),
    onError: (e: Error) => setLoi(e.message),
  });

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={`border-border bg-card flex flex-col gap-2 rounded-lg border transition-shadow ${
        isDragging ? "shadow-lg opacity-70" : ""
      }`}
    >
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 px-2 py-1.5">
        {suaDuoc && (
          <button
            type="button"
            className="text-muted-foreground hover:text-foreground grid size-9 shrink-0 cursor-grab place-items-center rounded-md transition-colors active:cursor-grabbing"
            aria-label={`Kéo để đổi thứ tự bài ${bai.title}`}
            {...attributes}
            {...listeners}
          >
            <GripVertical className="size-4" aria-hidden />
          </button>
        )}

        {/* Số thứ tự dùng tabular-nums để cột số không răng cưa khi chương có trên 10 bài. */}
        <span className="text-muted-foreground w-5 shrink-0 text-right text-xs tabular-nums">
          {bai.order_index + 1}
        </span>
        <span className="min-w-0 flex-1 truncate font-medium">{bai.title}</span>

        {bai.file_url && (
          <Badge variant="outline" className="shrink-0 gap-1 font-normal">
            <Paperclip className="size-3" aria-hidden />
            Có tệp
          </Badge>
        )}

        <Button
          size="sm"
          variant="ghost"
          className="text-muted-foreground shrink-0"
          onClick={() => setMo((v) => !v)}
          aria-expanded={mo}
        >
          {bai.components.length} phần
          <ChevronDown
            className={`size-4 transition-transform ${mo ? "rotate-180" : ""}`}
            aria-hidden
          />
        </Button>
      </div>

      {mo && (
        <div className="border-border flex flex-col gap-2 border-t px-3 py-2.5">
          {bai.components.length === 0 ? (
            // Một bài không có component nào là HỢP LỆ (SRS mục 5.1) — nói rõ để người
            // dùng không tưởng là thiếu dữ liệu.
            <p className="text-muted-foreground text-sm">
              Bài này chưa gắn phần nào — hoàn toàn hợp lệ.
            </p>
          ) : (
            <ul className="flex flex-col gap-1">
              {bai.components.map((c) => (
                <li
                  key={c.id}
                  className="border-border flex items-center gap-2 rounded-md border px-2 py-1 text-sm"
                >
                  <span className="text-muted-foreground w-4 shrink-0 text-right text-xs tabular-nums">
                    {c.order_index + 1}
                  </span>
                  <span className="min-w-0 flex-1 truncate">{NHAN_LOAI_COMPONENT[c.type]}</span>
                  {suaDuoc && (
                    <Button
                      size="sm"
                      variant="ghost"
                      className="text-muted-foreground hover:text-danger hover:bg-danger-soft size-7 shrink-0 p-0"
                      aria-label={`Xoá phần ${NHAN_LOAI_COMPONENT[c.type]}`}
                      onClick={() => xoaComponent.mutate(c.id)}
                    >
                      <X className="size-4" aria-hidden />
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          )}

          {suaDuoc && (
            <div className="flex flex-wrap items-center gap-2">
              <Select
                value={loaiChon}
                onValueChange={(v) => setLoaiChon(v as LessonComponentType)}
              >
                <SelectTrigger className="w-48" size="sm">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {LOAI_THEM_NHANH.map((t) => (
                    <SelectItem key={t} value={t}>
                      {NHAN_LOAI_COMPONENT[t]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              <Button
                size="sm"
                variant="outline"
                onClick={() => themComponent.mutate(loaiChon)}
                disabled={themComponent.isPending}
              >
                <Plus className="size-4" aria-hidden />
                Thêm phần
              </Button>
            </div>
          )}

          {loi && (
            <p className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3 py-2 text-sm">
              {loi}
            </p>
          )}
        </div>
      )}
    </li>
  );
}
