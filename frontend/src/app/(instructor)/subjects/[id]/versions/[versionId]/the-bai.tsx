"use client";

import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ChevronRight, GripVertical, Paperclip, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { ICON_LOAI } from "@/components/noi-dung/loai-phan";
import { apiFetch } from "@/lib/api/fetcher";
import {
  LOAI_CAN_REF,
  NHAN_LOAI_COMPONENT,
  type LessonComponentType,
  type LessonDto,
} from "@/lib/api/types";
import { cn } from "@/lib/utils";

/** Tám loại component của SRS; loại cần ref_id bị loại khỏi menu nhanh vì phải chọn bản ghi đích. */
const LOAI_THEM_NHANH = (Object.keys(NHAN_LOAI_COMPONENT) as LessonComponentType[]).filter(
  (t) => !LOAI_CAN_REF.includes(t),
);

/**
 * Một bài học trong trình soạn thảo — khuôn "curriculum builder" của Udemy.
 *
 * Ba điều bản trước làm sai và đây là chỗ sửa:
 *
 * 1. **Dòng bài học không nói gì.** Chỉ có tên và một con số "N phần". Nhìn một chương 20 bài
 *    không biết bài nào có bài tập, bài nào có trắc nghiệm, bài nào còn trống. Nay mỗi phần
 *    hiện thành một chip có icon ngay trên dòng, không cần mở ra mới thấy.
 * 2. **Quá thưa.** Mỗi bài chiếm một thẻ có viền, bo góc, cách nhau 8px — ba bài học ăn hết
 *    nửa màn hình 1500px. Nay là hàng cao 40px, lồng trong khối chương.
 * 3. **Thao tác lúc nào cũng hiện.** Nút xoá và tay cầm kéo bày ra ở mọi dòng làm mắt không
 *    biết nhìn đâu. Nay chúng chỉ hiện khi rê chuột hoặc khi focus bàn phím — `focus-within`
 *    là bắt buộc, thiếu nó thì người dùng bàn phím không bao giờ tới được nút xoá.
 */
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
      setMo(true);
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
      className={cn(
        "border-border bg-card border-b last:border-b-0",
        isDragging && "relative z-10 shadow-md",
      )}
    >
      {/* ── Hàng bài học ────────────────────────────────────────────────────────────────── */}
      <div className="group hover:bg-accent/40 flex items-center gap-1.5 px-2 py-1.5 transition-colors">
        {/* Tay cầm kéo chiếm chỗ CỐ ĐỊNH kể cả khi ẩn (`invisible`, không phải `hidden`):
            dùng `hidden` thì cả hàng dịch ngang 24px lúc rê chuột vào — cảm giác giật. */}
        <span
          {...(suaDuoc ? attributes : {})}
          {...(suaDuoc ? listeners : {})}
          aria-label={suaDuoc ? `Kéo để đổi thứ tự bài ${bai.title}` : undefined}
          role={suaDuoc ? "button" : undefined}
          className={cn(
            "text-muted-foreground grid size-6 shrink-0 place-items-center rounded",
            suaDuoc
              ? "cursor-grab opacity-0 group-hover:opacity-100 focus-visible:opacity-100 active:cursor-grabbing"
              : "invisible",
          )}
        >
          <GripVertical className="size-3.5" aria-hidden />
        </span>

        <button
          type="button"
          onClick={() => setMo((v) => !v)}
          aria-expanded={mo}
          className="text-muted-foreground hover:text-foreground grid size-6 shrink-0 place-items-center rounded transition-colors"
          aria-label={mo ? "Thu gọn bài học" : "Mở bài học"}
        >
          <ChevronRight
            className={cn("size-3.5 transition-transform", mo && "rotate-90")}
            aria-hidden
          />
        </button>

        {/* `tabular-nums` để cột số không răng cưa khi chương có trên 10 bài. */}
        <span className="text-muted-foreground w-5 shrink-0 text-right text-xs tabular-nums">
          {bai.order_index + 1}
        </span>

        <span className="min-w-0 flex-1 truncate text-sm font-medium">{bai.title}</span>

        {/* Các phần hiện NGAY TRÊN HÀNG dưới dạng chip icon. Đây là thứ trả lời câu hỏi
            "chương này đã có bài tập chưa" mà không phải mở từng bài ra xem. */}
        {bai.components.length > 0 && (
          <span className="hidden shrink-0 items-center gap-1 sm:flex">
            {bai.components.slice(0, 5).map((c) => {
              const Icon = ICON_LOAI[c.type];
              return (
                <span
                  key={c.id}
                  title={NHAN_LOAI_COMPONENT[c.type]}
                  className="bg-muted text-muted-foreground grid size-5 place-items-center rounded"
                >
                  <Icon className="size-3" />
                  <span className="sr-only">{NHAN_LOAI_COMPONENT[c.type]}</span>
                </span>
              );
            })}
            {bai.components.length > 5 && (
              <span className="text-muted-foreground text-xs tabular-nums">
                +{bai.components.length - 5}
              </span>
            )}
          </span>
        )}

        {bai.file_url && (
          <span
            title="Bài này đã có tệp nội dung"
            className="text-muted-foreground grid size-5 shrink-0 place-items-center"
          >
            <Paperclip className="size-3.5" aria-hidden />
            <span className="sr-only">Có tệp nội dung</span>
          </span>
        )}
      </div>

      {/* ── Chi tiết khi mở ─────────────────────────────────────────────────────────────── */}
      {mo && (
        <div className="border-border bg-muted/30 border-t px-2 py-2 pl-10">
          {bai.components.length === 0 ? (
            // Một bài không có phần nào là HỢP LỆ (SRS mục 5.1) — nói rõ để người dùng không
            // tưởng là thiếu dữ liệu hoặc tải lỗi.
            <p className="text-muted-foreground py-1 text-xs">
              Chưa gắn phần nào — hoàn toàn hợp lệ.
            </p>
          ) : (
            <ul className="mb-1.5 flex flex-col">
              {bai.components.map((c) => {
                const Icon = ICON_LOAI[c.type];
                return (
                  <li
                    key={c.id}
                    className="group/phan hover:bg-card flex items-center gap-2 rounded px-1.5 py-1 transition-colors"
                  >
                    <Icon className="text-muted-foreground size-3.5 shrink-0" aria-hidden />
                    <span className="text-muted-foreground w-4 shrink-0 text-right text-xs tabular-nums">
                      {c.order_index + 1}
                    </span>
                    <span className="min-w-0 flex-1 truncate text-sm">
                      {NHAN_LOAI_COMPONENT[c.type]}
                    </span>

                    {suaDuoc && (
                      <button
                        type="button"
                        onClick={() => xoaComponent.mutate(c.id)}
                        aria-label={`Xoá phần ${NHAN_LOAI_COMPONENT[c.type]}`}
                        className="text-muted-foreground hover:text-danger hover:bg-danger-soft grid size-6 shrink-0 place-items-center rounded opacity-0 transition-colors group-hover/phan:opacity-100 focus-visible:opacity-100"
                      >
                        <Trash2 className="size-3.5" aria-hidden />
                      </button>
                    )}
                  </li>
                );
              })}
            </ul>
          )}

          {/* Thêm phần bằng MỘT cú bấm cho từng loại, không phải chọn dropdown rồi bấm nút.
              Soạn một bài là thêm 4–5 phần liên tiếp; bắt đi qua dropdown mỗi lần là gấp đôi
              số thao tác cho việc làm nhiều nhất trên màn hình này. */}
          {suaDuoc && (
            <div className="flex flex-wrap items-center gap-1">
              {LOAI_THEM_NHANH.map((t) => {
                const Icon = ICON_LOAI[t];
                return (
                  <button
                    key={t}
                    type="button"
                    disabled={themComponent.isPending}
                    onClick={() => themComponent.mutate(t)}
                    className="border-border text-muted-foreground hover:border-primary hover:text-foreground hover:bg-card inline-flex items-center gap-1 rounded-md border border-dashed px-2 py-1 text-xs transition-colors disabled:opacity-50"
                  >
                    <Plus className="size-3" aria-hidden />
                    <Icon className="size-3" aria-hidden />
                    {NHAN_LOAI_COMPONENT[t]}
                  </button>
                );
              })}
            </div>
          )}

          {loi && (
            <p className="bg-danger-soft text-danger border-danger/25 mt-1.5 rounded border px-2 py-1 text-xs">
              {loi}
            </p>
          )}
        </div>
      )}
    </li>
  );
}
