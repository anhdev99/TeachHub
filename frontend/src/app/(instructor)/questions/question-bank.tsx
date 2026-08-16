"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, ListChecks } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { EmptyState } from "@/components/shell/page-shell";
import { apiFetch } from "@/lib/api/fetcher";
import { formatScore } from "@/lib/format";
import { QUESTION_TYPE_LABELS, type QuestionType, type QuizQuestionDto } from "@/lib/api/types";

type Option = { text: string; is_correct: boolean };

export function QuestionBank({ initial }: { initial: QuizQuestionDto[] }) {
  const qc = useQueryClient();
  const queryKey = ["questions"] as const;

  const { data: questions } = useQuery({
    queryKey,
    queryFn: () => apiFetch<QuizQuestionDto[]>("/api/questions"),
    initialData: initial,
  });

  const [type, setType] = useState<QuestionType>("single_choice");
  const [text, setText] = useState("");
  const [explanation, setExplanation] = useState("");
  const [tags, setTags] = useState("");
  const [points, setPoints] = useState(1);
  const [partialCredit, setPartialCredit] = useState(false);
  const [options, setOptions] = useState<Option[]>([
    { text: "", is_correct: true },
    { text: "", is_correct: false },
  ]);
  const [blankAnswers, setBlankAnswers] = useState("");

  const isChoiceType = type === "single_choice" || type === "multiple_choice" || type === "true_false";

  const save = useMutation({
    mutationFn: () =>
      apiFetch<QuizQuestionDto>("/api/questions", {
        method: "POST",
        body: JSON.stringify({
          id: null,
          type,
          text,
          explanation: explanation || null,
          foundation_tags: tags.split(",").map((t) => t.trim()).filter(Boolean),
          points: Number(points),
          allow_partial_credit: type === "multiple_choice" ? partialCredit : false,
          options: isChoiceType ? options.filter((o) => o.text.trim()) : [],
          fill_blank:
            type === "fill_blank"
              ? {
                  accepted_answers: blankAnswers.split("\n").map((a) => a.trim()).filter(Boolean),
                  case_sensitive: false,
                  trim_whitespace: true,
                  // Unicode normalization must be ON for Vietnamese: "Hà Nội" typed with
                  // Telex and typed on macOS produce two different strings that look
                  // completely identical.
                  normalize_unicode: true,
                }
              : null,
        }),
      }),
    onSuccess: () => {
      toast.success("Đã lưu câu hỏi.");
      setText("");
      setExplanation("");
      setBlankAnswers("");
      setOptions([{ text: "", is_correct: true }, { text: "", is_correct: false }]);
      qc.invalidateQueries({ queryKey });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const remove = useMutation({
    mutationFn: (id: string) => apiFetch(`/api/questions/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      toast.success("Đã xóa câu hỏi.");
      qc.invalidateQueries({ queryKey });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  function changeType(v: QuestionType) {
    setType(v);
    if (v === "true_false") {
      setOptions([{ text: "Đúng", is_correct: true }, { text: "Sai", is_correct: false }]);
    } else if (v !== "fill_blank" && options.length < 2) {
      setOptions([{ text: "", is_correct: true }, { text: "", is_correct: false }]);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Câu hỏi mới</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-[11rem_6rem_minmax(0,1fr)]">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="qtype">Loại câu</Label>
              <Select value={type} onValueChange={(v) => changeType(v as QuestionType)}>
                <SelectTrigger id="qtype" className="min-h-11 w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {Object.entries(QUESTION_TYPE_LABELS).map(([value, label]) => (
                    <SelectItem key={value} value={value}>{label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="points">Điểm</Label>
              <Input id="points" type="number" step="0.5" min={0.5}
                     className="min-h-11 tabular-nums"
                     value={points} onChange={(e) => setPoints(Number(e.target.value))} />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="tags">Nhãn kiến thức (cách nhau bằng dấu phẩy)</Label>
              <Input id="tags" value={tags} placeholder="react_native, hooks"
                     className="min-h-11"
                     onChange={(e) => setTags(e.target.value)} />
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="qtext">Nội dung câu hỏi</Label>
            <Textarea id="qtext" rows={2} value={text}
                      onChange={(e) => setText(e.target.value)} />
          </div>

          {isChoiceType && (
            <div className="flex flex-col gap-2">
              <Label>Phương án — tích vào ô bên trái nếu là đáp án đúng</Label>
              {options.map((option, i) => (
                <div key={i} className="flex items-center gap-2">
                  <span
                    className={`grid size-11 shrink-0 place-items-center rounded-lg ${
                      option.is_correct ? "bg-ok-soft" : ""
                    }`}
                  >
                    <Checkbox
                      aria-label={`Phương án ${i + 1} là đáp án đúng`}
                      checked={option.is_correct}
                      onCheckedChange={(c) =>
                        setOptions((prev) =>
                          prev.map((x, j) =>
                            // Single-choice and true/false may have EXACTLY one correct
                            // answer, so ticking this box unticks every other one — leaving
                            // that to the user to remember means letting them submit a
                            // question the backend will reject.
                            type === "multiple_choice"
                              ? (j === i ? { ...x, is_correct: c === true } : x)
                              : { ...x, is_correct: j === i && c === true },
                          ),
                        )
                      }
                    />
                  </span>
                  <Input
                    className="min-h-11"
                    value={option.text} placeholder={`Phương án ${i + 1}`}
                    onChange={(e) =>
                      setOptions((prev) => prev.map((x, j) => (j === i ? { ...x, text: e.target.value } : x)))
                    }
                  />
                  {type !== "true_false" && options.length > 2 && (
                    <Button size="sm" variant="ghost"
                            className="text-muted-foreground hover:text-danger min-h-11 shrink-0"
                            onClick={() => setOptions((prev) => prev.filter((_, j) => j !== i))}>
                      Xóa
                    </Button>
                  )}
                </div>
              ))}

              {type !== "true_false" && (
                <Button size="sm" variant="outline" className="min-h-11 self-start"
                        onClick={() => setOptions((prev) => [...prev, { text: "", is_correct: false }])}>
                  + Thêm phương án
                </Button>
              )}

              {type === "multiple_choice" && (
                <label className="flex min-h-11 cursor-pointer items-center gap-2 text-sm">
                  <Checkbox checked={partialCredit}
                            onCheckedChange={(c) => setPartialCredit(c === true)} />
                  Chấm một phần (mặc định: tất-cả-hoặc-không)
                </label>
              )}
            </div>
          )}

          {type === "fill_blank" && (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="answers">Đáp án được chấp nhận — mỗi dòng một đáp án</Label>
              <Textarea id="answers" rows={3} value={blankAnswers} placeholder={"Hà Nội\nHa Noi"}
                        onChange={(e) => setBlankAnswers(e.target.value)} />
              <p className="text-muted-foreground text-sm text-pretty">
                Không phân biệt hoa thường, bỏ qua khoảng trắng thừa, và chuẩn hóa Unicode —
                sinh viên gõ bằng bộ gõ nào cũng chấm đúng.
              </p>
            </div>
          )}

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="explain">Giải thích (hiện sau khi nộp)</Label>
            <Textarea id="explain" rows={2} value={explanation}
                      onChange={(e) => setExplanation(e.target.value)} />
          </div>

          <Button
            className="min-h-11 self-start"
            disabled={save.isPending || !text.trim()}
            onClick={() => save.mutate()}
          >
            {save.isPending ? "Đang lưu…" : "Lưu câu hỏi"}
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Đã có {questions.length} câu hỏi</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          {questions.length === 0 ? (
            <EmptyState
              Icon={ListChecks}
              title="Ngân hàng còn trống"
              description="Câu hỏi lưu ở đây dùng lại được cho mọi bài trắc nghiệm của mọi lớp. Tạo câu đầu tiên bằng biểu mẫu phía trên."
            />
          ) : (
            questions.map((c) => (
              <div key={c.id} className="border-border flex flex-col gap-2 rounded-lg border p-3">
                {/* The "Xóa" button is a SEPARATE sibling of the badge cluster, not the last
                    element of the same wrapping row: kept together, at 390px it drops onto an
                    empty line of its own and the whole question card looks like it is missing
                    a block. */}
                <div className="flex items-start justify-between gap-2">
                  <div className="flex min-w-0 flex-wrap items-center gap-2">
                    <Badge className="bg-card text-muted-foreground border-border border">
                      {QUESTION_TYPE_LABELS[c.type]}
                    </Badge>
                    <Badge className="bg-card text-muted-foreground border-border border tabular-nums">
                      {formatScore(c.points)} điểm
                    </Badge>
                    {c.foundation_tags.map((t) => (
                      <Badge
                        key={t}
                        className="bg-accent text-accent-foreground border-transparent font-mono text-xs"
                      >
                        {t}
                      </Badge>
                    ))}
                  </div>
                  <Button size="sm" variant="ghost"
                          className="text-muted-foreground hover:text-danger -mt-1.5 min-h-11 shrink-0"
                          disabled={remove.isPending} onClick={() => remove.mutate(c.id)}>
                    Xóa
                  </Button>
                </div>

                <p className="text-sm text-pretty">{c.text}</p>

                {c.options.length > 0 && (
                  <ul className="flex flex-col gap-0.5 text-sm">
                    {c.options.map((o) => (
                      <li
                        key={o.id}
                        className={`flex items-start gap-1.5 ${
                          o.is_correct ? "text-ok font-medium" : "text-muted-foreground"
                        }`}
                      >
                        {o.is_correct ? (
                          <Check className="mt-0.5 size-4 shrink-0" aria-label="Đáp án đúng" />
                        ) : (
                          <span className="size-4 shrink-0" aria-hidden />
                        )}
                        <span className="min-w-0 text-pretty">{o.text}</span>
                      </li>
                    ))}
                  </ul>
                )}

                {c.fill_blank && (
                  <p className="text-muted-foreground text-sm text-pretty">
                    Đáp án:{" "}
                    <span className="text-ok font-medium">
                      {c.fill_blank.accepted_answers.join(" · ")}
                    </span>
                  </p>
                )}
              </div>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}
