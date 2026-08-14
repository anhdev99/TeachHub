"use client";

import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { useMutation, useQueryClient } from "@tanstack/react-query";
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
      className={`border-border bg-background flex flex-col gap-2 rounded-md border p-3 ${
        isDragging ? "opacity-60 shadow-lg" : ""
      }`}
    >
      <div className="flex flex-wrap items-center gap-2">
        {suaDuoc && (
          <button
            type="button"
            className="text-muted-foreground cursor-grab px-1 active:cursor-grabbing"
            aria-label={`Kéo để đổi thứ tự bài ${bai.title}`}
            {...attributes}
            {...listeners}
          >
            ⠿
          </button>
        )}

        <span className="text-muted-foreground text-xs tabular-nums">{bai.order_index + 1}.</span>
        <span className="font-medium">{bai.title}</span>

        {bai.file_url && <Badge variant="outline">Có tệp</Badge>}

        <Button
          size="sm"
          variant="ghost"
          className="ml-auto"
          onClick={() => setMo((v) => !v)}
          aria-expanded={mo}
        >
          {bai.components.length} phần {mo ? "▲" : "▼"}
        </Button>
      </div>

      {mo && (
        <div className="flex flex-col gap-2 pl-6">
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
                  className="border-border flex items-center gap-2 rounded border px-2 py-1 text-sm"
                >
                  <span className="text-muted-foreground text-xs tabular-nums">
                    {c.order_index + 1}
                  </span>
                  <span>{NHAN_LOAI_COMPONENT[c.type]}</span>
                  {suaDuoc && (
                    <Button
                      size="sm"
                      variant="ghost"
                      className="text-destructive ml-auto h-6"
                      onClick={() => xoaComponent.mutate(c.id)}
                    >
                      Xoá
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
                + Thêm phần
              </Button>
            </div>
          )}

          {loi && <p className="text-destructive text-sm">{loi}</p>}
        </div>
      )}
    </li>
  );
}
