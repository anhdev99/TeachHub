"use client";

import {
  DndContext, KeyboardSensor, MouseSensor, TouchSensor,
  useDraggable, useDroppable, useSensor, useSensors, type DragEndEvent,
} from "@dnd-kit/core";
import { CSS } from "@dnd-kit/utilities";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { apiFetch } from "@/lib/api/fetcher";
import type { EnrollmentDto, GroupDto, GroupMemberDto } from "@/lib/api/types";

/** Vùng thả cho sinh viên chưa thuộc nhóm nào. Không phải uuid nên không đụng id nhóm thật. */
const CHUA_CO_NHOM = "chua-co-nhom";

/** Nhóm trong bản xem trước mang id toàn số 0 vì chưa được ghi xuống DB. */
const ID_TAM = "00000000-0000-0000-0000-000000000000";

type ChuyenNhom = {
  /**
   * Cả bản ghi thành viên, không chỉ id. `onMutate` cần đủ tên và MSSV để dựng thẻ ở nhóm
   * đích; dò ngược trong cache thì hụt đúng ca kéo từ "Chưa có nhóm" ra — người đó không nằm
   * trong nhóm nào để mà dò, và thẻ chỉ xuất hiện sau khi refetch xong.
   */
  nguoi: GroupMemberDto;
  enrollmentId: string;
  tenSinhVien: string;
  tuGroupId: string | null;
  denGroupId: string | null;
  /** Danh sách thành viên MỚI của nhóm nguồn (đã bỏ sinh viên ra). */
  dsNguon: string[];
  /** Danh sách thành viên MỚI của nhóm đích (đã thêm sinh viên vào). */
  dsDich: string[];
  /** Danh sách thành viên CŨ của nhóm nguồn — dùng để đền bù nếu bước hai hỏng. */
  dsNguonCu: string[];
};

function TheSinhVien({
  m,
  groupId,
  khoaKeo,
}: {
  m: GroupMemberDto;
  groupId: string | null;
  khoaKeo: boolean;
}) {
  // id của dnd-kit là enrollment_id THẬT. Dùng chỉ số mảng thì sau mỗi lần chuyển nhóm React
  // tái sử dụng nhầm node và tên sinh viên hiện sai ở ô vừa thả.
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: m.enrollment_id,
    data: { tuGroupId: groupId },
    disabled: khoaKeo,
  });

  return (
    <div
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform) }}
      className={`border-border flex min-h-11 items-center justify-between gap-2 rounded border px-2 py-1.5 text-sm ${
        khoaKeo ? "" : "cursor-grab touch-manipulation active:cursor-grabbing"
      } ${isDragging ? "opacity-40" : ""}`}
      {...attributes}
      {...listeners}
    >
      <span>{m.full_name}</span>
      <span className="text-muted-foreground font-mono text-xs">{m.student_code}</span>
    </div>
  );
}

function VungTha({
  id,
  khoaKeo,
  children,
}: {
  id: string;
  khoaKeo: boolean;
  children: React.ReactNode;
}) {
  const { setNodeRef, isOver } = useDroppable({ id, disabled: khoaKeo });

  return (
    <div
      ref={setNodeRef}
      className={`flex min-h-16 flex-col gap-1 rounded-md p-1 ${
        isOver ? "bg-muted ring-primary ring-2" : ""
      }`}
    >
      {children}
    </div>
  );
}

