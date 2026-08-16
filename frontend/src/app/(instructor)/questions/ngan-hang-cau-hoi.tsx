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
import { TrongRong } from "@/components/shell/trang";
import { apiFetch } from "@/lib/api/fetcher";
import { diem } from "@/lib/dinh-dang";
import { NHAN_LOAI_CAU_HOI, type QuestionType, type QuizQuestionDto } from "@/lib/api/types";

type PhuongAn = { text: string; is_correct: boolean };

export function NganHangCauHoi({ banDau }: { banDau: QuizQuestionDto[] }) {
  const qc = useQueryClient();
  const khoa = ["questions"] as const;

  const { data: cau } = useQuery({
    queryKey: khoa,
    queryFn: () => apiFetch<QuizQuestionDto[]>("/api/questions"),
    initialData: banDau,
  });

  const [loai, setLoai] = useState<QuestionType>("single_choice");
  const [noiDung, setNoiDung] = useState("");
  const [giaiThich, setGiaiThich] = useState("");
  const [tags, setTags] = useState("");
  const [soDiem, setSoDiem] = useState(1);
  const [chamMotPhan, setChamMotPhan] = useState(false);
  const [pa, setPa] = useState<PhuongAn[]>([
    { text: "", is_correct: true },
    { text: "", is_correct: false },
  ]);
  const [dapAnDien, setDapAnDien] = useState("");

  const laCauChon = loai === "single_choice" || loai === "multiple_choice" || loai === "true_false";

  const luu = useMutation({
    mutationFn: () =>
      apiFetch<QuizQuestionDto>("/api/questions", {
        method: "POST",
        body: JSON.stringify({
          id: null,
          type: loai,
          text: noiDung,
          explanation: giaiThich || null,
          foundation_tags: tags.split(",").map((t) => t.trim()).filter(Boolean),
          points: Number(soDiem),
          allow_partial_credit: loai === "multiple_choice" ? chamMotPhan : false,
          options: laCauChon ? pa.filter((p) => p.text.trim()) : [],
          fill_blank:
            loai === "fill_blank"
              ? {
                  accepted_answers: dapAnDien.split("\n").map((a) => a.trim()).filter(Boolean),
                  case_sensitive: false,
                  trim_whitespace: true,
                  // Chuẩn hóa Unicode phải BẬT cho tiếng Việt: "Hà Nội" gõ Telex và gõ trên
                  // macOS ra hai chuỗi khác nhau nhưng nhìn giống hệt.
                  normalize_unicode: true,
                }
              : null,
        }),
      }),
    onSuccess: () => {
      toast.success("Đã lưu câu hỏi.");
      setNoiDung("");
      setGiaiThich("");
      setDapAnDien("");
      setPa([{ text: "", is_correct: true }, { text: "", is_correct: false }]);
      qc.invalidateQueries({ queryKey: khoa });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const xoa = useMutation({
    mutationFn: (id: string) => apiFetch(`/api/questions/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      toast.success("Đã xóa câu hỏi.");
      qc.invalidateQueries({ queryKey: khoa });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  function doiLoai(v: QuestionType) {
    setLoai(v);
    if (v === "true_false") {
      setPa([{ text: "Đúng", is_correct: true }, { text: "Sai", is_correct: false }]);
    } else if (v !== "fill_blank" && pa.length < 2) {
      setPa([{ text: "", is_correct: true }, { text: "", is_correct: false }]);
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
              <Select value={loai} onValueChange={(v) => doiLoai(v as QuestionType)}>
                <SelectTrigger id="qtype" className="min-h-11 w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {Object.entries(NHAN_LOAI_CAU_HOI).map(([k, n]) => (
                    <SelectItem key={k} value={k}>{n}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="points">Điểm</Label>
              <Input id="points" type="number" step="0.5" min={0.5}
                     className="min-h-11 tabular-nums"
                     value={soDiem} onChange={(e) => setSoDiem(Number(e.target.value))} />
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
            <Textarea id="qtext" rows={2} value={noiDung}
                      onChange={(e) => setNoiDung(e.target.value)} />
          </div>

          {laCauChon && (
            <div className="flex flex-col gap-2">
              <Label>Phương án — tích vào ô bên trái nếu là đáp án đúng</Label>
              {pa.map((p, i) => (
                <div key={i} className="flex items-center gap-2">
                  <span
                    className={`grid size-11 shrink-0 place-items-center rounded-lg ${
                      p.is_correct ? "bg-ok-soft" : ""
                    }`}
                  >
                    <Checkbox
                      aria-label={`Phương án ${i + 1} là đáp án đúng`}
                      checked={p.is_correct}
                      onCheckedChange={(c) =>
                        setPa((cu) =>
                          cu.map((x, j) =>
                            // Chọn-một và Đúng/Sai chỉ được có ĐÚNG một đáp án đúng, nên tích ô
                            // này là bỏ tích mọi ô khác — để người dùng tự nhớ là để họ gửi lên
                            // một câu hỏi mà backend từ chối.
                            loai === "multiple_choice"
                              ? (j === i ? { ...x, is_correct: c === true } : x)
                              : { ...x, is_correct: j === i && c === true },
                          ),
                        )
                      }
                    />
                  </span>
                  <Input
                    className="min-h-11"
                    value={p.text} placeholder={`Phương án ${i + 1}`}
                    onChange={(e) =>
                      setPa((cu) => cu.map((x, j) => (j === i ? { ...x, text: e.target.value } : x)))
                    }
                  />
                  {loai !== "true_false" && pa.length > 2 && (
                    <Button size="sm" variant="ghost"
                            className="text-muted-foreground hover:text-danger min-h-11 shrink-0"
                            onClick={() => setPa((cu) => cu.filter((_, j) => j !== i))}>
                      Xóa
                    </Button>
                  )}
                </div>
              ))}

              {loai !== "true_false" && (
                <Button size="sm" variant="outline" className="min-h-11 self-start"
                        onClick={() => setPa((cu) => [...cu, { text: "", is_correct: false }])}>
                  + Thêm phương án
                </Button>
              )}

              {loai === "multiple_choice" && (
                <label className="flex min-h-11 cursor-pointer items-center gap-2 text-sm">
                  <Checkbox checked={chamMotPhan}
                            onCheckedChange={(c) => setChamMotPhan(c === true)} />
                  Chấm một phần (mặc định: tất-cả-hoặc-không)
                </label>
              )}
            </div>
          )}

          {loai === "fill_blank" && (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="answers">Đáp án được chấp nhận — mỗi dòng một đáp án</Label>
              <Textarea id="answers" rows={3} value={dapAnDien} placeholder={"Hà Nội\nHa Noi"}
                        onChange={(e) => setDapAnDien(e.target.value)} />
              <p className="text-muted-foreground text-sm text-pretty">
                Không phân biệt hoa thường, bỏ qua khoảng trắng thừa, và chuẩn hóa Unicode —
                sinh viên gõ bằng bộ gõ nào cũng chấm đúng.
              </p>
            </div>
          )}

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="explain">Giải thích (hiện sau khi nộp)</Label>
            <Textarea id="explain" rows={2} value={giaiThich}
                      onChange={(e) => setGiaiThich(e.target.value)} />
          </div>

          <Button
            className="min-h-11 self-start"
            disabled={luu.isPending || !noiDung.trim()}
            onClick={() => luu.mutate()}
          >
            {luu.isPending ? "Đang lưu…" : "Lưu câu hỏi"}
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Đã có {cau.length} câu hỏi</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          {cau.length === 0 ? (
            <TrongRong
              Icon={ListChecks}
              tieuDe="Ngân hàng còn trống"
              moTa="Câu hỏi lưu ở đây dùng lại được cho mọi bài trắc nghiệm của mọi lớp. Tạo câu đầu tiên bằng biểu mẫu phía trên."
            />
          ) : (
            cau.map((c) => (
              <div key={c.id} className="border-border flex flex-col gap-2 rounded-lg border p-3">
                {/* Nút "Xóa" là anh em RIÊNG của cụm nhãn, không phải phần tử cuối cùng của
                    cùng một hàng wrap: để chung thì ở 390px nó rơi xuống một dòng trống của
                    riêng nó và cả thẻ câu hỏi trông như bị hụt một khối. */}
                <div className="flex items-start justify-between gap-2">
                  <div className="flex min-w-0 flex-wrap items-center gap-2">
                    <Badge className="bg-card text-muted-foreground border-border border">
                      {NHAN_LOAI_CAU_HOI[c.type]}
                    </Badge>
                    <Badge className="bg-card text-muted-foreground border-border border tabular-nums">
                      {diem(c.points)} điểm
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
                          disabled={xoa.isPending} onClick={() => xoa.mutate(c.id)}>
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
