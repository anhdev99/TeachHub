"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CalendarClock,
  Check,
  ChevronDown,
  Circle,
  CircleAlert,
  CircleCheck,
  FileText,
  FolderTree,
  History,
  Trash2,
  Upload,
} from "lucide-react";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { EmptyState } from "@/components/shell/page-shell";
import { SourceViewer } from "@/components/submission/source-viewer";
import { apiFetch } from "@/lib/api/fetcher";
import { formatScore, formatDateTime } from "@/lib/format";
import type {
  MyAssignmentDto, MyAssignmentSubmissionDto, SubmissionDto, SubmissionFileDto,
  SubmissionSlotDto,
} from "@/lib/api/types";
import { cn } from "@/lib/utils";

/** `1,4 MB` · `312 KB` — số tròn, đọc lướt được, không phải `1433,6 KB`. */
function coTep(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1).replace(".", ",")} MB`;
}

export function TrangNopBai({
  baiTap,
  banDau,
  quaHan,
}: {
  baiTap: MyAssignmentDto;
  banDau: MyAssignmentSubmissionDto;
  /**
   * Đã quá hạn hay chưa, tính SẴN ở máy chủ.
   *
   * Đọc đồng hồ ngay trong thân component client thì máy chủ và trình duyệt ra hai giá trị
   * khác nhau → React báo lệch hydrate và khối trạng thái đổi màu ngay trước mắt người dùng.
   * Đẩy sang effect thì hết lệch nhưng lại là một vòng render thừa cho một con số không đổi
   * trong suốt phiên. Máy chủ đã render trang này với `cache: "no-store"`, nên mốc thời gian
   * của nó luôn mới.
   */
  quaHan: boolean;
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

  const soTepNhap = hienHanh?.files.length ?? 0;

  return (
    <div className="flex flex-col gap-4">
      <TheTrangThai
        baiTap={baiTap}
        hienHanh={hienHanh}
        daNop={daNop}
        soTepNhap={soTepNhap}
        quaHan={quaHan}
      />

      {/* Bài tập không có phần nộp nào là ca THẬT trong dữ liệu hiện tại. Không nói gì thì
          màn hình chỉ còn nút "Nộp chính thức" trơ trọi, và sinh viên đi tìm chỗ đính kèm
          tệp cho tới lúc bỏ cuộc. */}
      {baiTap.slots.length === 0 && (
        <EmptyState
          Icon={Upload}
          title="Bài tập này chưa có phần nộp nào"
          description="Giảng viên chưa cấu hình chỗ để đính kèm tệp. Hỏi lại giảng viên trước khi bấm Nộp chính thức."
        />
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

      {/* Thanh hành động DÍNH ĐÁY: sinh viên hay nộp vào phút chót trên điện thoại, và với
          bài có ba phần nộp thì nút "Nộp chính thức" nằm dưới hai màn hình cuộn. Dính đáy là
          nút luôn ở trong tầm ngón cái, không phải phần thưởng cho việc cuộn đúng chỗ. */}
      <div className="bg-card/95 border-border sticky bottom-0 z-20 flex flex-col gap-2.5 rounded-xl border p-3 backdrop-blur sm:flex-row sm:items-center sm:justify-between">
        <span
          className={cn(
            "flex items-start gap-2 text-sm text-pretty",
            thieuBatBuoc.length === 0 ? "text-muted-foreground" : "text-warn",
          )}
        >
          {thieuBatBuoc.length === 0 ? (
            <Check className="mt-0.5 size-4 shrink-0" aria-hidden />
          ) : (
            <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
          )}
          {thieuBatBuoc.length === 0
            ? "Đã đủ các phần bắt buộc."
            : `Còn thiếu: ${thieuBatBuoc.map((s) => s.label).join(", ")}`}
        </span>

        <Button
          disabled={thieuBatBuoc.length > 0 || nopChinhThuc.isPending}
          onClick={() => nopChinhThuc.mutate()}
          className="min-h-11 w-full sm:w-fit"
        >
          {nopChinhThuc.isPending ? "Đang nộp…" : daNop ? "Nộp lại" : "Nộp chính thức"}
        </Button>
      </div>

      {data.history.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <History className="size-4 shrink-0" aria-hidden />
              Lịch sử nộp
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-1">
            {data.history.map((h) => (
              <div
                key={h.id}
                className="text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-dashed py-2 text-sm last:border-0"
              >
                <span className="text-foreground font-medium">Lần {h.attempt_no}</span>
                {h.submitted_at ? (
                  <time dateTime={h.submitted_at}>{formatDateTime(h.submitted_at)}</time>
                ) : (
                  <span>chưa nộp chính thức</span>
                )}
                <span>{h.files.length} tệp</span>
                {h.is_late && (
                  <Badge className="bg-danger-soft text-danger border-danger/25 border">Trễ</Badge>
                )}
              </div>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

/**
 * Thẻ trạng thái — thứ ĐẦU TIÊN phải đọc được trên màn hình này.
 *
 * Bốn câu hỏi sinh viên mang tới đây: đã nộp chưa, nộp lúc nào, có trễ không, còn nộp lại
 * được không. Bản cũ trả lời cả bốn bằng một dòng chữ xám cỡ 14px, và câu nguy hiểm nhất —
 * "tệp đã tải lên nhưng CHƯA nộp" — thì không trả lời chút nào.
 */
function TheTrangThai({
  baiTap,
  hienHanh,
  daNop,
  soTepNhap,
  quaHan,
}: {
  baiTap: MyAssignmentDto;
  hienHanh: SubmissionDto | null | undefined;
  daNop: boolean;
  soTepNhap: number;
  quaHan: boolean;
}) {
  const treHan = hienHanh?.is_late === true;
  const sac = daNop ? (treHan ? "warn" : "ok") : quaHan ? "danger" : "warn";

  const Icon = daNop ? CircleCheck : CircleAlert;

  return (
    <div
      className={cn(
        "flex flex-col gap-3 rounded-xl border p-3 sm:p-4",
        sac === "ok" && "bg-ok-soft border-ok/25",
        sac === "warn" && "bg-warn-soft border-warn/25",
        sac === "danger" && "bg-danger-soft border-danger/25",
      )}
    >
      <div className="flex items-start gap-2.5">
        <Icon
          className={cn(
            "mt-0.5 size-5 shrink-0",
            sac === "ok" && "text-ok",
            sac === "warn" && "text-warn",
            sac === "danger" && "text-danger",
          )}
          aria-hidden
        />

        <div className="flex min-w-0 flex-col gap-1">
          <p
            className={cn(
              "font-semibold text-pretty",
              sac === "ok" && "text-ok",
              sac === "warn" && "text-warn",
              sac === "danger" && "text-danger",
            )}
          >
            {daNop
              ? `Đã nộp lần ${hienHanh?.attempt_no}${treHan ? " — nộp trễ" : ""}`
              : soTepNhap > 0
                ? "Chưa nộp — tệp mới chỉ nằm ở bản nháp"
                : "Bạn chưa nộp bài này"}
          </p>

          <p className="text-foreground/80 text-sm text-pretty">
            {daNop ? (
              <>
                {hienHanh?.submitted_at && (
                  <>
                    Lúc{" "}
                    <time dateTime={hienHanh.submitted_at}>{formatDateTime(hienHanh.submitted_at)}</time>.{" "}
                  </>
                )}
                Tải tệp mới lên sẽ bắt đầu lần nộp thứ {(hienHanh?.attempt_no ?? 1) + 1}.
              </>
            ) : soTepNhap > 0 ? (
              <>
                Bạn đã tải lên {soTepNhap} tệp nhưng <strong>chưa bấm Nộp chính thức</strong> —
                giảng viên chưa nhìn thấy bài của bạn.
              </>
            ) : (
              "Chọn tệp cho từng phần bên dưới, rồi bấm Nộp chính thức."
            )}
          </p>
        </div>
      </div>

      <div className="text-foreground/80 flex flex-wrap items-center gap-x-4 gap-y-1.5 text-sm">
        <span className="inline-flex items-center gap-1.5">
          <CalendarClock className="size-3.5 shrink-0" aria-hidden />
          {baiTap.due_at ? (
            <>
              Hạn nộp <time dateTime={baiTap.due_at}>{formatDateTime(baiTap.due_at)}</time>
            </>
          ) : (
            "Không có hạn nộp"
          )}
        </span>

        {quaHan && (
          <span className="font-medium">
            {baiTap.allow_late_submission ? "Đã quá hạn · vẫn nhận nộp trễ" : "Đã quá hạn · đã đóng"}
          </span>
        )}

        <span className="tabular-nums">Điểm tối đa {formatScore(baiTap.max_score)}</span>

        {baiTap.score != null && (
          <span className="font-semibold tabular-nums">
            Điểm của bạn {formatScore(baiTap.score)}/{formatScore(baiTap.max_score)}
          </span>
        )}
      </div>
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
  const [dangKeo, setDangKeo] = useState(false);

  // Đã nộp chính thức thì slot luôn còn chỗ: tệp mới bắt đầu một LƯỢT NỘP MỚI với các slot
  // trống, chứ không cộng thêm vào lượt cũ. Đếm theo lượt cũ ở đây là chính ngõ cụt đã gặp
  // khi chạy thử — slot max_files = 1 đã có tệp, nút chọn tệp biến mất, và sinh viên nộp
  // nhầm bài thì không còn đường nào sửa.
  const conCho = daNop || files.length < slot.max_files;

  const daCoTep = files.length > 0;
  const thieu = !daCoTep && slot.is_required;

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
        {/* Icon có nhãn thay cho chấm tròn màu: chấm màu một mình không nói được nghĩa, và
            người mù màu đọc ba chấm xanh/đỏ/xám y hệt nhau. */}
        {daCoTep ? (
          <CircleCheck className="text-ok size-4 shrink-0" aria-label="Đã có tệp" />
        ) : thieu ? (
          <CircleAlert className="text-danger size-4 shrink-0" aria-label="Bắt buộc, còn trống" />
        ) : (
          <Circle className="text-muted-foreground/50 size-4 shrink-0" aria-label="Còn trống" />
        )}

        <CardTitle className="min-w-0 flex-1">{slot.label}</CardTitle>

        {slot.is_required && (
          <Badge
            className={cn(
              "border",
              thieu
                ? "bg-danger-soft text-danger border-danger/25"
                : "bg-ok-soft text-ok border-ok/25",
            )}
          >
            Bắt buộc
          </Badge>
        )}
      </CardHeader>

      <CardContent className="flex flex-col gap-3">
        <p className="text-muted-foreground text-sm text-pretty">
          {slot.accepted_file_types.join(", ")} · tối đa {slot.max_files} tệp ·{" "}
          {slot.max_file_size_mb} MB mỗi tệp
        </p>

        {(slot.required_files.length > 0 || slot.required_folders.length > 0) && (
          <p className="text-muted-foreground text-sm text-pretty">
            Bên trong tệp nén cần có:{" "}
            <span className="font-mono text-xs">
              {[...slot.required_files, ...slot.required_folders.map((f) => `${f}/`)].join(", ")}
            </span>
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
          <div
            onDragOver={(e) => {
              e.preventDefault();
              setDangKeo(true);
            }}
            onDragLeave={() => setDangKeo(false)}
            onDrop={(e) => {
              e.preventDefault();
              setDangKeo(false);
              const f = e.dataTransfer.files?.[0];
              if (f) void taiLen(f);
            }}
            className={cn(
              "rounded-xl border-2 border-dashed transition-colors",
              dangKeo ? "border-primary bg-accent" : "border-border",
            )}
          >
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
            {/* CẢ Ô là vùng bấm, không phải một nút cao 28px ở góc: trên điện thoại đây là
                thao tác chính của màn hình và nó phải bắt được cả ngón cái. */}
            <button
              type="button"
              disabled={dangTai}
              onClick={() => input.current?.click()}
              className="hover:bg-muted/60 flex min-h-24 w-full flex-col items-center justify-center gap-1 rounded-[calc(var(--radius-xl)-2px)] px-4 py-5 text-center transition-colors disabled:opacity-60"
            >
              <Upload className="text-muted-foreground size-5" aria-hidden />
              <span className="text-sm font-medium">
                {dangTai ? "Đang tải lên…" : daNop ? "Chọn tệp mới (nộp lại)" : "Chọn tệp"}
              </span>
              <span className="text-muted-foreground text-xs">
                Chạm để chọn, hoặc kéo tệp thả vào đây
              </span>
            </button>
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
    <div className="border-border flex flex-col gap-2 rounded-xl border p-3">
      <div className="flex items-start gap-2">
        <FileText className="text-muted-foreground mt-0.5 size-4 shrink-0" aria-hidden />

        <div className="flex min-w-0 flex-1 flex-col gap-1">
          {/* Tên tệp do sinh viên đặt — render qua text node của React nên đã được thoát tự động. */}
          <span className="text-sm font-medium break-all">{file.file_name}</span>

          <div className="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
            <span className="tabular-nums">{coTep(file.size_bytes)}</span>
            <span aria-hidden>·</span>
            <time dateTime={file.uploaded_at}>{formatDateTime(file.uploaded_at)}</time>
          </div>

          <div className="flex flex-wrap items-center gap-1.5">
            {file.processing_status === "pending" && <Badge variant="outline">Đang chờ xử lý</Badge>}
            {file.processing_status === "processing" && <Badge variant="outline">Đang giải nén</Badge>}
            {file.processing_status === "failed" && (
              <Badge className="bg-danger-soft text-danger border-danger/25 border">
                Không đọc được
              </Badge>
            )}

            {file.structure_check_passed === true && (
              <Badge className="bg-ok-soft text-ok border-ok/25 border">Cấu trúc đạt</Badge>
            )}
            {file.structure_check_passed === false && (
              <Badge className="bg-warn-soft text-warn border-warn/25 border">Thiếu cấu trúc</Badge>
            )}
          </div>
        </div>

        {xoaDuoc && (
          <Button
            variant="ghost"
            size="icon-lg"
            aria-label={`Xóa tệp ${file.file_name}`}
            title="Xóa tệp"
            disabled={xoa.isPending}
            onClick={() => xoa.mutate()}
            className="text-muted-foreground hover:text-danger min-h-11 shrink-0"
          >
            <Trash2 aria-hidden />
          </Button>
        )}
      </div>

      {file.processing_error && (
        <p className="text-danger text-sm text-pretty">{file.processing_error}</p>
      )}

      {file.structure_check_passed === false && file.missing_structure_items.length > 0 && (
        <p className="text-muted-foreground text-sm text-pretty">
          Thiếu: <span className="font-mono text-xs">{file.missing_structure_items.join(", ")}</span>.
          Bài vẫn nộp được, nhưng giảng viên sẽ thấy dấu này.
        </p>
      )}

      {file.processing_status === "done" && submissionId && (
        <div>
          <Button
            variant="ghost"
            onClick={() => setMoCay((v) => !v)}
            aria-expanded={moCay}
            className="text-muted-foreground min-h-11 -ml-2"
          >
            <FolderTree aria-hidden />
            {moCay ? "Ẩn cây thư mục" : "Xem cây thư mục"}
            <ChevronDown
              className={cn("transition-transform", moCay && "rotate-180")}
              aria-hidden
            />
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
  if (error) return <p className="text-danger text-sm">{(error as Error).message}</p>;
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
              className={cn(
                "hover:bg-muted flex w-full items-center justify-between gap-3 rounded px-1.5 py-1.5 text-left transition-colors",
                dangXem === e.path ? "bg-muted" : "text-muted-foreground",
              )}
            >
              {/* Đường dẫn do sinh viên đặt — text node của React đã thoát tự động. */}
              <span className="truncate">{e.path}</span>
              <span className="shrink-0 tabular-nums">{coTep(e.size_bytes)}</span>
            </button>
          </li>
        ))}
      </ul>

      {dangXem && (
        <SourceViewer submissionId={submissionId} fileId={fileId} path={dangXem} />
      )}
    </div>
  );
}
