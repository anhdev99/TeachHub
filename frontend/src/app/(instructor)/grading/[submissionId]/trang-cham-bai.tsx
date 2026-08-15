"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { XemMaNguon } from "@/components/submission/xem-ma-nguon";
import { apiFetch } from "@/lib/api/fetcher";
import type {
  GroupDto, RubricDto, SubmissionDto, SubmissionFileDto,
} from "@/lib/api/types";

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
  useEffect(() => {
    const cu = bai.rubric_scores ?? {};
    if (Object.keys(cu).length > 0) {
      setDiemTieuChi(Object.fromEntries(Object.entries(cu).map(([k, v]) => [k, String(v)])));
    }
  }, [bai.rubric_scores]);

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
    <div className="flex flex-1 flex-col">
      {nhom && (
        // SRS mục 6.7: "báo cáo xong thì list ra nhóm đó có ai" — nhu cầu trực tiếp của
        // UC-25, chấm ngay sau buổi báo cáo trên lớp.
        <div className="border-border flex flex-wrap items-center gap-2 border-b p-3 text-sm">
          <span className="font-medium">{nhom.name}</span>
          {nhom.members.map((m) => (
            <span key={m.enrollment_id} className="text-muted-foreground">
              {m.full_name} <span className="font-mono text-xs">{m.student_code}</span>
            </span>
          ))}
        </div>
      )}

      <div className="flex flex-1 flex-col lg:flex-row">
        {/* Cột 1 — các slot đã nộp */}
        <aside className="border-border w-full shrink-0 border-b p-3 lg:w-56 lg:border-r lg:border-b-0">
          <h2 className="text-muted-foreground mb-2 text-xs font-medium uppercase">Phần đã nộp</h2>
          <ul className="flex flex-col gap-1">
            {slotKeys.length === 0 && (
              <li className="text-muted-foreground text-sm">Không có tệp nào.</li>
            )}
            {slotKeys.map((k) => (
              <li key={k}>
                <button
                  type="button"
                  onClick={() => setSlotDangXem(k)}
                  className={`w-full rounded px-2 py-1.5 text-left text-sm ${
                    slotDangXem === k ? "bg-muted font-medium" : "hover:bg-muted"
                  }`}
                >
                  {k}{" "}
                  <span className="text-muted-foreground">
                    ({bai.files.filter((f) => f.slot_key === k).length})
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </aside>

        {/* Cột 2 — nội dung slot đang xem */}
        <section className="min-w-0 flex-1 p-3 sm:p-4">
          {fileCuaSlot.length === 0 ? (
            <p className="text-muted-foreground text-sm">Chọn một phần để xem nội dung.</p>
          ) : (
            fileCuaSlot.map((f) => (
              <XemTepNop key={f.id} submissionId={bai.id} file={f} />
            ))
          )}
        </section>

        {/* Cột 3 — rubric và nhận xét */}
        <aside className="border-border w-full shrink-0 border-t p-3 sm:p-4 lg:w-80 lg:border-t-0 lg:border-l">
          <div className="flex flex-col gap-3">
            <div className="flex flex-col gap-1">
              <Label>Rubric</Label>
              {rubrics.length === 0 ? (
                <p className="text-muted-foreground text-sm">
                  Chưa có rubric nào. Tạo rubric ở{" "}
                  <a href="/rubrics" className="underline">
                    trang rubric
                  </a>{" "}
                  để chấm theo tiêu chí.
                </p>
              ) : (
                <Select value={rubricId ?? ""} onValueChange={setRubricId}>
                  <SelectTrigger><SelectValue placeholder="Chọn rubric" /></SelectTrigger>
                  <SelectContent>
                    {rubrics.map((r) => (
                      <SelectItem key={r.id} value={r.id}>
                        {r.name} ({r.max_points}đ)
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </div>

            {rubric?.criteria.map((c) => {
              const v = diemTieuChi[c.id] ?? "";
              const qua = Number(v) > c.max_points;

              return (
                <div key={c.id} className="flex items-center gap-2">
                  <span className="flex-1 text-sm">{c.description}</span>
                  <Input
                    type="number"
                    step="0.25"
                    min={0}
                    max={c.max_points}
                    value={v}
                    onChange={(e) =>
                      setDiemTieuChi((cu) => ({ ...cu, [c.id]: e.target.value }))
                    }
                    className={`w-20 ${qua ? "border-destructive" : ""}`}
                  />
                  <span className="text-muted-foreground w-8 text-sm">/{c.max_points}</span>
                </div>
              );
            })}

            {rubric && (
              <div className="border-border flex items-center justify-between border-t pt-2">
                <span className="text-sm font-medium">Tổng</span>
                <span className={`font-mono ${vuotTran ? "text-destructive" : ""}`}>
                  {tong.toFixed(2)} / {rubric.max_points}
                </span>
              </div>
            )}

            {vuotTran && (
              <p className="text-destructive text-sm">
                Có tiêu chí vượt điểm tối đa của chính nó.
              </p>
            )}

            <div className="flex flex-col gap-1">
              <Label htmlFor="feedback">Nhận xét</Label>
              <Textarea
                id="feedback"
                rows={5}
                value={nhanXet}
                onChange={(e) => setNhanXet(e.target.value)}
              />
            </div>

            <Button disabled={cham.isPending || vuotTran} onClick={() => cham.mutate()}>
              {cham.isPending ? "Đang lưu…" : "Lưu điểm (⌘/Ctrl + Enter)"}
            </Button>

            <div className="flex flex-wrap items-center gap-2 text-sm">
              {bai.score !== null && <Badge>Đã chấm: {bai.score}</Badge>}
              {bai.status === "returned" ? (
                <Badge variant="secondary">Đã trả bài</Badge>
              ) : (
                <Button
                  size="sm"
                  variant="outline"
                  disabled={bai.score === null || traBai.isPending}
                  onClick={() => traBai.mutate()}
                >
                  Trả bài
                </Button>
              )}
            </div>

            {bai.status !== "returned" && bai.score !== null && (
              <p className="text-muted-foreground text-sm">
                Sinh viên chưa thấy điểm cho tới khi bạn bấm “Trả bài”.
              </p>
            )}

            {/* Chỗ trống đã bố trí sẵn cho điểm cộng — Phase 4b nối BonusPointPopover vào
                đây và dùng lại đúng component đó ở sổ điểm. Không viết bản riêng ở 4a. */}
          </div>
        </aside>
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
    <div className="mb-4 flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-medium">{file.file_name}</span>
        {file.structure_check_passed === false && (
          <Badge variant="destructive">Thiếu cấu trúc</Badge>
        )}
        {file.processing_status === "failed" && (
          <Badge variant="destructive">Không đọc được</Badge>
        )}
      </div>

      {(data?.entries.length ?? 0) > 0 ? (
        <div className="flex flex-col gap-2 lg:flex-row">
          <ul className="flex shrink-0 flex-col gap-0.5 font-mono text-xs lg:w-56">
            {data!.entries.map((e) => (
              <li key={e.path}>
                <button
                  type="button"
                  onClick={() => setDangXem((cu) => (cu === e.path ? null : e.path))}
                  className={`hover:bg-muted w-full truncate rounded px-1.5 py-0.5 text-left ${
                    dangXem === e.path ? "bg-muted" : "text-muted-foreground"
                  }`}
                >
                  {e.path}
                </button>
              </li>
            ))}
          </ul>

          <div className="min-w-0 flex-1">
            {dangXem ? (
              <XemMaNguon submissionId={submissionId} fileId={file.id} path={dangXem} />
            ) : (
              <p className="text-muted-foreground text-sm">Chọn một tệp để xem nội dung.</p>
            )}
          </div>
        </div>
      ) : (
        <p className="text-muted-foreground text-sm">
          {file.processing_status === "pending" || file.processing_status === "processing"
            ? "Đang xử lý tệp…"
            : "Tệp này không có cây thư mục để duyệt."}
        </p>
      )}
    </div>
  );
}
