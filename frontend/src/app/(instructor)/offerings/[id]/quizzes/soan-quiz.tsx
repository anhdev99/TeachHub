"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ListChecks, Plus, Timer } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { EmptyState } from "@/components/shell/page-shell";
import { apiFetch } from "@/lib/api/fetcher";
import { QUESTION_TYPE_LABELS, type QuizDto, type QuizQuestionDto } from "@/lib/api/types";

export function SoanQuiz({
  offeringId,
  banDau,
  nganHang,
}: {
  offeringId: string;
  banDau: QuizDto[];
  nganHang: QuizQuestionDto[];
}) {
  const qc = useQueryClient();
  const khoa = ["quizzes", offeringId] as const;

  const { data: ds } = useQuery({
    queryKey: khoa,
    queryFn: () => apiFetch<QuizDto[]>(`/api/quizzes?offering_id=${offeringId}`),
    initialData: banDau,
  });

  const [ten, setTen] = useState("");
  const [tinhDiem, setTinhDiem] = useState(true);
  const [phut, setPhut] = useState<number | "">(15);
  const [soLuot, setSoLuot] = useState<number | "">(2);
  const [xaoCau, setXaoCau] = useState(true);
  const [xaoPa, setXaoPa] = useState(true);

  const tao = useMutation({
    mutationFn: () =>
      apiFetch<QuizDto>("/api/quizzes", {
        method: "POST",
        body: JSON.stringify({
          id: null,
          subject_version_id: null,
          offering_id: offeringId,
          title: ten,
          description: null,
          config: {
            is_graded: tinhDiem,
            time_limit_minutes: phut === "" ? null : Number(phut),
            max_attempts: soLuot === "" ? null : Number(soLuot),
            shuffle_questions: xaoCau,
            shuffle_options: xaoPa,
            show_explanation_after_submit: true,
          },
        }),
      }),
    onSuccess: () => {
      toast.success("Đã tạo bài trắc nghiệm.");
      setTen("");
      qc.invalidateQueries({ queryKey: khoa });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Bài trắc nghiệm mới</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <div className="flex flex-col gap-1">
            <Label htmlFor="quiz_title">Tên bài</Label>
            <Input id="quiz_title" value={ten} placeholder="Kiểm tra giữa kỳ"
                   onChange={(e) => setTen(e.target.value)} />
          </div>

          <div className="flex flex-wrap items-end gap-3">
            <div className="flex flex-col gap-1">
              <Label htmlFor="limit">Thời gian (phút)</Label>
              <Input id="limit" type="number" min={1} className="w-28" value={phut}
                     placeholder="không giới hạn"
                     onChange={(e) => setPhut(e.target.value === "" ? "" : Number(e.target.value))} />
            </div>

            <div className="flex flex-col gap-1">
              <Label htmlFor="attempts">Số lượt</Label>
              <Input id="attempts" type="number" min={1} className="w-28" value={soLuot}
                     placeholder="không giới hạn"
                     onChange={(e) => setSoLuot(e.target.value === "" ? "" : Number(e.target.value))} />
            </div>
          </div>

          <div className="flex flex-wrap gap-x-5">
            <label className="flex min-h-11 items-center gap-2.5 text-sm">
              <Checkbox checked={tinhDiem} onCheckedChange={(c) => setTinhDiem(c === true)} />
              Tính điểm (bỏ tích = bài luyện tập)
            </label>
            <label className="flex min-h-11 items-center gap-2.5 text-sm">
              <Checkbox checked={xaoCau} onCheckedChange={(c) => setXaoCau(c === true)} />
              Xáo thứ tự câu
            </label>
            <label className="flex min-h-11 items-center gap-2.5 text-sm">
              <Checkbox checked={xaoPa} onCheckedChange={(c) => setXaoPa(c === true)} />
              Xáo thứ tự phương án
            </label>
          </div>

          <Button
            className="self-start"
            disabled={tao.isPending || !ten.trim()}
            onClick={() => tao.mutate()}
          >
            <Plus className="size-4" aria-hidden />
            {tao.isPending ? "Đang tạo…" : "Tạo bài trắc nghiệm"}
          </Button>
        </CardContent>
      </Card>

      {ds.length === 0 ? (
        <Card>
          <CardContent>
            <EmptyState
              Icon={ListChecks}
              title="Lớp này chưa có bài trắc nghiệm nào"
              description="Tạo bài ở khối bên trên, rồi chọn câu hỏi từ ngân hàng câu hỏi của bạn."
            />
          </CardContent>
        </Card>
      ) : (
        ds.map((q) => (
          <ChonCauHoi key={q.id} quiz={q} nganHang={nganHang}
                      onXong={() => qc.invalidateQueries({ queryKey: khoa })} />
        ))
      )}
    </div>
  );
}

function ChonCauHoi({
  quiz,
  nganHang,
  onXong,
}: {
  quiz: QuizDto;
  nganHang: QuizQuestionDto[];
  onXong: () => void;
}) {
  const [mo, setMo] = useState(false);
  const [chon, setChon] = useState<string[]>(quiz.questions.map((q) => q.id));

  const luu = useMutation({
    mutationFn: () =>
      apiFetch<QuizDto>(`/api/quizzes/${quiz.id}/questions`, {
        method: "PUT",
        body: JSON.stringify({ question_ids: chon }),
      }),
    onSuccess: () => {
      toast.success("Đã cập nhật danh sách câu hỏi.");
      setMo(false);
      onXong();
    },
    // Backend trả 409 khi quiz đã có lượt làm — hiện nguyên câu đó, vì nó nói đúng lý do.
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <Card>
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
        <CardTitle className="text-base">{quiz.title}</CardTitle>
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant={quiz.config.is_graded ? "default" : "secondary"} className="font-normal">
            {quiz.config.is_graded ? "Tính điểm" : "Luyện tập"}
          </Badge>
          <Badge variant="outline" className="font-normal">
            {quiz.question_count} câu · {quiz.total_points} điểm
          </Badge>
          {/* `!= null`: API không gửi khoá nào có giá trị null, nên bài không giới hạn thời
              gian về tới đây là `undefined` — so sánh chặt sẽ lọt và in ra nhãn trống "phút". */}
          {quiz.config.time_limit_minutes != null && (
            <Badge variant="outline" className="gap-1 font-normal">
              <Timer className="size-3" aria-hidden />
              {quiz.config.time_limit_minutes} phút
            </Badge>
          )}
        </div>
      </CardHeader>

      <CardContent className="flex flex-col gap-3">
        {!mo ? (
          <Button size="sm" variant="outline" className="self-start" onClick={() => setMo(true)}>
            Chọn câu hỏi
          </Button>
        ) : (
          <>
            {nganHang.length === 0 ? (
              <EmptyState
                Icon={ListChecks}
                title="Ngân hàng câu hỏi đang trống"
                description="Soạn câu hỏi một lần trong ngân hàng, rồi chọn lại cho bất kỳ bài trắc nghiệm nào."
                action={
                  <Link
                    href="/questions"
                    className="bg-primary text-primary-foreground inline-flex min-h-11 items-center rounded-lg px-4 text-sm font-medium"
                  >
                    Tới Ngân hàng câu hỏi
                  </Link>
                }
              />
            ) : (
              <ul className="flex flex-col gap-0.5">
                {nganHang.map((c) => (
                  <li key={c.id}>
                    <label className="hover:bg-accent flex min-h-11 items-start gap-2.5 rounded-lg px-2 py-2 text-sm transition-colors">
                      <Checkbox
                        className="mt-0.5"
                        checked={chon.includes(c.id)}
                        onCheckedChange={(v) =>
                          setChon((cu) =>
                            v === true ? [...cu, c.id] : cu.filter((x) => x !== c.id),
                          )
                        }
                      />
                      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                        <span className="text-pretty">{c.text}</span>
                        <span className="text-muted-foreground text-xs">
                          {QUESTION_TYPE_LABELS[c.type]} · {c.points}đ
                        </span>
                      </span>
                    </label>
                  </li>
                ))}
              </ul>
            )}

            <div className="flex gap-2">
              <Button size="sm" disabled={luu.isPending} onClick={() => luu.mutate()}>
                {luu.isPending ? "Đang lưu…" : `Lưu ${chon.length} câu`}
              </Button>
              <Button size="sm" variant="ghost" onClick={() => setMo(false)}>Đóng</Button>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}
