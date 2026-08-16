"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FileCode, FolderTree, Inbox, Users } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { HorizontalScroll, EmptyState } from "@/components/shell/page-shell";
import { SourceViewer } from "@/components/submission/source-viewer";
import { apiFetch } from "@/lib/api/fetcher";
import { formatScore, formatRelative, formatDateTime } from "@/lib/format";
import {
  SUBMISSION_STATUS_LABELS,
  type GroupDto, type RubricDto, type SubmissionDto,
  type SubmissionFileDto, type SubmissionStatus,
} from "@/lib/api/types";

/**
 * Màu của nhãn trạng thái bài nộp.
 *
 * "Đã chấm" và "Đã trả bài" là việc đã xong (xanh lá); "Đã nộp"/"Đang chấm" là việc còn nằm
 * trên bàn (hổ phách). Nhớ màu bằng đầu ở từng chỗ dùng là cách để cùng một trạng thái hiện
 * hai màu khác nhau ở hai màn hình.
 */
const MAU_TRANG_THAI: Record<SubmissionStatus, string> = {
  draft: "bg-muted text-muted-foreground border-border",
  submitted: "bg-warn-soft text-warn border-warn/25",
  grading: "bg-warn-soft text-warn border-warn/25",
  graded: "bg-ok-soft text-ok border-ok/25",
  returned: "bg-ok-soft text-ok border-ok/25",
};

