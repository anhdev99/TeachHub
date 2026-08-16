"use client";

import {
  DndContext, KeyboardSensor, MouseSensor, TouchSensor,
  closestCenter, useSensor, useSensors, type DragEndEvent,
} from "@dnd-kit/core";
import {
  SortableContext, arrayMove, sortableKeyboardCoordinates,
  useSortable, verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FileUp, GripVertical, Lightbulb } from "lucide-react";
import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { TrongRong } from "@/components/shell/trang";
import { apiFetch } from "@/lib/api/fetcher";
import { diem, khoangCach, ngayGio } from "@/lib/dinh-dang";
import type { AssignmentDto, SubmissionSlotDto } from "@/lib/api/types";

const DINH_DANG_GOI_Y = [".zip", ".pdf", ".docx", ".jpg", ".jpeg", ".png", ".mp4"];

/** Một dòng "nhãn — giá trị" trong bảng thiết lập. */
function MucThietLap({ nhan, children }: { nhan: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5">
      <dt className="text-muted-foreground text-xs tracking-wide uppercase">{nhan}</dt>
      <dd className="text-sm text-pretty">{children}</dd>
    </div>
  );
}

/**
 * Một phần nộp, kéo được để đổi thứ tự.
 *
 * `id` của dnd-kit là uuid THẬT của slot chứ không phải chỉ số mảng: dùng chỉ số thì sau mỗi
 * cú kéo React tái sử dụng nhầm DOM node và nút "Xóa" của hàng này nằm lại trên hàng khác —
 * xóa nhầm một phần nộp đang có bài của sinh viên là thiệt hại không hoàn lại được.
 */
function HangSlot({
  s,
  thuTu,
  dangKeo,
  onXoa,
}: {
  s: SubmissionSlotDto;
  thuTu: number;
  dangKeo: boolean;
  onXoa: () => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } =
    useSortable({ id: s.id });

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={`border-border bg-card flex items-start gap-2 rounded-lg border p-2.5 ${
        isDragging ? "border-primary opacity-60" : ""
      }`}
    >
      {/* touch-none = touch-action:none, bắt buộc cho tay cầm: thiếu nó thì trên điện thoại
          trình duyệt nuốt cử chỉ để cuộn và tay cầm không kéo được. */}
      <button
        type="button"
        className="text-muted-foreground hover:text-foreground hover:bg-accent grid size-11 shrink-0 cursor-grab touch-none place-items-center rounded-lg transition-colors active:cursor-grabbing"
        aria-label={`Kéo để đổi thứ tự phần nộp ${s.label}`}
        {...attributes}
        {...listeners}
      >
        <GripVertical className="size-4" aria-hidden />
      </button>

      <div className="flex min-w-0 flex-1 flex-col gap-1 py-1.5">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <span className="text-muted-foreground font-mono text-xs tabular-nums">
            {thuTu}.
          </span>
          <span className="font-medium">{s.label}</span>
          <code className="text-muted-foreground font-mono text-xs">{s.slot_key}</code>
          {s.is_required ? (
            <Badge className="bg-warn-soft text-warn border-warn/25 border">Bắt buộc</Badge>
          ) : (
            <Badge className="bg-card text-muted-foreground border-border border">
              Không bắt buộc
            </Badge>
          )}
          {s.allow_link_instead_of_file && (
            <Badge className="bg-card text-muted-foreground border-border border">
              Cho nộp link
            </Badge>
          )}
        </div>

        <p className="text-muted-foreground text-sm text-pretty">
          {s.accepted_file_types.join(", ")} · tối đa {s.max_files} tệp ·{" "}
          {s.max_file_size_mb} MB mỗi tệp
        </p>

        {(s.required_files.length > 0 || s.required_folders.length > 0) && (
          <p className="text-muted-foreground text-sm text-pretty">
            Cấu trúc bắt buộc trong ZIP:{" "}
            <span className="font-mono text-xs">
              {[...s.required_files, ...s.required_folders.map((f) => `${f}/`)].join(", ")}
            </span>
          </p>
        )}
      </div>

      <Button
        size="sm"
        variant="ghost"
        className="text-muted-foreground hover:text-danger min-h-11 shrink-0"
        disabled={dangKeo}
        onClick={onXoa}
      >
        Xóa
      </Button>
    </li>
  );
}

