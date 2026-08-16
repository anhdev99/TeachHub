"use client";

import {
  DndContext,
  KeyboardSensor,
  MouseSensor,
  TouchSensor,
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
import { ChevronRight, Loader2, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { Input } from "@/components/ui/input";
import { apiFetch } from "@/lib/api/fetcher";
import type { ChapterDto, LessonDto } from "@/lib/api/types";
import { cn } from "@/lib/utils";
import { TheBai } from "./the-bai";

/**
 * Một chương trong trình soạn thảo.
 *
 * Khuôn lấy từ curriculum builder của Udemy: chương là một khối có **đầu khối tô nền**, bài
 * học nằm LỒNG bên trong, và ô thêm bài là dòng cuối cùng của chính khối đó. Bản trước để
 * chương và bài thành hai thẻ trắng cùng cỡ, cách nhau bằng khoảng trắng — nhìn không ra cái
 * nào chứa cái nào, mà quan hệ chứa nhau chính là thứ duy nhất màn hình này cần thể hiện.
 */
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
  const [mo, setMo] = useState(true);

  // MouseSensor + TouchSensor, KHÔNG phải PointerSensor: pointer event gộp chuột với cảm ứng
  // nên mọi cú vuốt để cuộn danh sách bài học trên điện thoại đều thành kéo bài đi chỗ khác.
  const sensors = useSensors(
    useSensor(MouseSensor, { activationConstraint: { distance: 8 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 200, tolerance: 8 } }),
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
   * Kéo-thả phải phản hồi TỨC THÌ. Chờ round-trip mới thấy thứ tự đổi sẽ khiến việc soạn một
   * chương 20 bài khó chịu tới mức người dùng bỏ luôn tính năng. onMutate cập nhật cache ngay,
   * onError hoàn tác — server vẫn là nguồn chân lý, chỉ phản hồi hình ảnh là tức thì.
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
  const soPhan = bai.reduce((t, l) => t + l.components.length, 0);

  return (
    <section className="border-border overflow-hidden rounded-lg border">
      {/* ── Đầu khối ────────────────────────────────────────────────────────────────────── */}
      <header className="group bg-muted/60 flex items-center gap-2 px-3 py-2">
        <button
          type="button"
          onClick={() => setMo((v) => !v)}
          aria-expanded={mo}
          aria-label={mo ? "Thu gọn chương" : "Mở chương"}
          className="text-muted-foreground hover:text-foreground grid size-6 shrink-0 place-items-center rounded transition-colors"
        >
          <ChevronRight
            className={cn("size-4 transition-transform", mo && "rotate-90")}
            aria-hidden
          />
        </button>

        <div className="flex min-w-0 flex-1 flex-col">
          {/* KHÔNG thêm tiền tố "Chương N:" vào đây. Giảng viên gõ tên chương theo thói quen
              là đã có sẵn số ("Chương 1 — React Native cơ bản"), nên thêm lần nữa ra
              "Chương 1: Chương 1 — …". Thứ tự đã hiện qua vị trí trong danh sách. */}
          <h2 className="truncate text-sm font-semibold">{chuong.title}</h2>
          {chuong.objective && (
            <p className="text-muted-foreground truncate text-xs">{chuong.objective}</p>
          )}
        </div>

        {/* Đếm ở đây chứ không phải trong một thẻ riêng: đây là hai con số giảng viên liếc
            liên tục khi soạn ("chương này đủ bài chưa"), không đáng phải đi tìm. */}
        <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
          {bai.length} bài · {soPhan} phần
        </span>

        {suaDuoc && (
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <button
                type="button"
                aria-label={`Xoá chương ${chuong.title}`}
                className="text-muted-foreground hover:text-danger hover:bg-danger-soft grid size-7 shrink-0 place-items-center rounded opacity-0 transition-colors group-hover:opacity-100 focus-visible:opacity-100"
              >
                <Trash2 className="size-4" aria-hidden />
              </button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Xoá chương “{chuong.title}”?</AlertDialogTitle>
                <AlertDialogDescription>
                  {/* Nói ĐÚNG cái sắp mất, kèm số lượng. "Bạn có chắc không?" chỉ tạo thêm một
                      cú bấm rồi ai cũng bấm qua theo phản xạ. */}
                  {bai.length > 0
                    ? `Chương này đang có ${bai.length} bài học. Xoá chương sẽ xoá luôn toàn bộ số bài học đó.`
                    : "Chương này chưa có bài học nào."}{" "}
                  Thao tác này không hoàn tác được.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Huỷ</AlertDialogCancel>
                <AlertDialogAction
                  onClick={() => xoaChuong.mutate()}
                  className="bg-danger text-primary-foreground hover:bg-danger/90"
                >
                  Xoá chương
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        )}
      </header>

      {mo && (
        <>
          {baiQuery.isPending ? (
            <p className="text-muted-foreground flex items-center gap-2 px-3 py-2.5 text-sm">
              <Loader2 className="size-4 animate-spin" aria-hidden />
              Đang tải bài học…
            </p>
          ) : bai.length === 0 ? (
            <p className="text-muted-foreground border-border border-t px-3 py-2.5 text-sm">
              Chương này chưa có bài học nào.
            </p>
          ) : (
            <DndContext
              id={`keo-bai-${chuong.id}`}
              sensors={sensors}
              collisionDetection={closestCenter}
              onDragEnd={ketThucKeo}
            >
              <SortableContext items={bai.map((l) => l.id)} strategy={verticalListSortingStrategy}>
                <ul className="border-border border-t">
                  {bai.map((l) => (
                    <TheBai key={l.id} bai={l} suaDuoc={suaDuoc} chapterId={chuong.id} />
                  ))}
                </ul>
              </SortableContext>
            </DndContext>
          )}

          {/* Ô thêm bài là DÒNG CUỐI của chính khối chương, không phải một thanh trôi nổi bên
              dưới: chỗ đặt nó là thứ nói cho người dùng biết bài sẽ vào chương nào. */}
          <div className="border-border bg-muted/30 flex items-center gap-2 border-t px-2 py-1.5">
            <Plus className="text-muted-foreground ml-1 size-3.5 shrink-0" aria-hidden />
            <Input
              className="h-8 border-transparent bg-transparent text-sm shadow-none focus-visible:bg-card focus-visible:border-input"
              value={tenBaiMoi}
              disabled={!suaDuoc}
              aria-label={`Tên bài học mới trong ${chuong.title}`}
              onChange={(e) => setTenBaiMoi(e.target.value)}
              placeholder={
                suaDuoc ? "Thêm bài học — gõ tên rồi Enter" : "Đã khoá — phiên bản chỉ xem"
              }
              onKeyDown={(e) => {
                if (e.key === "Enter" && tenBaiMoi.trim()) themBai.mutate(tenBaiMoi.trim());
              }}
            />
          </div>
        </>
      )}
    </section>
  );
}
