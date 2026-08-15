"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { XemMaNguon } from "@/components/submission/xem-ma-nguon";
import { apiFetch } from "@/lib/api/fetcher";
import type {
  MyAssignmentDto, MyAssignmentSubmissionDto, SubmissionFileDto, SubmissionSlotDto,
} from "@/lib/api/types";

export function TrangNopBai({
  baiTap,
  banDau,
}: {
  baiTap: MyAssignmentDto;
  banDau: MyAssignmentSubmissionDto;
}) {
  const qc = useQueryClient();
  const khoa = ["my-submission", baiTap.id] as const;

  const { data } = useQuery({
    queryKey: khoa,
    queryFn: () => apiFetch<MyAssignmentSubmissionDto>(`/api/assignments/${baiTap.id}/my-submission`),
    initialData: banDau,
    // Job giải nén chạy ở nền. Poll trong lúc còn tệp đang xử lý, dừng ngay khi xong —
    // poll mãi là hàng trăm truy vấn thừa mỗi phút khi cả lớp mở trang.
    refetchInterval: (q) =>
      q.state.data?.current?.files.some(
        (f) => f.processing_status === "pending" || f.processing_status === "processing",
      )
        ? 2000
        : false,
  });

  const hienHanh = data.current;
  const daNop = hienHanh?.status !== undefined && hienHanh.status !== "draft";

  const nopChinhThuc = useMutation({
    mutationFn: () =>
      apiFetch(`/api/assignments/${baiTap.id}/submissions/submit`, { method: "POST" }),
    onSuccess: () => {
      toast.success("Đã nộp bài.");
      qc.invalidateQueries({ queryKey: khoa });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const soFileTheoSlot = (slotKey: string) =>
    hienHanh?.files.filter((f) => f.slot_key === slotKey) ?? [];

  const thieuBatBuoc = baiTap.slots
    .filter((s) => s.is_required)
    .filter((s) => soFileTheoSlot(s.slot_key).length === 0);

  return (
    <div className="flex flex-col gap-4">
      {daNop && (
        <div className="border-border rounded-lg border p-3 text-sm">
          Đã nộp lần {hienHanh.attempt_no}
          {hienHanh.submitted_at &&
            ` lúc ${new Date(hienHanh.submitted_at).toLocaleString("vi-VN")}`}
          {hienHanh.is_late && " · nộp trễ"}. Tải tệp mới lên sẽ bắt đầu một lần nộp lại.
        </div>
      )}

      {baiTap.slots.map((slot) => (
        <VungNopSlot
          key={slot.id}
          assignmentId={baiTap.id}
          slot={slot}
          files={soFileTheoSlot(slot.slot_key)}
          submissionId={hienHanh?.id}
          daNop={daNop}
          onXong={() => qc.invalidateQueries({ queryKey: khoa })}
        />
      ))}

      <div className="border-border flex flex-wrap items-center justify-between gap-3 border-t pt-4">
        <span className="text-muted-foreground text-sm">
          {thieuBatBuoc.length === 0
            ? "Đã đủ các phần bắt buộc."
            : `Còn thiếu: ${thieuBatBuoc.map((s) => s.label).join(", ")}`}
        </span>

        <Button
          disabled={thieuBatBuoc.length > 0 || nopChinhThuc.isPending}
          onClick={() => nopChinhThuc.mutate()}
        >
          {nopChinhThuc.isPending ? "Đang nộp…" : daNop ? "Nộp lại" : "Nộp chính thức"}
        </Button>
      </div>

      {data.history.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Lịch sử nộp</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-2 text-sm">
            {data.history.map((h) => (
              <div key={h.id} className="text-muted-foreground flex flex-wrap gap-x-3">
                <span>Lần {h.attempt_no}</span>
                <span>
                  {h.submitted_at
                    ? new Date(h.submitted_at).toLocaleString("vi-VN")
                    : "chưa nộp chính thức"}
                </span>
                <span>{h.files.length} tệp</span>
                {h.is_late && <span>trễ</span>}
              </div>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function VungNopSlot({
  assignmentId,
  slot,
  files,
  submissionId,
  daNop,
  onXong,
}: {
  assignmentId: string;
  slot: SubmissionSlotDto;
  files: SubmissionFileDto[];
  submissionId?: string;
  daNop: boolean;
  onXong: () => void;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [dangTai, setDangTai] = useState(false);

  // Đã nộp chính thức thì slot luôn còn chỗ: tệp mới bắt đầu một LƯỢT NỘP MỚI với các slot
  // trống, chứ không cộng thêm vào lượt cũ. Đếm theo lượt cũ ở đây là chính ngõ cụt đã gặp
  // khi chạy thử — slot max_files = 1 đã có tệp, nút chọn tệp biến mất, và sinh viên nộp
  // nhầm bài thì không còn đường nào sửa.
  const conCho = daNop || files.length < slot.max_files;

  // Đèn trạng thái: xám = chưa nộp · xanh = đã có tệp · đỏ = bắt buộc mà còn trống.
  const mau =
    files.length > 0 ? "bg-green-500" : slot.is_required ? "bg-destructive" : "bg-muted-foreground";

  async function taiLen(file: File) {
    // Kiểm phía client để không tốn băng thông của sinh viên. KHÔNG thay cho kiểm phía
    // server — server mới là thứ bảo vệ hệ thống, và nó kiểm cả chữ ký byte đầu tệp.
    const duoi = `.${file.name.split(".").pop()?.toLowerCase() ?? ""}`;
    if (!slot.accepted_file_types.includes(duoi)) {
      toast.error(`Phần này chỉ nhận ${slot.accepted_file_types.join(", ")}.`);
      return;
    }

    if (file.size > slot.max_file_size_mb * 1024 * 1024) {
      toast.error(`Tệp vượt giới hạn ${slot.max_file_size_mb} MB của phần này.`);
      return;
    }

    const form = new FormData();
    form.append("file", file);
    form.append("slot_key", slot.slot_key);   // snake_case: model binder form không theo JsonNamingPolicy

    setDangTai(true);
    try {
      const res = await fetch(`/api/assignments/${assignmentId}/submissions/draft`, {
        method: "POST",
        body: form,
      });

      if (!res.ok) {
        const problem = await res.json().catch(() => null);
        throw new Error(problem?.detail ?? `Lỗi ${res.status}`);
      }

      toast.success(`Đã tải lên “${file.name}”.`);
      onXong();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Không tải được tệp lên.");
    } finally {
      setDangTai(false);
      if (input.current) input.current.value = "";
    }
  }

  return (
    <Card>
      <CardHeader className="flex flex-row flex-wrap items-center gap-2">
        <span className={`size-2.5 rounded-full ${mau}`} aria-hidden />
        <CardTitle className="text-base">{slot.label}</CardTitle>
        {slot.is_required && <Badge variant="secondary">Bắt buộc</Badge>}
      </CardHeader>

      <CardContent className="flex flex-col gap-3">
        <p className="text-muted-foreground text-sm">
          {slot.accepted_file_types.join(", ")} · tối đa {slot.max_files} tệp ·{" "}
          {slot.max_file_size_mb} MB mỗi tệp
        </p>

        {(slot.required_files.length > 0 || slot.required_folders.length > 0) && (
          <p className="text-muted-foreground text-sm">
            Bên trong tệp nén cần có:{" "}
            {[...slot.required_files, ...slot.required_folders.map((f) => `${f}/`)].join(", ")}
          </p>
        )}

        {files.map((f) => (
          <TepDaNop
            key={f.id}
            file={f}
            submissionId={submissionId}
            xoaDuoc={!daNop}
            onXong={onXong}
          />
        ))}

        {conCho && (
          <div>
            <input
              ref={input}
              type="file"
              className="hidden"
              accept={slot.accepted_file_types.join(",")}
              onChange={(e) => {
                const f = e.target.files?.[0];
                if (f) void taiLen(f);
              }}
            />
            <Button
              size="sm"
              variant="outline"
              disabled={dangTai}
              onClick={() => input.current?.click()}
            >
              {dangTai ? "Đang tải lên…" : daNop ? "Chọn tệp mới (nộp lại)" : "Chọn tệp"}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function TepDaNop({
  file,
  submissionId,
  xoaDuoc,
  onXong,
}: {
  file: SubmissionFileDto;
  submissionId?: string;
  xoaDuoc: boolean;
  onXong: () => void;
}) {
  const [moCay, setMoCay] = useState(false);

  const xoa = useMutation({
    mutationFn: () => apiFetch(`/api/submission-files/${file.id}`, { method: "DELETE" }),
    onSuccess: () => {
      toast.success("Đã xóa tệp.");
      onXong();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <div className="border-border flex flex-col gap-2 rounded-lg border p-3">
      <div className="flex flex-wrap items-center gap-2">
        {/* Tên tệp do sinh viên đặt — render qua text node của React nên đã được thoát tự động. */}
        <span className="text-sm font-medium">{file.file_name}</span>
        <span className="text-muted-foreground text-sm">
          {(file.size_bytes / 1024).toFixed(0)} KB
        </span>

        {file.processing_status === "pending" && <Badge variant="outline">Đang chờ xử lý</Badge>}
        {file.processing_status === "processing" && <Badge variant="outline">Đang giải nén</Badge>}
        {file.processing_status === "failed" && <Badge variant="destructive">Không đọc được</Badge>}

        {file.structure_check_passed === true && <Badge>Cấu trúc đạt</Badge>}
        {file.structure_check_passed === false && (
          <Badge variant="destructive">Thiếu cấu trúc</Badge>
        )}

        {xoaDuoc && (
          <Button
            size="sm"
            variant="ghost"
            className="ml-auto"
            disabled={xoa.isPending}
            onClick={() => xoa.mutate()}
          >
            Xóa
          </Button>
        )}
      </div>

      {file.processing_error && (
        <p className="text-destructive text-sm">{file.processing_error}</p>
      )}

      {file.structure_check_passed === false && file.missing_structure_items.length > 0 && (
        <p className="text-muted-foreground text-sm">
          Thiếu: {file.missing_structure_items.join(", ")}. Bài vẫn nộp được, nhưng giảng viên
          sẽ thấy dấu này.
        </p>
      )}

      {file.processing_status === "done" && submissionId && (
        <div>
          <Button size="sm" variant="ghost" onClick={() => setMoCay((v) => !v)}>
            {moCay ? "Ẩn cây thư mục" : "Xem cây thư mục"}
          </Button>
          {moCay && <CayThuMuc submissionId={submissionId} fileId={file.id} />}
        </div>
      )}
    </div>
  );
}

function CayThuMuc({ submissionId, fileId }: { submissionId: string; fileId: string }) {
  const [dangXem, setDangXem] = useState<string | null>(null);

  const { data, isPending, error } = useQuery({
    queryKey: ["file-tree", submissionId, fileId] as const,
    queryFn: () =>
      apiFetch<{ entries: { path: string; size_bytes: number }[] }>(
        `/api/submissions/${submissionId}/files/${fileId}/file-tree`,
      ),
  });

  if (isPending) return <p className="text-muted-foreground text-sm">Đang tải…</p>;
  if (error) return <p className="text-destructive text-sm">{(error as Error).message}</p>;
  if (data.entries.length === 0)
    return <p className="text-muted-foreground text-sm">Tệp nén rỗng.</p>;

  return (
    <div className="mt-2 flex flex-col gap-2">
      <ul className="flex flex-col gap-0.5 font-mono text-xs">
        {data.entries.map((e) => (
          <li key={e.path}>
            <button
              type="button"
              onClick={() => setDangXem((cu) => (cu === e.path ? null : e.path))}
              className={`hover:bg-muted flex w-full justify-between gap-4 rounded px-1.5 py-0.5 text-left ${
                dangXem === e.path ? "bg-muted" : "text-muted-foreground"
              }`}
            >
              {/* Đường dẫn do sinh viên đặt — text node của React đã thoát tự động. */}
              <span className="truncate">{e.path}</span>
              <span className="shrink-0">{(e.size_bytes / 1024).toFixed(1)} KB</span>
            </button>
          </li>
        ))}
      </ul>

      {dangXem && (
        <XemMaNguon submissionId={submissionId} fileId={fileId} path={dangXem} />
      )}
    </div>
  );
}
