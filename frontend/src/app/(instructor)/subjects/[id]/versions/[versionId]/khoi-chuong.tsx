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
import { Loader2, Plus, Trash2 } from "lucide-react";
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
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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

  // MouseSensor + TouchSensor, KHÔNG phải PointerSensor — giống `so-diem.tsx` và
  // `chia-nhom.tsx`. Pointer event gộp chuột với cảm ứng làm một, nên `PointerSensor` khởi
  // động kéo ngay khi ngón tay trượt được vài pixel: trên điện thoại, MỌI cú vuốt để cuộn
  // danh sách bài học đều bị hiểu thành kéo bài đi chỗ khác. Đây là chỗ duy nhất còn sót lại
  // dùng PointerSensor, và nó là danh sách dài nhất — tức là chỗ người ta cuộn nhiều nhất.
  const sensors = useSensors(
    useSensor(MouseSensor, { activationConstraint: { distance: 8 } }),
    // `delay` chứ không phải `distance` cho cảm ứng: phải giữ một nhịp mới tính là kéo, nên
    // vuốt nhanh vẫn là cuộn.
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
    <Card>
      <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-x-3 gap-y-2">
        <div className="flex min-w-0 flex-col gap-1">
          <CardTitle className="flex flex-wrap items-center gap-2 text-base">
            <span className="text-pretty">{chuong.title}</span>
            <Badge variant="secondary" className="shrink-0 font-normal">
              {bai.length} bài
            </Badge>
          </CardTitle>
          {chuong.objective && (
            <p className="text-muted-foreground text-sm text-pretty">{chuong.objective}</p>
          )}
        </div>

        {suaDuoc && (
          // Xoá chương kéo theo TOÀN BỘ bài học bên trong. Trước đây một cú bấm là mất sạch,
          // không hỏi lại, không hoàn tác được — và nút thì nằm ngay cạnh tiêu đề chương.
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button
                size="sm"
                variant="ghost"
                className="text-danger hover:text-danger hover:bg-danger-soft shrink-0"
                disabled={xoaChuong.isPending}
              >
                <Trash2 className="size-4" aria-hidden />
                {xoaChuong.isPending ? "Đang xoá…" : "Xoá chương"}
              </Button>
            </AlertDialogTrigger>

            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Xoá chương “{chuong.title}”?</AlertDialogTitle>
                <AlertDialogDescription>
                  {/* Nói ĐÚNG cái sắp mất, kèm số lượng. Một câu chung chung kiểu "bạn có
                      chắc không?" chỉ tạo thêm một cú bấm rồi ai cũng bấm qua theo phản xạ;
                      con số bài học mới là thứ khiến người ta dừng lại đọc. */}
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
      </CardHeader>

      <CardContent className="flex flex-col gap-3">
        {baiQuery.isPending ? (
          <p className="text-muted-foreground flex items-center gap-2 text-sm">
            <Loader2 className="size-4 animate-spin" aria-hidden />
            Đang tải bài học…
          </p>
        ) : bai.length === 0 ? (
          // Không dùng <TrongRong> ở đây: khung nét đứt cỡ lớn lặp lại cho từng chương sẽ
          // át cả trang. Một dòng ngắn trong khung nhạt là đủ để phân biệt "trống" với "hỏng".
          <p className="border-border text-muted-foreground rounded-lg border border-dashed px-3 py-4 text-center text-sm">
            Chương này chưa có bài học nào.
          </p>
        ) : (
          <DndContext id={`keo-bai-${chuong.id}`} sensors={sensors} collisionDetection={closestCenter} onDragEnd={ketThucKeo}>
            <SortableContext items={bai.map((l) => l.id)} strategy={verticalListSortingStrategy}>
              <ul className="flex flex-col gap-2">
                {bai.map((l) => (
                  <TheBai key={l.id} bai={l} suaDuoc={suaDuoc} chapterId={chuong.id} />
                ))}
              </ul>
            </SortableContext>
          </DndContext>
        )}

        {/* Giữ nguyên ô nhập ở trạng thái `disabled` khi phiên bản đã khoá, không ẩn đi —
            xem ghi chú ở trinh-soan-thao.tsx. */}
        <div className="flex flex-wrap gap-2">
          <Input
            className="min-w-40 flex-1"
            value={tenBaiMoi}
            disabled={!suaDuoc}
            aria-label={`Tên bài học mới trong chương ${chuong.title}`}
            onChange={(e) => setTenBaiMoi(e.target.value)}
            placeholder={suaDuoc ? "Tên bài học mới" : "Đã khoá — phiên bản chỉ xem"}
            onKeyDown={(e) => {
              if (e.key === "Enter" && tenBaiMoi.trim()) themBai.mutate(tenBaiMoi.trim());
            }}
          />
          <Button
            size="sm"
            variant="outline"
            className="h-9"
            onClick={() => themBai.mutate(tenBaiMoi.trim())}
            disabled={!suaDuoc || !tenBaiMoi.trim() || themBai.isPending}
          >
            <Plus className="size-4" aria-hidden />
            Thêm bài
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