export function ChiaNhom({
  offeringId,
  banDau,
  sinhVien,
}: {
  offeringId: string;
  banDau: GroupDto[];
  sinhVien: EnrollmentDto[];
}) {
  const qc = useQueryClient();
  const khoa = ["groups", offeringId] as const;

  const { data: nhom } = useQuery({
    queryKey: khoa,
    queryFn: () => apiFetch<GroupDto[]>(`/api/offerings/${offeringId}/groups`),
    initialData: banDau,
  });

  const [soNguoi, setSoNguoi] = useState(4);
  const [seed, setSeed] = useState(2026);
  const [xemTruoc, setXemTruoc] = useState<GroupDto[] | null>(null);

  // Xem TouchSensor/MouseSensor ở so-diem.tsx: PointerSensor gộp chuột với cảm ứng nên mọi
  // cú vuốt để cuộn danh sách nhóm trên điện thoại sẽ thành kéo sinh viên sang nhóm khác.
  const sensors = useSensors(
    useSensor(MouseSensor, { activationConstraint: { distance: 8 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 200, tolerance: 8 } }),
    useSensor(KeyboardSensor),
  );

  const chia = useMutation({
    mutationFn: (preview: boolean) =>
      apiFetch<GroupDto[]>(`/api/offerings/${offeringId}/groups/auto-generate`, {
        method: "POST",
        body: JSON.stringify({ group_size: soNguoi, seed, preview }),
      }),
    onSuccess: (ds, preview) => {
      if (preview) {
        setXemTruoc(ds);
        toast.success("Đây là kết quả xem trước — bấm “Xác nhận” để lưu.");
      } else {
        setXemTruoc(null);
        toast.success("Đã lưu danh sách nhóm.");
        qc.invalidateQueries({ queryKey: khoa });
      }
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const datThanhVien = (groupId: string, ids: string[]) =>
    apiFetch(`/api/groups/${groupId}/members`, {
      method: "PUT",
      body: JSON.stringify({ member_enrollment_ids: ids }),
    });

  const chuyen = useMutation({
    mutationFn: async (v: ChuyenNhom) => {
      // BỎ KHỎI NHÓM NGUỒN TRƯỚC. Gọi ngược lại chắc chắn ăn 409: backend từ chối thêm một
      // sinh viên đang thuộc nhóm khác ("Đã ở nhóm khác: …").
      if (v.tuGroupId) await datThanhVien(v.tuGroupId, v.dsNguon);

      if (!v.denGroupId) return;

      try {
        await datThanhVien(v.denGroupId, v.dsDich);
      } catch (e) {
        // Bước một đã ghi xuống server mà bước hai hỏng ⇒ sinh viên rơi ra ngoài MỌI nhóm.
        // Chỉ hoàn tác cache là chưa đủ: giao diện sẽ hiện đúng nhưng server thì sai. Phải
        // đền bù bằng một lời gọi ngược thật sự.
        if (v.tuGroupId) {
          try {
            await datThanhVien(v.tuGroupId, v.dsNguonCu);
          } catch {
            throw new Error(
              `Không chuyển được ${v.tenSinhVien} sang nhóm mới VÀ không đưa lại được về nhóm cũ. ` +
                "Tải lại trang để xem trạng thái thật trước khi thao tác tiếp.",
            );
          }
        }
        throw e;
      }
    },

    onMutate: async (v) => {
      // Chặn refetch đang bay ghi đè bản lạc quan — nếu không, thẻ sinh viên vừa thả sẽ nhảy
      // về nhóm cũ rồi lại nhảy sang nhóm mới.
      await qc.cancelQueries({ queryKey: khoa });
      const truoc = qc.getQueryData<GroupDto[]>(khoa);

      qc.setQueryData<GroupDto[]>(khoa, (cu) =>
        cu?.map((g) => {
          if (g.id === v.tuGroupId) {
            return { ...g, members: g.members.filter((m) => m.enrollment_id !== v.enrollmentId) };
          }
          if (g.id === v.denGroupId) {
            return { ...g, members: [...g.members, v.nguoi] };
          }
          return g;
        }),
      );

      return { truoc };
    },

    onError: (e: Error, _v, ctx) => {
      if (ctx?.truoc) qc.setQueryData(khoa, ctx.truoc);
      toast.error(e.message);
    },

    // Server là nguồn chân lý — nó mới biết nhóm thật sự còn những ai sau hai lời gọi.
    onSettled: () => qc.invalidateQueries({ queryKey: khoa }),
  });

  const dangXemTruoc = xemTruoc !== null;
  const dangHien = xemTruoc ?? nhom;

  // Kéo-thả CHỈ bật trên danh sách đã lưu. Nhóm trong bản xem trước chưa có id thật, gọi
  // PUT /api/groups/{id}/members với id toàn số 0 chỉ nhận về 404.
  const khoaKeo = dangXemTruoc || chuyen.isPending;

  const daCoNhom = new Set(nhom.flatMap((g) => g.members.map((m) => m.enrollment_id)));
  const chuaCoNhom: GroupMemberDto[] = sinhVien
    .filter((e) => !daCoNhom.has(e.id))
    .map((e) => ({
      enrollment_id: e.id,
      student_code: e.student_code,
      full_name: e.full_name,
    }));

  const keoXong = (e: DragEndEvent) => {
    const { active, over } = e;
    if (!over || khoaKeo) return;

    const enrollmentId = String(active.id);
    const tuGroupId = (active.data.current?.tuGroupId as string | null) ?? null;
    const denGroupId = over.id === CHUA_CO_NHOM ? null : String(over.id);

    if (tuGroupId === denGroupId) return;

    const nguon = tuGroupId ? nhom.find((g) => g.id === tuGroupId) : null;
    const dich = denGroupId ? nhom.find((g) => g.id === denGroupId) : null;

    const nguoi =
      nhom.flatMap((g) => g.members).find((m) => m.enrollment_id === enrollmentId) ??
      chuaCoNhom.find((m) => m.enrollment_id === enrollmentId);

    if (!nguoi) return;

    const dsNguonCu = nguon?.members.map((m) => m.enrollment_id) ?? [];

    chuyen.mutate({
      nguoi,
      enrollmentId,
      tenSinhVien: nguoi.full_name,
      tuGroupId,
      denGroupId,
      dsNguon: dsNguonCu.filter((id) => id !== enrollmentId),
      dsDich: [...(dich?.members.map((m) => m.enrollment_id) ?? []), enrollmentId],
      dsNguonCu,
    });
  };

  return (
    <div className="flex flex-col gap-5">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Chia ngẫu nhiên</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <p className="text-muted-foreground text-sm">
            Lớp có {sinhVien.length} sinh viên. Phần dư được rải sang các nhóm sẵn có — không để
            lại nhóm một người.
          </p>

          <div className="flex flex-wrap items-end gap-3">
            <div className="flex flex-col gap-1">
              <Label htmlFor="group_size">Số người mỗi nhóm</Label>
              <Input
                id="group_size" type="number" min={2} max={20} className="w-28"
                value={soNguoi} onChange={(e) => setSoNguoi(Number(e.target.value))}
              />
            </div>

            <div className="flex flex-col gap-1">
              <Label htmlFor="seed">Seed</Label>
              {/* Cùng seed cho cùng kết quả: giảng viên xem trước rồi mới xác nhận, và danh
                  sách không được đổi giữa hai lần đó. */}
              <Input
                id="seed" type="number" className="w-32"
                value={seed} onChange={(e) => setSeed(Number(e.target.value))}
              />
            </div>

            <Button variant="outline" disabled={chia.isPending} onClick={() => chia.mutate(true)}>
              Xem trước
            </Button>

            <Button disabled={chia.isPending} onClick={() => chia.mutate(false)}>
              {chia.isPending ? "Đang chia…" : "Xác nhận & lưu"}
            </Button>
          </div>

          {dangXemTruoc && (
            <p className="text-sm">
              Đang hiển thị <strong>bản xem trước</strong>, chưa lưu. Bấm “Xác nhận & lưu” để
              ghi lại đúng cách chia này. Kéo-thả tạm khoá cho tới khi bản này được lưu.
            </p>
          )}
        </CardContent>
      </Card>

      {dangHien.length === 0 && chuaCoNhom.length === 0 ? (
        <p className="text-muted-foreground text-sm">Chưa chia nhóm.</p>
      ) : (
        <DndContext sensors={sensors} onDragEnd={keoXong}>
          {!dangXemTruoc && (
            <p className="text-muted-foreground text-sm">
              Kéo tên sinh viên sang nhóm khác để chuyển. Trên điện thoại: chạm giữ khoảng một
              nhịp rồi kéo.
            </p>
          )}

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            {dangHien.map((g, i) => (
              <Card key={g.id === ID_TAM ? `tam-${i}` : g.id}>
                <CardHeader className="flex flex-row items-center justify-between gap-2">
                  <CardTitle className="text-base">{g.name}</CardTitle>
                  <Badge variant="secondary">{g.members.length} người</Badge>
                </CardHeader>
                <CardContent>
                  <VungTha id={g.id} khoaKeo={khoaKeo}>
                    {g.members.length === 0 && (
                      <p className="text-muted-foreground p-1 text-sm">Kéo sinh viên vào đây.</p>
                    )}
                    {g.members.map((m) => (
                      <TheSinhVien
                        key={m.enrollment_id}
                        m={m}
                        groupId={g.id}
                        khoaKeo={khoaKeo}
                      />
                    ))}
                  </VungTha>
                </CardContent>
              </Card>
            ))}
          </div>

          {!dangXemTruoc && (
            <Card className="mt-3">
              <CardHeader className="flex flex-row items-center justify-between gap-2">
                <CardTitle className="text-base">Chưa có nhóm</CardTitle>
                <Badge variant="outline">{chuaCoNhom.length} người</Badge>
              </CardHeader>
              <CardContent>
                <VungTha id={CHUA_CO_NHOM} khoaKeo={khoaKeo}>
                  {chuaCoNhom.length === 0 && (
                    <p className="text-muted-foreground p-1 text-sm">
                      Mọi sinh viên đều đã có nhóm.
                    </p>
                  )}
                  <div className="grid grid-cols-1 gap-1 sm:grid-cols-2">
                    {chuaCoNhom.map((m) => (
                      <TheSinhVien
                        key={m.enrollment_id}
                        m={m}
                        groupId={null}
                        khoaKeo={khoaKeo}
                      />
                    ))}
                  </div>
                </VungTha>
              </CardContent>
            </Card>
          )}
        </DndContext>
      )}
    </div>
  );
}
