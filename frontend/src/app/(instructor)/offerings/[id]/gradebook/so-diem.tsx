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
import {
  AlertTriangle, Download, GripVertical, Loader2, Plus, Settings2, Table2, Trash2, Users,
} from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { HorizontalScroll, EmptyState } from "@/components/shell/page-shell";
import { formatScore } from "@/lib/format";
import { apiFetch } from "@/lib/api/fetcher";
import {
  AGGREGATION_KIND_LABELS, type AggregationKind, type GradebookDto, type GradeColumnDto,
} from "@/lib/api/types";
import { CapDiemCong } from "./cap-diem-cong";

type Nguon = { type: "assignment" | "final_assessment"; id: string; title: string };

/**
 * Hai cột định danh được GHIM khi cuộn ngang.
 *
 * `left` của cột thứ hai phải bằng ĐÚNG bề rộng thật của cột thứ nhất, và đó là lý do phải
 * có `min-w-*` chứ không chỉ `w-*`: với `table-auto`, `width` chỉ là gợi ý — trình duyệt đo
 * được cột MSSV chỉ cần 88px thì nó co lại còn 88px, trong khi cột Họ tên vẫn dính ở mốc
 * 96px. Khe 8px ở giữa để lọt đúng phần nội dung đang cuộn qua, và trông như chữ chồng chữ.
 * `min-width` thì thuật toán bảng buộc phải tôn trọng.
 */
const O_MSSV = "sticky left-0 w-24 min-w-24 sm:w-28 sm:min-w-28";
const O_HO_TEN =
  "sticky left-24 sm:left-28 w-32 min-w-32 max-w-32 sm:w-52 sm:min-w-52 sm:max-w-52 " +
  "border-r-2 border-border";

/**
 * Ô ghim BẮT BUỘC phải có z-index. Thiếu nó thì ô nằm sau trong DOM (chính là các cột đang
 * trôi qua khi cuộn) vẽ đè lên ô ghim: mã sinh viên và ô nhập điểm chồng lên nhau thành một
 * mớ không đọc được — đúng vào lúc cuộn tới cột cuối, tức là đúng lúc cần cột ghim nhất.
 * Hàng tiêu đề cao hơn thân bảng một bậc để hàng đầu tiên không che nó.
 */
const GHIM_HEADER = "z-20";
const GHIM_O = "z-10";