export function SoanBaiTap({ banDau }: { banDau: AssignmentDto }) {
  const qc = useQueryClient();
  const khoa = ["assignment", banDau.id] as const;

  const { data: bai } = useQuery({
    queryKey: khoa,
    queryFn: () => apiFetch<AssignmentDto>(`/api/assignments/${banDau.id}`),
    initialData: banDau,
  });

  const [moThem, setMoThem] = useState(false);

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

  // MouseSensor + TouchSensor thay cho PointerSensor: pointer event gộp chuột với cảm ứng nên
  // PointerSensor bắt đầu kéo ngay khi ngón tay trượt 8px, tức mọi thao tác cuộn trang trên
  // điện thoại đều biến thành kéo phần nộp.
  const sensors = useSensors(
    useSensor(MouseSensor, { activationConstraint: { distance: 8 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 200, tolerance: 8 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const sapXep = useMutation({
    mutationFn: (v: { slotId: string; toIndex: number }) =>
      apiFetch(`/api/submission-slots/${v.slotId}/reorder`, {
        method: "PUT",
        body: JSON.stringify({ to_index: v.toIndex }),
      }),

    onMutate: async (v) => {
      // Chặn refetch đang bay ghi đè bản lạc quan — nếu không, phần vừa kéo nhảy về chỗ cũ
      // rồi lại nhảy tới. Lỗi này gần như không tái hiện được trên máy nhanh.
      await qc.cancelQueries({ queryKey: khoa });
      const truoc = qc.getQueryData<AssignmentDto>(khoa);

      qc.setQueryData<AssignmentDto>(khoa, (cu) => {
        if (!cu) return cu;
        const tu = cu.slots.findIndex((s) => s.id === v.slotId);
        if (tu < 0) return cu;
        const ds = arrayMove(cu.slots, tu, v.toIndex);
        return { ...cu, slots: ds.map((s, i) => ({ ...s, order_index: i })) };
      });

      return { truoc };
    },

    onError: (e: Error, _v, ctx) => {
      // Không hoàn tác thì giao diện giữ thứ tự mới dù server đã từ chối: giảng viên tin là
      // đã lưu, tải lại trang mới thấy thứ tự cũ và không hiểu mình vừa mất gì.
      if (ctx?.truoc) qc.setQueryData(khoa, ctx.truoc);
      toast.error(e.message);
    },

    // Server đánh số lại order_index về 0..n-1; thiếu bước này thì lần kéo sau tính trên chỉ
    // số đã lệch.
    onSettled: () => lamMoi(),
  });

  const keoXong = (e: DragEndEvent) => {
    const { active, over } = e;
    if (!over || active.id === over.id) return;
    const den = bai.slots.findIndex((s) => s.id === over.id);
    if (den < 0) return;
    sapXep.mutate({ slotId: String(active.id), toIndex: den });
  };

  const soBatBuoc = bai.slots.filter((s) => s.is_required).length;

  return (
    <div className="flex flex-col gap-6">
      {/* ── Nhóm 1: thiết lập chung ─────────────────────────────────────────────────────
       * Chỉ để ĐỌC ở màn hình này. Hạn nộp và mức phạt trễ quyết định điểm của cả lớp mà
       * trước đây không hiện ở đâu trong trang soạn bài tập — giảng viên phải mở lại lớp
       * học phần mới biết mình đang sửa phần nộp cho một bài tập đã quá hạn.
       */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Thiết lập chung</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <MucThietLap nhan="Hình thức nộp">
              {bai.submission_mode === "group" ? "Theo nhóm" : "Cá nhân"}
            </MucThietLap>

            <MucThietLap nhan="Hạn nộp">
              {bai.due_at ? (
                // Không tự tính "đã quá hạn chưa" để tô đỏ: so `Date.now()` ngay trong lượt
                // render là hàm không thuần, React Compiler chặn — và bản thân chuỗi tương đối
                // đã nói rõ ("2 ngày trước" so với "trong 2 ngày"), tô thêm màu chỉ là lặp lại.
                <span className="flex flex-wrap items-baseline gap-x-1.5">
                  <time dateTime={bai.due_at}>{ngayGio(bai.due_at)}</time>
                  <span className="text-muted-foreground text-xs">
                    ({khoangCach(bai.due_at)})
                  </span>
                </span>
              ) : (
                <span className="text-warn">Chưa đặt hạn</span>
              )}
            </MucThietLap>

            <MucThietLap nhan="Nộp trễ">
              {bai.allow_late_submission ? (
                <>
                  Nhận bài trễ, trừ{" "}
                  <span className="tabular-nums">
                    {diem(bai.late_penalty_percent_per_day)}%
                  </span>{" "}
                  mỗi ngày
                </>
              ) : (
                "Không nhận bài trễ"
              )}
            </MucThietLap>

            <MucThietLap nhan="Điểm tối đa">
              <span className="tabular-nums">{diem(bai.max_score)}</span>
            </MucThietLap>

            <MucThietLap nhan="Bộ tiêu chí chấm">
              {bai.rubric_id ? (
                "Đã gắn sẵn một bộ tiêu chí"
              ) : (
                <span className="text-muted-foreground">
                  Chưa gắn — chọn khi chấm từng bài
                </span>
              )}
            </MucThietLap>

            <MucThietLap nhan="Phần nộp bắt buộc">
              <span className="tabular-nums">
                {soBatBuoc}/{bai.slots.length}
              </span>
            </MucThietLap>
          </dl>
        </CardContent>
      </Card>

      {/* ── Nhóm 2: gợi ý có trừ điểm ─────────────────────────────────────────────────── */}
      {bai.hints.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Lightbulb className="text-warn size-4" aria-hidden />
              Gợi ý mở khoá ({bai.hints.length})
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="flex flex-col gap-2">
              {bai.hints.map((g, i) => (
                <li
                  key={g.id}
                  className="border-border flex flex-wrap items-start justify-between gap-x-3 gap-y-1 rounded-lg border p-3"
                >
                  <span className="flex min-w-0 flex-1 items-baseline gap-2">
                    <span className="text-muted-foreground font-mono text-xs tabular-nums">
                      {i + 1}.
                    </span>
                    <span className="min-w-0 text-sm text-pretty">{g.content}</span>
                  </span>
                  <Badge className="bg-warn-soft text-warn border-warn/25 shrink-0 border tabular-nums">
                    −{diem(g.penalty_percent)}%
                  </Badge>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}

      {/* ── Nhóm 3: các phần nộp ─────────────────────────────────────────────────────── */}
      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-x-3 gap-y-2">
          <CardTitle className="text-base">Các phần nộp ({bai.slots.length})</CardTitle>
          <div className="flex flex-wrap gap-2">
            <Button
              size="sm"
              variant="outline"
              className="min-h-11"
              disabled={dungMau.isPending}
              onClick={() => dungMau.mutate()}
            >
              {dungMau.isPending ? "Đang thêm…" : "Dùng mẫu gợi ý"}
            </Button>
            <Button
              size="sm"
              className="min-h-11"
              disabled={moThem}
              onClick={() => setMoThem(true)}
            >
              + Thêm phần nộp
            </Button>
          </div>
        </CardHeader>

        <CardContent className="flex flex-col gap-4">
          {/* Biểu mẫu nằm trong thân thẻ chứ không trong đầu thẻ: nhét nó cạnh hai cái nút
              là ép một biểu mẫu sáu ô vào một dải ngang, và ở 390px nó vỡ hẳn. */}
          {moThem && (
            <FormThemSlot
              assignmentId={bai.id}
              onXong={() => {
                setMoThem(false);
                lamMoi();
              }}
              onHuy={() => setMoThem(false)}
            />
          )}

          {bai.slots.length === 0 ? (
            <TrongRong
              Icon={FileUp}
              tieuDe="Chưa có phần nộp nào"
              moTa="Sinh viên không nộp được bài cho tới khi bạn thêm ít nhất một phần. Mẫu gợi ý tạo sẵn các phần thường dùng."
              hanhDong={
                <Button
                  className="min-h-11"
                  disabled={dungMau.isPending}
                  onClick={() => dungMau.mutate()}
                >
                  {dungMau.isPending ? "Đang thêm…" : "Dùng mẫu gợi ý"}
                </Button>
              }
            />
          ) : (
            <>
              <p
                className={`text-sm text-pretty ${
                  soBatBuoc === 0 ? "text-warn" : "text-muted-foreground"
                }`}
              >
                {soBatBuoc === 0
                  ? "Không có phần nào bắt buộc — sinh viên sẽ không nộp được vì không đủ điều kiện."
                  : `${soBatBuoc} phần bắt buộc phải có tệp thì mới nộp chính thức được. Kéo tay cầm để đổi thứ tự — đây cũng là thứ tự sinh viên thấy khi nộp bài.`}
              </p>

              <DndContext
                id="keo-slot-nop"
                sensors={sensors}
                collisionDetection={closestCenter}
                onDragEnd={keoXong}
              >
                <SortableContext
                  items={bai.slots.map((s) => s.id)}
                  strategy={verticalListSortingStrategy}
                >
                  <ul className="flex flex-col gap-2">
                    {bai.slots.map((s, i) => (
                      <HangSlot
                        key={s.id}
                        s={s}
                        thuTu={i + 1}
                        dangKeo={xoaSlot.isPending}
                        onXoa={() => xoaSlot.mutate(s.id)}
                      />
                    ))}
                  </ul>
                </SortableContext>
              </DndContext>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function FormThemSlot({
  assignmentId,
  onXong,
  onHuy,
}: {
  assignmentId: string;
  onXong: () => void;
  onHuy: () => void;
}) {
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
      setKey("");
      setLabel("");
      onXong();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <div className="border-border bg-muted/40 flex flex-col gap-4 rounded-xl border p-3 sm:p-4">
      <h3 className="text-sm font-medium">Phần nộp mới</h3>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="slot_key">Khóa (chữ thường, không dấu)</Label>
          <Input
            id="slot_key"
            className="bg-card min-h-11"
            value={key}
            placeholder="report"
            onChange={(e) => setKey(e.target.value)}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="slot_label">Nhãn hiển thị</Label>
          <Input
            id="slot_label"
            className="bg-card min-h-11"
            value={label}
            placeholder="Báo cáo (PDF)"
            onChange={(e) => setLabel(e.target.value)}
          />
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <Label>Định dạng nhận</Label>
        <div className="flex flex-wrap gap-x-3 gap-y-2">
          {DINH_DANG_GOI_Y.map((d) => (
            <label
              key={d}
              className="bg-card border-border flex min-h-11 cursor-pointer items-center gap-2 rounded-lg border px-3 text-sm"
            >
              <Checkbox
                checked={dinhDang.includes(d)}
                onCheckedChange={(c) =>
                  setDinhDang((cu) => (c === true ? [...cu, d] : cu.filter((x) => x !== d)))
                }
              />
              <code className="font-mono text-xs">{d}</code>
            </label>
          ))}
        </div>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="max_files">Số tệp tối đa</Label>
          <Input id="max_files" type="number" min={1} max={50} value={soFile}
                 className="bg-card min-h-11"
                 onChange={(e) => setSoFile(Number(e.target.value))} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="max_size">Dung lượng mỗi tệp (MB)</Label>
          <Input id="max_size" type="number" min={1} max={500} value={dungLuong}
                 className="bg-card min-h-11"
                 onChange={(e) => setDungLuong(Number(e.target.value))} />
        </div>
      </div>

      <label className="flex min-h-11 cursor-pointer items-center gap-2 text-sm">
        <Checkbox checked={batBuoc} onCheckedChange={(c) => setBatBuoc(c === true)} />
        Bắt buộc phải nộp phần này
      </label>

      <div className="flex flex-wrap gap-2">
        <Button className="min-h-11" disabled={them.isPending} onClick={() => them.mutate()}>
          {them.isPending ? "Đang lưu…" : "Thêm phần nộp"}
        </Button>
        <Button variant="ghost" className="min-h-11" onClick={onHuy}>
          Hủy
        </Button>
      </div>
    </div>
  );
}
