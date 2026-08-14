"use client";

import {
  DndContext,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
  type DragEndEvent,
} from "@dnd-kit/core";
import {
  SortableContext,
  arrayMove,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { apiFetch } from "@/lib/api/fetcher";
import type { ChapterDto, LessonDto } from "@/lib/api/types";
import { TheBai } from "./the-bai";

export function KhoiChuong({
  chuong,
  suaDuoc,
  versionId,
}: {
  chuong: ChapterDto;
  suaDuoc: boolean;
  versionId: string;
}) {
  const qc = useQueryClient();
  const khoa = ["lessons", chuong.id] as const;
  const [tenBaiMoi, setTenBaiMoi] = useState("");

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const baiQuery = useQuery({
    queryKey: khoa,
    queryFn: () => apiFetch<LessonDto[]>(`/api/chapters/${chuong.id}/lessons`),
  });

  const themBai = useMutation({
    mutationFn: (title: string) =>
      apiFetch<LessonDto>(`/api/chapters/${chuong.id}/lessons`, {
        method: "POST",
        body: JSON.stringify({ title }),
      }),
    onSuccess: () => {
      setTenBaiMoi("");
      qc.invalidateQueries({ queryKey: khoa });
    },
  });

  const xoaChuong = useMutation({
    mutationFn: () => apiFetch(`/api/chapters/${chuong.id}`, { method: "DELETE" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["chapters", versionId] }),
  });

  /**
   * Kéo-thả phải phản hồi TỨC THÌ. Chờ round-trip mới thấy thứ tự đổi sẽ khiến việc soạn
   * một chương 20 bài khó chịu tới mức người dùng bỏ luôn tính năng. onMutate cập nhật
   * cache ngay, onError hoàn tác — server vẫn là nguồn chân lý, chỉ phản hồi hình ảnh là tức thì.
   */
  const doiThuTu = useMutation({
    mutationFn: ({ lessonId, toIndex }: { lessonId: string; toIndex: number }) =>
      apiFetch(`/api/lessons/${lessonId}/reorder`, {
        method: "PUT",
        body: JSON.stringify({ to_index: toIndex }),
      }),
    onMutate: async ({ lessonId, toIndex }) => {
      await qc.cancelQueries({ queryKey: khoa });
      const truoc = qc.getQueryData<LessonDto[]>(khoa);
      if (truoc) {
        const tu = truoc.findIndex((l) => l.id === lessonId);
        qc.setQueryData<LessonDto[]>(khoa, arrayMove(truoc, tu, toIndex));
      }
      return { truoc };
    },
    onError: (_e, _v, ctx) => {
      if (ctx?.truoc) qc.setQueryData(khoa, ctx.truoc);
    },
    onSettled: () => qc.invalidateQueries({ queryKey: khoa }),
  });

  function ketThucKeo(e: DragEndEvent) {
    const { active, over } = e;
    if (!over || active.id === over.id || !baiQuery.data) return;

    const toIndex = baiQuery.data.findIndex((l) => l.id === over.id);
    if (toIndex >= 0) doiThuTu.mutate({ lessonId: String(active.id), toIndex });
  }

  const bai = baiQuery.data ?? [];

  return (
    <section className="border-border flex flex-col gap-3 rounded-lg border p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="font-medium">{chuong.title}</h2>
        {suaDuoc && (
          <Button
            size="sm"
            variant="ghost"
            className="text-destructive"
            onClick={() => xoaChuong.mutate()}
            disabled={xoaChuong.isPending}
          >
            Xoá chương
          </Button>
        )}
      </div>

      {chuong.objective && (
        <p className="text-muted-foreground text-sm">{chuong.objective}</p>
      )}

      {baiQuery.isPending ? (
        <p className="text-muted-foreground text-sm">Đang tải bài học…</p>
      ) : bai.length === 0 ? (
        <p className="text-muted-foreground text-sm">Chương này chưa có bài học nào.</p>
      ) : (
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={ketThucKeo}>
          <SortableContext items={bai.map((l) => l.id)} strategy={verticalListSortingStrategy}>
            <ul className="flex flex-col gap-2">
              {bai.map((l) => (
                <TheBai key={l.id} bai={l} suaDuoc={suaDuoc} chapterId={chuong.id} />
              ))}
            </ul>
          </SortableContext>
        </DndContext>
      )}

      {suaDuoc && (
        <div className="flex gap-2">
          <Input
            value={tenBaiMoi}
            onChange={(e) => setTenBaiMoi(e.target.value)}
            placeholder="Tên bài học mới"
            onKeyDown={(e) => {
              if (e.key === "Enter" && tenBaiMoi.trim()) themBai.mutate(tenBaiMoi.trim());
            }}
          />
          <Button
            size="sm"
            variant="outline"
            onClick={() => themBai.mutate(tenBaiMoi.trim())}
            disabled={!tenBaiMoi.trim() || themBai.isPending}
          >
            Thêm bài
          </Button>
        </div>
      )}
    </section>
  );
}
