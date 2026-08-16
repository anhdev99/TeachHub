"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { apiFetch } from "@/lib/api/fetcher";
import { NHAN_LOAI_CAU_HOI, type QuizDto, type QuizQuestionDto } from "@/lib/api/types";

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

          <div className="flex flex-wrap gap-4">
            <label className="flex items-center gap-2 text-sm">
              <Checkbox checked={tinhDiem} onCheckedChange={(c) => setTinhDiem(c === true)} />
              Tính điểm (bỏ tích = bài luyện tập)
            </label>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox checked={xaoCau} onCheckedChange={(c) => setXaoCau(c === true)} />
              Xáo thứ tự câu
            </label>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox checked={xaoPa} onCheckedChange={(c) => setXaoPa(c === true)} />
              Xáo thứ tự phương án
            </label>
          </div>

          <Button disabled={tao.isPending || !ten.trim()} onClick={() => tao.mutate()}>
            {tao.isPending ? "Đang tạo…" : "Tạo bài trắc nghiệm"}
          </Button>
        </CardContent>
      </Card>

      {ds.map((q) => (
        <ChonCauHoi key={q.id} quiz={q} nganHang={nganHang}
                    onXong={() => qc.invalidateQueries({ queryKey: khoa })} />
      ))}
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
          <Badge variant={quiz.config.is_graded ? "default" : "secondary"}>
            {quiz.config.is_graded ? "Tính điểm" : "Luyện tập"}
          </Badge>
          <Badge variant="outline">{quiz.question_count} câu · {quiz.total_points} điểm</Badge>
          {quiz.config.time_limit_minutes !== null && (
            <Badge variant="outline">{quiz.config.time_limit_minutes} phút</Badge>
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
              <p className="text-muted-foreground text-sm">
                Ngân hàng câu hỏi đang trống — soạn câu hỏi trước rồi quay lại đây.
              </p>
            ) : (
              nganHang.map((c) => (
                <label key={c.id} className="flex items-start gap-2 text-sm">
                  <Checkbox
                    className="mt-0.5"
                    checked={chon.includes(c.id)}
                    onCheckedChange={(v) =>
                      setChon((cu) => (v === true ? [...cu, c.id] : cu.filter((x) => x !== c.id)))
                    }
                  />
                  <span className="flex-1">
                    {c.text}
                    <span className="text-muted-foreground ml-2 text-xs">
                      {NHAN_LOAI_CAU_HOI[c.type]} · {c.points}đ
                    </span>
                  </span>
                </label>
              ))
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