export function SoDiem({
  offeringId,
  banDau,
  nguon,
}: {
  offeringId: string;
  banDau: GradebookDto;
  nguon: Nguon[];
}) {
  const qc = useQueryClient();
  const [tamTinh, setTamTinh] = useState(false);
  const khoa = ["gradebook", offeringId, tamTinh] as const;

  const { data: so } = useQuery({
    queryKey: khoa,
    queryFn: () =>
      apiFetch<GradebookDto>(`/api/offerings/${offeringId}/gradebook?provisional=${tamTinh}`),
    initialData: banDau,
  });

  const lamMoi = () => qc.invalidateQueries({ queryKey: ["gradebook", offeringId] });

  const khoiTao = useMutation({
    mutationFn: () =>
      apiFetch(`/api/offerings/${offeringId}/gradebook/init`, {
        method: "POST",
        body: JSON.stringify({ template_id: null }),
      }),
    onSuccess: () => {
      toast.success("Đã tạo bộ cột mặc định: Thường kỳ 40% · Cuối môn 60%.");
      lamMoi();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const suaO = useMutation({
    mutationFn: (v: { columnId: string; enrollmentId: string; score: number | null }) =>
      apiFetch(`/api/grade-columns/${v.columnId}/grades`, {
        method: "PUT",
        body: JSON.stringify({
          enrollment_id: v.enrollmentId,
          score: v.score,
          note: null,
        }),
      }),
    onSuccess: () => lamMoi(),
    onError: (e: Error) => toast.error(e.message),
  });

  if (so.columns.length === 0) {
    return (
      <Card>
        <CardContent>
          <EmptyState
            Icon={Table2}
            title="Lớp này chưa có cột điểm nào"
            description="Tạo bộ cột mặc định để bắt đầu — tên cột, trọng số và quy tắc gộp sửa lại được bất cứ lúc nào."
            action={
              <Button
                className="min-h-11"
                disabled={khoiTao.isPending}
                onClick={() => khoiTao.mutate()}
              >
                {khoiTao.isPending ? "Đang tạo…" : "Tạo bộ cột mặc định"}
              </Button>
            }
          />
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-3">
        <label className="flex min-h-11 items-center gap-2.5 text-sm">
          <Switch checked={tamTinh} onCheckedChange={setTamTinh} />
          Xem điểm tạm tính <span className="text-muted-foreground">(bỏ qua bài chưa chấm)</span>
        </label>

        <div className="flex flex-wrap items-center gap-2">
          {/* Endpoint xuất mặc định trả .xlsx; CSV phải hỏi bằng ?format=csv. Nhãn phải khớp
              định dạng thật — ghi "CSV" rồi tải về một tệp .xlsx là giảng viên mở bằng công cụ
              sai rồi tưởng tệp hỏng. */}
          <Button asChild variant="outline" size="sm">
            <a href={`/api/offerings/${offeringId}/gradebook/export`}>
              <Download className="size-4" aria-hidden />
              Xuất Excel
            </a>
          </Button>

          <Button asChild variant="ghost" size="sm">
            <a href={`/api/offerings/${offeringId}/gradebook/export?format=csv`}>Xuất CSV</a>
          </Button>

          <CauHinhCot offeringId={offeringId} so={so} nguon={nguon} onXong={lamMoi} />
        </div>
      </div>

      {!so.weights_valid && so.weights_error && (
        <p className="bg-danger-soft text-danger border-danger/25 flex items-start gap-2 rounded-lg border px-3 py-2 text-sm">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span className="text-pretty">{so.weights_error}</span>
        </p>
      )}

      {so.unknown_condition_column_keys.length > 0 && (
        <p className="bg-danger-soft text-danger border-danger/25 flex items-start gap-2 rounded-lg border px-3 py-2 text-sm">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span className="text-pretty">
            Điều kiện điểm sàn trỏ tới cột không tồn tại:{" "}
            <span className="font-mono text-xs">
              {so.unknown_condition_column_keys.join(", ")}
            </span>{" "}
            — điều kiện đó đang <strong className="font-semibold">KHÔNG</strong> có hiệu lực.
          </span>
        </p>
      )}

      {so.rows.length === 0 ? (
        <Card>
          <CardContent>
            <EmptyState
              Icon={Users}
              title="Lớp chưa có sinh viên nào"
              description="Cột điểm đã sẵn sàng. Nhập danh sách sinh viên cho lớp là bảng này có hàng để nhập."
            />
          </CardContent>
        </Card>
      ) : (
        // `overflow-hidden` là bắt buộc, không phải trang trí: <HorizontalScroll> có `-mx-1` nên
        // vùng cuộn thò ra ngoài khung 4px mỗi bên. Không cắt thì đúng dải 4px đó hiện phần
        // nội dung đang cuộn qua NGAY BÊN TRÁI cột ghim — trông như chữ chồng lên nhau.
        <div className="bg-card border-border overflow-hidden rounded-xl border">
          {/* Bảng rộng cuộn trong KHỐI RIÊNG (quy tắc §5), không phải cả trang cuộn ngang. */}
          <HorizontalScroll>
            {/* border-separate chứ không border-collapse: với border-collapse, đường kẻ của
                ô `position: sticky` bị trình duyệt vẽ vào lớp bảng chứ không đi theo ô, nên
                cột ghim mất viền ngay khi bắt đầu cuộn. */}
            <table className="w-full border-separate border-spacing-0 text-sm">
              <thead>
                <tr>
                  <th
                    className={`${O_MSSV} ${GHIM_HEADER} bg-card border-border border-b px-2 py-2 text-left align-bottom`}
                  >
                    MSSV
                  </th>
                  <th
                    className={`${O_HO_TEN} ${GHIM_HEADER} bg-card border-border border-b px-2 py-2 text-left align-bottom`}
                  >
                    Họ tên
                  </th>
                  {so.columns.map((c) => (
                    <th
                      key={c.id}
                      className="bg-card border-border border-b px-2 py-2 text-left align-bottom whitespace-nowrap"
                    >
                      {c.name}
                      <span className="text-muted-foreground block text-xs font-normal">
                        {c.weight_percent}% · {AGGREGATION_KIND_LABELS[c.kind]}
                      </span>
                    </th>
                  ))}
                  <th className="bg-card border-border border-b px-2 py-2 text-left align-bottom whitespace-nowrap">
                    Điểm cộng
                  </th>
                  <th className="bg-card border-border border-b px-2 py-2 text-left align-bottom">
                    Tổng
                  </th>
                  <th className="bg-card border-border border-b px-2 py-2" />
                </tr>
              </thead>

              <tbody>
                {so.rows.map((h) => (
                  // Cả hàng sáng lên khi rê chuột: với bảng rộng phải cuộn ngang, đây là thứ
                  // giữ mắt bám đúng một sinh viên trên đường đi tới cột "Cuối môn".
                  <tr key={h.enrollment_id} className="hover:bg-accent/50 group/hang">
                    <td
                      className={`${O_MSSV} ${GHIM_O} bg-card border-border group-hover/hang:bg-accent/50 border-b px-2 py-1.5 font-mono text-xs`}
                    >
                      {h.student_code}
                    </td>
                    <td
                      className={`${O_HO_TEN} ${GHIM_O} bg-card border-border group-hover/hang:bg-accent/50 truncate border-b px-2 py-1.5 whitespace-nowrap`}
                      title={h.full_name}
                    >
                      {h.full_name}
                    </td>

                    {so.columns.map((c) => {
                      const o = h.cells.find((x) => x.column_id === c.id);

                      // Ba trạng thái của một ô điểm phải phân biệt được: đang lưu (chưa
                      // chắc chắn), sửa tay (không bị chấm bài ghi đè), tự tính (dẫn xuất từ
                      // nguồn). Nhìn giống nhau thì giảng viên không biết số nào là số của mình.
                      const dangLuu =
                        suaO.isPending &&
                        suaO.variables?.columnId === c.id &&
                        suaO.variables?.enrollmentId === h.enrollment_id;

                      return (
                        <td key={c.id} className="border-border border-b px-2 py-1.5">
                          <div className="flex items-center gap-1.5">
                            <div className="relative">
                              <Input
                                type="number" step="0.25" min={0} max={10}
                                defaultValue={o?.score ?? ""}
                                aria-busy={dangLuu}
                                aria-label={`${c.name} của ${h.full_name}`}
                                title={
                                  o?.is_manual_override
                                    ? "Điểm sửa tay — chấm lại bài sẽ không ghi đè"
                                    : "Điểm do quy tắc gộp tự tính"
                                }
                                className={`h-9 w-20 tabular-nums transition-colors ${
                                  o?.is_manual_override
                                    ? "border-primary ring-primary/25 font-medium ring-1"
                                    : ""
                                } ${dangLuu ? "text-muted-foreground pr-7" : ""}`}
                                onBlur={(e) => {
                                  const v = e.target.value === "" ? null : Number(e.target.value);
                                  if (v === (o?.score ?? null)) return;
                                  suaO.mutate({
                                    columnId: c.id, enrollmentId: h.enrollment_id, score: v,
                                  });
                                }}
                              />
                              {dangLuu && (
                                <Loader2
                                  className="text-muted-foreground pointer-events-none absolute top-1/2 right-2 size-3.5 -translate-y-1/2 animate-spin"
                                  aria-hidden
                                />
                              )}
                            </div>

                            {(o?.bonus ?? 0) > 0 && (
                              <span
                                className="text-ok text-xs font-medium tabular-nums"
                                title="Điểm cộng đã cấp vào cột này"
                              >
                                +{formatScore(o!.bonus)}
                              </span>
                            )}
                          </div>
                        </td>
                      );
                    })}

                    <td className="border-border border-b px-2 py-1.5 tabular-nums">
                      {h.bonus_to_final > 0 ? (
                        <span className="text-ok font-medium">+{formatScore(h.bonus_to_final)}</span>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </td>
                    <td className="border-border border-b px-2 py-1.5 text-base font-semibold tabular-nums">
                      {formatScore(h.final_score)}
                    </td>
                    <td className="border-border border-b px-1 py-1.5">
                      <CapDiemCong
                        offeringId={offeringId}
                        enrollmentId={h.enrollment_id}
                        tenSinhVien={h.full_name}
                        cot={so.columns}
                        macDinhTarget={so.bonus_default_target}
                        macDinhColumnId={so.bonus_default_column_id}
                        onXong={lamMoi}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </HorizontalScroll>
        </div>
      )}

      {/* Chú giải đặt ngay dưới bảng, có mẫu màu thật: mô tả bằng lời "ô viền đậm" bắt người
          đọc tự dịch sang thứ đang nhìn thấy. */}
      <div className="text-muted-foreground flex flex-col gap-1.5 text-sm">
        <div className="flex flex-wrap items-center gap-x-5 gap-y-2">
          <span className="flex items-center gap-2">
            <span className="border-input bg-background h-5 w-9 rounded-md border" aria-hidden />
            Tự tính theo quy tắc gộp
          </span>
          <span className="flex items-center gap-2">
            <span
              className="border-primary ring-primary/25 bg-background h-5 w-9 rounded-md border ring-1"
              aria-hidden
            />
            Đã sửa tay
          </span>
          <span className="flex items-center gap-2">
            <Loader2 className="size-4 animate-spin" aria-hidden />
            Đang lưu
          </span>
        </div>
        <p className="text-pretty">
          Chấm lại bài trong một cột <strong className="font-medium">không</strong> ghi đè lên ô
          đã sửa tay. Xoá trắng ô để trả nó về cho quy tắc tổng hợp tự tính.
        </p>
      </div>
    </div>
  );
}

/**
 * Một hàng cấu hình cột, kéo được để đổi thứ tự.
 *
 * `id` của dnd-kit là uuid THẬT của cột, không phải chỉ số mảng. Dùng chỉ số thì sau mỗi cú
 * kéo React tái sử dụng nhầm DOM node: ô input đang gõ dở giữ nguyên giá trị cũ nhưng nhảy
 * sang cột khác, và giảng viên sửa trọng số của cột mình không định sửa.
 */
function HangCot({
  c,
  nguon,
  onDoi,
  onXoa,
  onXongNguon,
}: {
  c: GradeColumnDto;
  nguon: Nguon[];
  onDoi: (moi: Partial<GradeColumnDto>) => void;
  onXoa: () => void;
  onXongNguon: () => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } =
    useSortable({ id: c.id });

  return (
    <div
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={`border-border bg-card flex flex-wrap items-end gap-2 rounded-lg border p-2 ${
        isDragging ? "opacity-50 shadow-lg" : ""
      }`}
    >
      {/* Tay cầm riêng thay vì kéo cả hàng: cả hàng đều là input, kéo được cả hàng thì mọi
          thao tác bôi đen chữ trong ô đều biến thành kéo cột.
          touch-none = touch-action:none — thiếu nó thì trên điện thoại trình duyệt nuốt mất
          cử chỉ để cuộn trang và tay cầm không kéo được. */}
      <button
        type="button"
        className="text-muted-foreground hover:text-foreground hover:bg-accent grid h-11 w-8 shrink-0 cursor-grab touch-none place-items-center rounded-md transition-colors active:cursor-grabbing"
        aria-label={`Kéo để đổi thứ tự cột ${c.name}`}
        {...attributes}
        {...listeners}
      >
        <GripVertical className="size-4" aria-hidden />
      </button>

      <div className="flex flex-col gap-1">
        <Label className="text-muted-foreground text-xs">Tên cột</Label>
        <Input
          className="w-40" value={c.name}
          onChange={(e) => onDoi({ name: e.target.value })}
        />
      </div>

      <div className="flex flex-col gap-1">
        <Label className="text-muted-foreground text-xs">Khóa</Label>
        {/* Khóa cột nối cột của LỚP với mẫu xuất Excel của GIẢNG VIÊN, nên nó chỉ đọc:
            đổi khóa là làm gãy mẫu xuất đang dùng. */}
        <Input className="text-muted-foreground w-32 font-mono text-xs" value={c.key} readOnly />
      </div>

      <div className="flex flex-col gap-1">
        <Label className="text-muted-foreground text-xs">Trọng số %</Label>
        <Input
          type="number" min={0} max={100} className="w-24 tabular-nums" value={c.weight_percent}
          onChange={(e) => onDoi({ weight_percent: Number(e.target.value) })}
        />
      </div>

      <div className="flex flex-col gap-1">
        <Label className="text-muted-foreground text-xs">Quy tắc gộp</Label>
        <Select value={c.kind} onValueChange={(v) => onDoi({ kind: v as AggregationKind })}>
          <SelectTrigger className="w-48"><SelectValue /></SelectTrigger>
          <SelectContent>
            {Object.entries(AGGREGATION_KIND_LABELS).map(([k, nhan]) => (
              <SelectItem key={k} value={k}>{nhan}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {(c.kind === "drop_lowest_n" || c.kind === "best_n_of_m") && (
        <div className="flex flex-col gap-1">
          <Label className="text-muted-foreground text-xs">N</Label>
          <Input
            type="number" min={0} className="w-20 tabular-nums" value={c.n}
            onChange={(e) => onDoi({ n: Number(e.target.value) })}
          />
        </div>
      )}

      <GanNguon columnId={c.id} nguon={nguon} daGan={c.sources} onXong={onXongNguon} />

      <Button
        size="sm"
        variant="ghost"
        className="text-muted-foreground hover:text-danger hover:bg-danger-soft"
        onClick={onXoa}
      >
        <Trash2 className="size-4" aria-hidden />
        Xóa cột
      </Button>
    </div>
  );
}

function CauHinhCot({
  offeringId,
  so,
  nguon,
  onXong,
}: {
  offeringId: string;
  so: GradebookDto;
  nguon: Nguon[];
  onXong: () => void;
}) {
  const qc = useQueryClient();
  const [mo, setMo] = useState(false);
  const [cot, setCot] = useState<GradeColumnDto[]>(so.columns);

  // Tiền tố, KHÔNG kèm cờ "tạm tính": sổ điểm nằm trong cache dưới hai khoá
  // ["gradebook", offeringId, false] và [..., true]. Ghi lạc quan vào một khoá thôi thì gạt
  // công tắc "xem điểm tạm tính" là thứ tự cột nhảy về như cũ.
  const khoaSo = ["gradebook", offeringId] as const;

  // MouseSensor + TouchSensor chứ KHÔNG phải PointerSensor: pointer event gộp cả chuột lẫn
  // chạm, nên PointerSensor sẽ bắt đầu kéo ngay khi ngón tay trượt 8px — tức là mọi thao tác
  // cuộn trang trên điện thoại đều biến thành kéo cột. Tách đôi thì cảm ứng đi qua TouchSensor
  // với ràng buộc GIỮ 200ms, và vuốt để cuộn vẫn là cuộn.
  const sensors = useSensors(
    useSensor(MouseSensor, { activationConstraint: { distance: 8 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 200, tolerance: 8 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const keoXong = (e: DragEndEvent) => {
    const { active, over } = e;
    if (!over || active.id === over.id) return;
    setCot((cu) => {
      const tu = cu.findIndex((x) => x.id === active.id);
      const den = cu.findIndex((x) => x.id === over.id);
      if (tu < 0 || den < 0) return cu;
      return arrayMove(cu, tu, den);
    });
  };

  const luu = useMutation({
    mutationFn: (ds: GradeColumnDto[]) =>
      apiFetch(`/api/offerings/${offeringId}/grade-columns`, {
        method: "PUT",
        body: JSON.stringify({
          // order_index lấy theo VỊ TRÍ hiện tại trong mảng, không lấy c.order_index cũ —
          // đó chính là thứ vừa bị kéo-thả làm cho lỗi thời.
          columns: ds.map((c, i) => ({
            id: c.id, key: c.key, name: c.name,
            weight_percent: c.weight_percent, order_index: i,
            kind: c.kind, n: c.n, treat_missing_as_zero: c.treat_missing_as_zero,
          })),
        }),
      }),

    onMutate: async (ds) => {
      // Chặn mọi refetch đang bay: một phản hồi cũ về sau khi ghi lạc quan sẽ kéo bảng về
      // thứ tự cũ rồi lại nhảy tới — lỗi không tái hiện được trên máy nhanh.
      await qc.cancelQueries({ queryKey: khoaSo });
      const truoc = qc.getQueriesData<GradebookDto>({ queryKey: khoaSo });
      qc.setQueriesData<GradebookDto>({ queryKey: khoaSo }, (cu) =>
        cu ? { ...cu, columns: ds.map((c, i) => ({ ...c, order_index: i })) } : cu,
      );
      return { truoc };
    },

    onError: (e: Error, _ds, ctx) => {
      // Hoàn tác ĐÚNG bản trước đó của từng khoá. Không hoàn tác thì bảng vẫn hiện thứ tự mới
      // dù server đã từ chối, giảng viên tin là đã lưu và chỉ phát hiện khi tải lại trang.
      ctx?.truoc.forEach(([k, d]) => qc.setQueryData(k, d));
      toast.error(e.message);
    },

    onSuccess: () => {
      toast.success("Đã lưu cấu hình cột.");
      setMo(false);
    },

    // Server là nguồn chân lý: nó đánh số lại order_index về 0..n-1 và tính lại điểm tổng
    // theo trọng số mới. Thiếu bước này thì lần kéo sau tính trên chỉ số đã lệch.
    onSettled: () => qc.invalidateQueries({ queryKey: khoaSo }),
  });

  const tong = cot.reduce((t, c) => t + Number(c.weight_percent), 0);

  if (!mo) {
    return (
      <Button size="sm" variant="outline" onClick={() => { setCot(so.columns); setMo(true); }}>
        <Settings2 className="size-4" aria-hidden />
        Cấu hình cột điểm
      </Button>
    );
  }

  return (
    <div className="bg-muted/40 border-border flex w-full flex-col gap-3 rounded-xl border p-3 sm:p-4">
      <p className="text-muted-foreground text-sm">
        Kéo tay cầm để đổi thứ tự cột. Thứ tự chỉ được ghi lại khi bấm “Lưu”.
      </p>

      <DndContext id="keo-cot-formatScore" sensors={sensors} collisionDetection={closestCenter} onDragEnd={keoXong}>
        <SortableContext items={cot.map((c) => c.id)} strategy={verticalListSortingStrategy}>
          <div className="flex flex-col gap-2">
            {cot.map((c) => (
              <HangCot
                key={c.id}
                c={c}
                nguon={nguon}
                onDoi={(moi) =>
                  setCot((cu) => cu.map((x) => (x.id === c.id ? { ...x, ...moi } : x)))
                }
                onXoa={() => setCot((cu) => cu.filter((x) => x.id !== c.id))}
                onXongNguon={onXong}
              />
            ))}
          </div>
        </SortableContext>
      </DndContext>

      <div className="flex flex-wrap items-center gap-3">
        <Button
          size="sm" variant="outline"
          onClick={() =>
            setCot((cu) => [...cu, {
              id: crypto.randomUUID(), key: `cot_${cu.length + 1}`, name: "Cột mới",
              weight_percent: 0, order_index: cu.length, kind: "average",
              n: 0, treat_missing_as_zero: true, sources: [],
            } as GradeColumnDto])
          }
        >
          <Plus className="size-4" aria-hidden />
          Thêm cột
        </Button>

        {/* Tổng trọng số là điều kiện để lưu được, nên nó phải nổi bật ngay cạnh nút Lưu chứ
            không phải một dòng chữ nhỏ ở đâu đó. */}
        <span
          className={`rounded-md px-2 py-1 text-sm font-medium tabular-nums ${
            tong === 100
              ? "bg-ok-soft text-ok"
              : "bg-danger-soft text-danger"
          }`}
        >
          Tổng trọng số: {tong}%{tong !== 100 && " — phải bằng 100%"}
        </span>

        <div className="flex flex-1 justify-end gap-2">
          <Button size="sm" variant="ghost" onClick={() => setMo(false)}>Đóng</Button>
          <Button
            size="sm"
            disabled={luu.isPending || tong !== 100}
            onClick={() => luu.mutate(cot)}
          >
            {luu.isPending ? "Đang lưu…" : "Lưu"}
          </Button>
        </div>
      </div>
    </div>
  );
}

function GanNguon({
  columnId,
  nguon,
  daGan,
  onXong,
}: {
  columnId: string;
  nguon: Nguon[];
  daGan: GradeColumnDto["sources"];
  onXong: () => void;
}) {
  const [mo, setMo] = useState(false);
  const [chon, setChon] = useState<string[]>(daGan.map((s) => `${s.source_type}:${s.source_id}`));

  const luu = useMutation({
    mutationFn: () =>
      apiFetch(`/api/grade-columns/${columnId}/sources`, {
        method: "PUT",
        body: JSON.stringify({
          sources: chon.map((k) => {
            const [type, id] = k.split(":");
            return { source_type: type, source_id: id, weight_within_column: null };
          }),
        }),
      }),
    onSuccess: () => {
      toast.success("Đã gắn nguồn điểm vào cột.");
      setMo(false);
      onXong();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  if (!mo) {
    return (
      <Button size="sm" variant="outline" onClick={() => setMo(true)}>
        Nguồn ({daGan.length})
      </Button>
    );
  }

  return (
    <div className="border-border bg-card flex w-full flex-col gap-2 rounded-lg border p-3">
      <p className="text-muted-foreground text-sm text-pretty">
        Bài tập / đợt cuối môn nào chảy vào cột này. Không gắn nguồn thì cột chỉ nhập tay.
      </p>

      {nguon.length === 0 ? (
        <p className="border-border text-muted-foreground rounded-lg border border-dashed px-3 py-4 text-center text-sm">
          Chưa có bài tập nào để gắn.
        </p>
      ) : (
        <ul className="flex flex-col gap-0.5">
          {nguon.map((n) => {
            const k = `${n.type}:${n.id}`;
            return (
              <li key={k}>
                <label className="hover:bg-accent flex min-h-11 items-center gap-2.5 rounded-lg px-2 text-sm transition-colors">
                  <Checkbox
                    checked={chon.includes(k)}
                    onCheckedChange={(v) =>
                      setChon((cu) => (v === true ? [...cu, k] : cu.filter((x) => x !== k)))
                    }
                  />
                  <span className="min-w-0 flex-1 truncate">{n.title}</span>
                  <Badge variant="outline" className="shrink-0 text-xs font-normal">
                    {n.type === "assignment" ? "Bài tập" : "Cuối môn"}
                  </Badge>
                </label>
              </li>
            );
          })}
        </ul>
      )}

      <div className="flex gap-2">
        <Button size="sm" disabled={luu.isPending} onClick={() => luu.mutate()}>Lưu nguồn</Button>
        <Button size="sm" variant="ghost" onClick={() => setMo(false)}>Hủy</Button>
      </div>
    </div>
  );
}