export function TrangChamBai({
  banDau,
  rubrics,
  nhom,
}: {
  banDau: SubmissionDto;
  rubrics: RubricDto[];
  nhom?: GroupDto;
}) {
  const qc = useQueryClient();
  const khoa = ["submission", banDau.id] as const;

  const { data: bai } = useQuery({
    queryKey: khoa,
    queryFn: () => apiFetch<SubmissionDto>(`/api/submissions/${banDau.id}`),
    initialData: banDau,
  });

  const [slotDangXem, setSlotDangXem] = useState<string | null>(
    bai.files[0]?.slot_key ?? null,
  );
  const [rubricId, setRubricId] = useState<string | null>(rubrics[0]?.id ?? null);
  const [diemTieuChi, setDiemTieuChi] = useState<Record<string, string>>({});
  const [nhanXet, setNhanXet] = useState(bai.feedback ?? "");

  const rubric = rubrics.find((r) => r.id === rubricId);

  // Nạp lại điểm đã chấm trước đó để "chấm lại" không phải gõ lại từ đầu.
  //
  // Chỉnh state ngay trong lúc render thay vì trong `useEffect` — khuôn React khuyến nghị
  // cho ca "đổi state khi dữ liệu đầu vào đổi". Bằng effect thì lượt vẽ ĐẦU TIÊN hiện bảng
  // điểm trống rồi mới nhảy sang điểm cũ; giảng viên mở một bài đã chấm sẽ thấy một nháy
  // trắng và, nếu gõ đúng vào khoảnh khắc đó, con số vừa gõ bị bản nạp lại ghi đè.
  const [diemCu, setDiemCu] = useState(bai.rubric_scores);
  if (bai.rubric_scores !== diemCu) {
    setDiemCu(bai.rubric_scores);
    const cu = bai.rubric_scores ?? {};
    if (Object.keys(cu).length > 0) {
      setDiemTieuChi(Object.fromEntries(Object.entries(cu).map(([k, v]) => [k, String(v)])));
    }
  }

  const tong = useMemo(() => {
    if (!rubric) return 0;
    return rubric.criteria.reduce((t, c) => {
      const v = Number(diemTieuChi[c.id] ?? "");
      return t + (Number.isFinite(v) ? v : 0);
    }, 0);
  }, [rubric, diemTieuChi]);

  const vuotTran = rubric?.criteria.some((c) => Number(diemTieuChi[c.id] ?? 0) > c.max_points) ?? false;

  const cham = useMutation({
    mutationFn: () =>
      apiFetch(`/api/submissions/${bai.id}/grade`, {
        method: "POST",
        body: JSON.stringify({
          rubric_scores: Object.fromEntries(
            Object.entries(diemTieuChi)
              .filter(([, v]) => v !== "")
              .map(([k, v]) => [k, Number(v)]),
          ),
          direct_score: null,
          feedback: nhanXet || null,
        }),
      }),
    onSuccess: () => {
      toast.success("Đã lưu điểm.");
      qc.invalidateQueries({ queryKey: khoa });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const traBai = useMutation({
    mutationFn: () => apiFetch(`/api/submissions/${bai.id}/return`, { method: "POST" }),
    onSuccess: () => {
      toast.success("Đã trả bài — sinh viên xem được điểm và nhận xét.");
      qc.invalidateQueries({ queryKey: khoa });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  // Chấm 40 bài liên tiếp mà phải rê chuột từng ô là công việc bào mòn ý chí — và là lý do
  // người ta quay về Excel. Ctrl+Enter lưu ngay, không rời bàn phím.
  useEffect(() => {
    function phim(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key === "Enter") {
        e.preventDefault();
        if (!cham.isPending && !vuotTran) cham.mutate();
      }
    }
    window.addEventListener("keydown", phim);
    return () => window.removeEventListener("keydown", phim);
  }, [cham, vuotTran]);

  const slotKeys = [...new Set(bai.files.map((f) => f.slot_key))];
  const fileCuaSlot = bai.files.filter((f) => f.slot_key === slotDangXem);

  return (
    <div className="flex flex-col gap-4">
      {/* ── Bài này là bài nào, nộp lúc nào, đang ở trạng thái gì ────────────────────── */}
      <div className="border-border bg-card flex flex-wrap items-center gap-x-3 gap-y-2 rounded-lg border px-3.5 py-3 text-sm">
        <Badge className={`border ${MAU_TRANG_THAI[bai.status]}`}>
          {SUBMISSION_STATUS_LABELS[bai.status]}
        </Badge>
        {bai.is_late && (
          <Badge className="bg-danger-soft text-danger border-danger/25 border">Trễ hạn</Badge>
        )}
        <span className="text-muted-foreground">Lần nộp {bai.attempt_no}</span>
        {bai.submitted_at && (
          <>
            <span className="text-muted-foreground" aria-hidden>·</span>
            {/* Tương đối để trả lời "bài này để lâu chưa"; mốc chính xác giữ trong title. */}
            <time
              className="text-muted-foreground"
              dateTime={bai.submitted_at}
              title={formatDateTime(bai.submitted_at)}
            >
              nộp {formatRelative(bai.submitted_at)}
            </time>
          </>
        )}
        {/* `typeof === "number"` chứ không phải `!== null`: API BỎ HẲN khoá `score` khỏi JSON
            khi bài chưa chấm (xem ghi chú ở khối "Trả bài" bên dưới), nên `!== null` đúng với
            cả bài chưa có điểm và dòng này hiện "Điểm hiện tại: —" trên một bài chưa chấm. */}
        {typeof bai.score === "number" && (
          <span className="ml-auto font-medium tabular-nums">
            Điểm hiện tại: <span className="text-ok">{formatScore(bai.score)}</span>
          </span>
        )}
      </div>

      {nhom && (
        // SRS mục 6.7: "báo cáo xong thì list ra nhóm đó có ai" — nhu cầu trực tiếp của
        // UC-25, chấm ngay sau buổi báo cáo trên lớp.
        <div className="border-border bg-card flex flex-wrap items-center gap-x-3 gap-y-2 rounded-lg border px-3.5 py-3 text-sm">
          <span className="text-muted-foreground flex shrink-0 items-center gap-1.5">
            <Users className="size-4" aria-hidden />
            {nhom.name}
          </span>
          <ul className="flex flex-wrap items-center gap-x-2 gap-y-1">
            {nhom.members.map((m) => (
              <li
                key={m.enrollment_id}
                className="border-border flex items-center gap-1.5 rounded-md border px-2 py-0.5"
              >
                <span className="truncate">{m.full_name}</span>
                <span className="text-muted-foreground font-mono text-xs">{m.student_code}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* ── Hai vùng: đọc bài | cho điểm ─────────────────────────────────────────────────
       * Xếp dọc dưới `lg`, cạnh nhau từ `lg` trở lên. Cột phải cố định 22rem chứ không co
       * giãn: bảng tiêu chí có bề rộng "đủ dùng" rất rõ, còn mã nguồn thì bao nhiêu cũng
       * thiếu — nên mọi pixel dôi ra phải chảy về cột trái.
       */}
      <div className="grid grid-cols-1 items-start gap-4 lg:grid-cols-[minmax(0,1fr)_22rem]">
        {/* ── Vùng 1: nội dung sinh viên nộp ───────────────────────────────────────── */}
        <Card className="min-w-0 gap-4">
          <CardHeader className="gap-3">
            <CardTitle className="text-base">Bài nộp</CardTitle>

            {slotKeys.length > 1 && (
              // Nhiều phần nộp thì đây là bộ chuyển; một phần thì nút đơn độc chỉ là nhiễu.
              <HorizontalScroll>
                <div className="flex gap-1.5">
                  {slotKeys.map((k) => {
                    const chon = slotDangXem === k;
                    return (
                      <button
                        key={k}
                        type="button"
                        onClick={() => setSlotDangXem(k)}
                        aria-pressed={chon}
                        className={`min-h-11 shrink-0 rounded-lg px-3 text-sm whitespace-nowrap transition-colors ${
                          chon
                            ? "bg-accent text-accent-foreground font-medium"
                            : "text-muted-foreground hover:bg-accent/50"
                        }`}
                      >
                        {k}{" "}
                        <span className="font-mono text-xs">
                          ({bai.files.filter((f) => f.slot_key === k).length})
                        </span>
                      </button>
                    );
                  })}
                </div>
              </HorizontalScroll>
            )}
          </CardHeader>

          <CardContent className="flex min-w-0 flex-col gap-4">
            {bai.files.length === 0 ? (
              <EmptyState
                Icon={Inbox}
                title="Bài nộp này không có tệp nào"
                description="Sinh viên có thể đã nộp bằng đường liên kết, hoặc phần nộp đã bị xóa sau khi nộp."
              />
            ) : fileCuaSlot.length === 0 ? (
              <EmptyState
                Icon={FolderTree}
                title="Chọn một phần để xem nội dung"
                description="Mỗi phần nộp là một ô riêng ở thanh phía trên."
              />
            ) : (
              fileCuaSlot.map((f) => (
                <XemTepNop key={f.id} submissionId={bai.id} file={f} />
              ))
            )}
          </CardContent>
        </Card>

        {/* ── Vùng 2: rubric và nhận xét ────────────────────────────────────────────────
         * `sticky` từ `lg`: đọc tới cuối một tệp 400 dòng mà bảng điểm đã cuộn khuất thì
         * thao tác tiếp theo luôn là cuộn ngược lên — nhân với 40 bài là một buổi làm việc.
         */}
        <Card className="gap-4 lg:sticky lg:top-8">
          <CardHeader>
            <CardTitle className="text-base">Chấm điểm</CardTitle>
          </CardHeader>

          <CardContent className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="rubric">Bộ tiêu chí</Label>
              {rubrics.length === 0 ? (
                <p className="text-muted-foreground text-sm text-pretty">
                  Chưa có bộ tiêu chí nào. Tạo ở{" "}
                  <a href="/rubrics" className="text-foreground underline underline-offset-2">
                    Bộ tiêu chí chấm
                  </a>{" "}
                  để chấm theo từng tiêu chí.
                </p>
              ) : (
                <Select value={rubricId ?? ""} onValueChange={setRubricId}>
                  <SelectTrigger id="rubric" className="min-h-11 w-full">
                    <SelectValue placeholder="Chọn bộ tiêu chí" />
                  </SelectTrigger>
                  <SelectContent>
                    {rubrics.map((r) => (
                      <SelectItem key={r.id} value={r.id}>
                        {r.name} ({formatScore(r.max_points)}đ)
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </div>

            {rubric && (
              <div className="flex flex-col gap-2.5">
                {rubric.criteria.map((c) => {
                  const v = diemTieuChi[c.id] ?? "";
                  const qua = Number(v) > c.max_points;

                  return (
                    <div
                      key={c.id}
                      className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1"
                    >
                      <Label
                        htmlFor={`tc-${c.id}`}
                        className="min-w-24 flex-1 text-sm font-normal text-pretty"
                      >
                        {c.description}
                      </Label>
                      <span className="flex shrink-0 items-center gap-1.5">
                        <Input
                          id={`tc-${c.id}`}
                          type="number"
                          step="0.25"
                          min={0}
                          max={c.max_points}
                          value={v}
                          aria-invalid={qua}
                          onChange={(e) =>
                            setDiemTieuChi((cu) => ({ ...cu, [c.id]: e.target.value }))
                          }
                          className="h-11 w-20 text-center tabular-nums"
                        />
                        <span className="text-muted-foreground w-9 text-sm tabular-nums">
                          /{formatScore(c.max_points)}
                        </span>
                      </span>
                    </div>
                  );
                })}

                <div className="border-border flex items-center justify-between border-t pt-2.5">
                  <span className="text-sm font-medium">Tổng</span>
                  <span
                    className={`text-lg font-semibold tabular-nums ${
                      vuotTran ? "text-danger" : ""
                    }`}
                  >
                    {formatScore(tong)}{" "}
                    <span className="text-muted-foreground text-sm font-normal">
                      / {formatScore(rubric.max_points)}
                    </span>
                  </span>
                </div>

                {vuotTran && (
                  <p className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3 py-2 text-sm text-pretty">
                    Có tiêu chí vượt điểm tối đa của chính nó — sửa lại trước khi lưu.
                  </p>
                )}
              </div>
            )}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="feedback">Nhận xét</Label>
              <Textarea
                id="feedback"
                rows={5}
                placeholder="Sinh viên đọc được nhận xét này sau khi bạn trả bài."
                value={nhanXet}
                onChange={(e) => setNhanXet(e.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Button
                className="min-h-11 w-full"
                disabled={cham.isPending || vuotTran}
                onClick={() => cham.mutate()}
              >
                {cham.isPending ? "Đang lưu…" : "Lưu điểm"}
              </Button>
              <p className="text-muted-foreground text-center text-xs">
                Hoặc bấm <kbd className="font-mono">⌘/Ctrl + Enter</kbd>
              </p>
            </div>

            <div className="border-border flex flex-col gap-2 border-t pt-3">
              {bai.status === "returned" ? (
                <p className="text-ok flex items-center gap-2 text-sm">
                  <span
                    className="bg-ok size-1.5 shrink-0 rounded-full"
                    aria-hidden
                  />
                  Đã trả bài — sinh viên xem được điểm và nhận xét.
                </p>
              ) : (
                <>
                  {/* LỖI HỢP ĐỒNG API, CỐ Ý KHÔNG VÁ Ở ĐÂY (việc của backend):
                      `GET /api/submissions/{id}` không trả khoá `score` khi bài chưa chấm, dù
                      OpenAPI khai `score: null | number` là trường BẮT BUỘC. Vì thế biểu thức
                      `score === null` bên dưới KHÔNG BAO GIỜ đúng, và nút "Trả bài" mở khoá
                      ngay cả trên bài chưa có điểm. Sửa ở frontend là dựng nguồn sự thật thứ
                      hai về hình dạng DTO — backend phải trả `"score": null`. */}
                  <Button
                    variant="outline"
                    className="min-h-11 w-full"
                    disabled={bai.score === null || traBai.isPending}
                    onClick={() => traBai.mutate()}
                  >
                    {traBai.isPending ? "Đang trả bài…" : "Trả bài cho sinh viên"}
                  </Button>
                  <p className="text-muted-foreground text-sm text-pretty">
                    {bai.score === null
                      ? "Lưu điểm trước rồi mới trả bài được."
                      : "Sinh viên chưa thấy điểm cho tới khi bạn bấm “Trả bài”."}
                  </p>
                </>
              )}
            </div>

            {/* Chỗ trống đã bố trí sẵn cho điểm cộng — Phase 4b nối BonusPointPopover vào
                đây và dùng lại đúng component đó ở sổ điểm. Không viết bản riêng ở 4a. */}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function XemTepNop({ submissionId, file }: { submissionId: string; file: SubmissionFileDto }) {
  const [dangXem, setDangXem] = useState<string | null>(null);

  const { data } = useQuery({
    queryKey: ["file-tree", submissionId, file.id] as const,
    queryFn: () =>
      apiFetch<{ entries: { path: string; size_bytes: number }[] }>(
        `/api/submissions/${submissionId}/files/${file.id}/file-tree`,
      ),
    enabled: file.processing_status === "done",
  });

  return (
    <div className="flex min-w-0 flex-col gap-2.5">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-mono text-sm font-medium">{file.file_name}</span>
        {/* Thiếu cấu trúc = bài nộp sai quy cách, cần giảng viên chú ý (hổ phách).
            Không đọc được = hệ thống hỏng với tệp này, giảng viên không chấm được (đỏ). */}
        {file.structure_check_passed === false && (
          <Badge className="bg-warn-soft text-warn border-warn/25 border">Thiếu cấu trúc</Badge>
        )}
        {file.processing_status === "failed" && (
          <Badge className="bg-danger-soft text-danger border-danger/25 border">
            Không đọc được
          </Badge>
        )}
      </div>

      {(data?.entries.length ?? 0) > 0 ? (
        <div className="flex min-w-0 flex-col gap-3 lg:flex-row">
          <HorizontalScroll>
            <ul className="border-border flex shrink-0 flex-col gap-0.5 rounded-lg border p-1.5 lg:w-56">
              {data!.entries.map((e) => {
                const chon = dangXem === e.path;
                return (
                  <li key={e.path}>
                    <button
                      type="button"
                      onClick={() => setDangXem((cu) => (cu === e.path ? null : e.path))}
                      aria-pressed={chon}
                      className={`w-full truncate rounded-md px-2 py-1.5 text-left font-mono text-xs transition-colors ${
                        chon
                          ? "bg-accent text-accent-foreground font-medium"
                          : "text-muted-foreground hover:bg-accent/50"
                      }`}
                    >
                      {e.path}
                    </button>
                  </li>
                );
              })}
            </ul>
          </HorizontalScroll>

          <div className="min-w-0 flex-1">
            {dangXem ? (
              <SourceViewer submissionId={submissionId} fileId={file.id} path={dangXem} />
            ) : (
              <EmptyState
                Icon={FileCode}
                title="Chọn một tệp để đọc"
                description="Nội dung hiện ở đây, có tô màu cú pháp."
              />
            )}
          </div>
        </div>
      ) : (
        <p className="text-muted-foreground text-sm text-pretty">
          {file.processing_status === "pending" || file.processing_status === "processing"
            ? "Đang xử lý tệp…"
            : "Tệp này không có cây thư mục để duyệt."}
        </p>
      )}
    </div>
  );
}
