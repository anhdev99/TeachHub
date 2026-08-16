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
import { useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { apiFetch } from "@/lib/api/fetcher";
import {
  NHAN_QUY_TAC_GOP, type AggregationKind, type GradebookDto, type GradeColumnDto,
} from "@/lib/api/types";
import { CapDiemCong } from "./cap-diem-cong";

type Nguon = { type: "assignment" | "final_assessment"; id: string; title: string };

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
      <div className="border-border flex flex-col items-start gap-3 rounded-lg border p-6">
        <p className="text-sm">
          Lớp này chưa có cột điểm nào. Tạo bộ cột mặc định để bắt đầu — sửa lại trọng số bất
          cứ lúc nào.
        </p>
        <Button disabled={khoiTao.isPending} onClick={() => khoiTao.mutate()}>
          {khoiTao.isPending ? "Đang tạo…" : "Tạo bộ cột mặc định"}
        </Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <label className="flex items-center gap-2 text-sm">
          <Switch checked={tamTinh} onCheckedChange={setTamTinh} />
          Xem điểm tạm tính (bỏ qua bài chưa chấm)
        </label>

        {/* Endpoint xuất mặc định trả .xlsx; CSV phải hỏi bằng ?format=csv. Nhãn phải khớp
            định dạng thật — ghi "CSV" rồi tải về một tệp .xlsx là giảng viên mở bằng công cụ
            sai rồi tưởng tệp hỏng. */}
        <a
          href={`/api/offerings/${offeringId}/gradebook/export`}
          className="text-sm underline"
        >
          Xuất bảng điểm (Excel)
        </a>

        <a
          href={`/api/offerings/${offeringId}/gradebook/export?format=csv`}
          className="text-muted-foreground hover:text-foreground text-sm underline"
        >
          Xuất CSV
        </a>

        <CauHinhCot offeringId={offeringId} so={so} nguon={nguon} onXong={lamMoi} />
      </div>

      {!so.weights_valid && (
        <p className="text-destructive text-sm">{so.weights_error}</p>
      )}

      {so.unknown_condition_column_keys.length > 0 && (
        <p className="text-destructive text-sm">
          Điều kiện điểm sàn trỏ tới cột không tồn tại:{" "}
          {so.unknown_condition_column_keys.join(", ")} — điều kiện đó đang KHÔNG có hiệu lực.
        </p>
      )}

      {/* Bảng rộng cuộn trong khối riêng; cột MSSV và Họ tên ghim lại, nếu không thì cuộn
          tới cột "Cuối môn" là không còn biết đang nhập điểm cho ai. */}
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr className="border-border border-b">
              <th className="bg-background sticky left-0 z-10 px-2 py-2 text-left">MSSV</th>
              <th className="bg-background sticky left-24 z-10 px-2 py-2 text-left">Họ tên</th>
              {so.columns.map((c) => (
                <th key={c.id} className="px-2 py-2 text-left whitespace-nowrap">
                  {c.name}
                  <span className="text-muted-foreground block text-xs font-normal">
                    {c.weight_percent}% · {NHAN_QUY_TAC_GOP[c.kind]}
                  </span>
                </th>
              ))}
              <th className="px-2 py-2 text-left">Điểm cộng</th>
              <th className="px-2 py-2 text-left">Tổng</th>
              <th className="px-2 py-2" />
            </tr>
          </thead>

          <tbody>
            {so.rows.map((h) => (
              <tr key={h.enrollment_id} className="border-border border-b">
                <td className="bg-background sticky left-0 z-10 px-2 py-1.5 font-mono text-xs">
                  {h.student_code}
                </td>
                <td className="bg-background sticky left-24 z-10 px-2 py-1.5 whitespace-nowrap">
                  {h.full_name}
                </td>

                {so.columns.map((c) => {
                  const o = h.cells.find((x) => x.column_id === c.id);

                  return (
                    <td key={c.id} className="px-2 py-1.5">
                      <div className="flex items-center gap-1">
                        <Input
                          type="number" step="0.25" min={0} max={10}
                          defaultValue={o?.score ?? ""}
                          className={`h-8 w-20 ${o?.is_manual_override ? "border-primary" : ""}`}
                          onBlur={(e) => {
                            const v = e.target.value === "" ? null : Number(e.target.value);
                            if (v === (o?.score ?? null)) return;
                            suaO.mutate({
                              columnId: c.id, enrollmentId: h.enrollment_id, score: v,
                            });
                          }}
                        />
                        {(o?.bonus ?? 0) > 0 && (
                          <span className="text-xs text-green-600">+{o!.bonus}</span>
                        )}
                      </div>
                    </td>
                  );
                })}

                <td className="px-2 py-1.5 tabular-nums">
                  {h.bonus_to_final > 0 ? `+${h.bonus_to_final}` : "—"}
                </td>
                <td className="px-2 py-1.5 font-medium tabular-nums">{h.final_score}</td>
                <td className="px-2 py-1.5">
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
      </div>

      <p className="text-muted-foreground text-sm">
        Ô viền đậm là điểm đã sửa tay — chấm lại bài trong cột sẽ không ghi đè lên nó. Xóa
        trắng ô để trả về cho quy tắc tổng hợp tự tính.
      </p>
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
      className={`flex flex-wrap items-end gap-2 ${isDragging ? "opacity-50" : ""}`}
    >
      {/* Tay cầm riêng thay vì kéo cả hàng: cả hàng đều là input, kéo được cả hàng thì mọi
          thao tác bôi đen chữ trong ô đều biến thành kéo cột.
          touch-none = touch-action:none — thiếu nó thì trên điện thoại trình duyệt nuốt mất
          cử chỉ để cuộn trang và tay cầm không kéo được. */}
      <button
        type="button"
        className="text-muted-foreground hover:text-foreground h-11 w-11 shrink-0 cursor-grab touch-none text-lg active:cursor-grabbing"
        aria-label={`Kéo để đổi thứ tự cột ${c.name}`}
        {...attributes}
        {...listeners}
      >
        ⠿
      </button>

      <div className="flex flex-col gap-1">
        <Label>Tên cột</Label>
        <Input
          className="w-40" value={c.name}
          onChange={(e) => onDoi({ name: e.target.value })}
        />
      </div>

      <div className="flex flex-col gap-1">
        <Label>Khóa</Label>
        <Input className="w-32 font-mono text-xs" value={c.key} readOnly />
      </div>

      <div className="flex flex-col gap-1">
        <Label>Trọng số %</Label>
        <Input
          type="number" min={0} max={100} className="w-24" value={c.weight_percent}
          onChange={(e) => onDoi({ weight_percent: Number(e.target.value) })}
        />
      </div>

      <div className="flex flex-col gap-1">
        <Label>Quy tắc gộp</Label>
        <Select value={c.kind} onValueChange={(v) => onDoi({ kind: v as AggregationKind })}>
          <SelectTrigger className="w-48"><SelectValue /></SelectTrigger>
          <SelectContent>
            {Object.entries(NHAN_QUY_TAC_GOP).map(([k, nhan]) => (
              <SelectItem key={k} value={k}>{nhan}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {(c.kind === "drop_lowest_n" || c.kind === "best_n_of_m") && (
        <div className="flex flex-col gap-1">
          <Label>N</Label>
          <Input
            type="number" min={0} className="w-20" value={c.n}
            onChange={(e) => onDoi({ n: Number(e.target.value) })}
          />
        </div>
      )}

      <GanNguon columnId={c.id} nguon={nguon} daGan={c.sources} onXong={onXongNguon} />

      <Button size="sm" variant="ghost" onClick={onXoa}>
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
        Cấu hình cột điểm
      </Button>
    );
  }

  return (
    <div className="border-border flex w-full flex-col gap-3 rounded-lg border p-4">
      <p className="text-muted-foreground text-sm">
        Kéo tay cầm ⠿ để đổi thứ tự cột. Thứ tự chỉ được ghi lại khi bấm “Lưu”.
      </p>

      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={keoXong}>
        <SortableContext items={cot.map((c) => c.id)} strategy={verticalListSortingStrategy}>
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
          + Thêm cột
        </Button>

        <span className={`text-sm ${tong === 100 ? "text-muted-foreground" : "text-destructive"}`}>
          Tổng trọng số: {tong}%{tong !== 100 && " — phải bằng 100%"}
        </span>

        <Button
          size="sm"
          disabled={luu.isPending || tong !== 100}
          onClick={() => luu.mutate(cot)}
        >
          {luu.isPending ? "Đang lưu…" : "Lưu"}
        </Button>
        <Button size="sm" variant="ghost" onClick={() => setMo(false)}>Đóng</Button>
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
    <div className="border-border flex w-full flex-col gap-2 rounded border p-3">
      <p className="text-muted-foreground text-sm">
        Bài tập / đợt cuối môn nào chảy vào cột này. Không gắn nguồn thì cột chỉ nhập tay.
      </p>

      {nguon.length === 0 && (
        <p className="text-muted-foreground text-sm">Chưa có bài tập nào để gắn.</p>
      )}

      {nguon.map((n) => {
        const k = `${n.type}:${n.id}`;
        return (
          <label key={k} className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={chon.includes(k)}
              onChange={(e) =>
                setChon((cu) => (e.target.checked ? [...cu, k] : cu.filter((x) => x !== k)))
              }
            />
            {n.title}
            <Badge variant="outline" className="text-xs">
              {n.type === "assignment" ? "Bài tập" : "Cuối môn"}
            </Badge>
          </label>
        );
      })}

      <div className="flex gap-2">
        <Button size="sm" disabled={luu.isPending} onClick={() => luu.mutate()}>Lưu nguồn</Button>
        <Button size="sm" variant="ghost" onClick={() => setMo(false)}>Hủy</Button>
      </div>
    </div>
  );
}
