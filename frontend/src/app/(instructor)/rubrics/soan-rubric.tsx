"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClipboardList } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { TrongRong } from "@/components/shell/trang";
import { apiFetch } from "@/lib/api/fetcher";
import { diem } from "@/lib/dinh-dang";
import type { RubricDto } from "@/lib/api/types";

type TieuChi = { description: string; max_points: number };

export function SoanRubric({ banDau }: { banDau: RubricDto[] }) {
  const qc = useQueryClient();
  const khoa = ["rubrics"] as const;

  const { data: rubrics } = useQuery({
    queryKey: khoa,
    queryFn: () => apiFetch<RubricDto[]>("/api/rubrics"),
    initialData: banDau,
  });

  const [ten, setTen] = useState("");
  const [tieuChi, setTieuChi] = useState<TieuChi[]>([
    { description: "Chạy đúng yêu cầu", max_points: 5 },
    { description: "Xử lý lỗi hợp lý", max_points: 3 },
    { description: "Code sạch, có chú thích", max_points: 2 },
  ]);

  const tong = tieuChi.reduce((t, c) => t + (Number(c.max_points) || 0), 0);
  const vuotThang = tong > 10;

  const tao = useMutation({
    mutationFn: () =>
      apiFetch<RubricDto>("/api/rubrics", {
        method: "POST",
        body: JSON.stringify({
          name: ten,
          is_template: false,
          criteria: tieuChi.map((c) => ({
            description: c.description,
            max_points: Number(c.max_points),
          })),
        }),
      }),
    onSuccess: () => {
      toast.success("Đã tạo bộ tiêu chí.");
      setTen("");
      qc.invalidateQueries({ queryKey: khoa });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Bộ tiêu chí mới</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="rubric_name">Tên bộ tiêu chí</Label>
            <Input
              id="rubric_name" value={ten} placeholder="Rubric bài tập lập trình"
              className="min-h-11"
              onChange={(e) => setTen(e.target.value)}
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label>Tiêu chí và điểm tối đa của từng tiêu chí</Label>

            {/* Xếp DỌC ở điện thoại: nhét mô tả + điểm + nút Xóa vào một hàng 390px thì ô mô
                tả còn ~140px, tức nhìn được ba chữ của một câu tiêu chí dài hai dòng. */}
            {tieuChi.map((c, i) => (
              <div
                key={i}
                className="border-border flex flex-col gap-2 rounded-lg border p-2.5 sm:flex-row sm:items-center"
              >
                <div className="flex min-w-0 flex-1 items-center gap-2">
                  <span className="text-muted-foreground w-5 shrink-0 text-right font-mono text-xs tabular-nums">
                    {i + 1}.
                  </span>
                  <Input
                    aria-label={`Mô tả tiêu chí ${i + 1}`}
                    className="min-h-11 min-w-0 flex-1"
                    value={c.description}
                    onChange={(e) =>
                      setTieuChi((cu) =>
                        cu.map((x, j) => (j === i ? { ...x, description: e.target.value } : x)),
                      )
                    }
                  />
                </div>

                <div className="flex items-center justify-end gap-2">
                  <span className="text-muted-foreground text-sm sm:sr-only">Điểm tối đa</span>
                  <Input
                    aria-label={`Điểm tối đa của tiêu chí ${i + 1}`}
                    type="number" step="0.5" min={0} max={10}
                    className="h-11 w-20 shrink-0 text-center tabular-nums"
                    value={c.max_points}
                    onChange={(e) =>
                      setTieuChi((cu) =>
                        cu.map((x, j) =>
                          j === i ? { ...x, max_points: Number(e.target.value) } : x,
                        ),
                      )
                    }
                  />
                  <Button
                    size="sm" variant="ghost"
                    className="text-muted-foreground hover:text-danger min-h-11 shrink-0"
                    onClick={() => setTieuChi((cu) => cu.filter((_, j) => j !== i))}
                  >
                    Xóa
                  </Button>
                </div>
              </div>
            ))}
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3">
            <Button
              size="sm" variant="outline" className="min-h-11"
              onClick={() => setTieuChi((cu) => [...cu, { description: "", max_points: 1 }])}
            >
              + Thêm tiêu chí
            </Button>

            {/* Bộ tiêu chí tổng 12 điểm cho bài thang 10 là lỗi rất dễ mắc và chỉ lộ ra lúc
                đang chấm dở — cảnh báo ngay tại đây thay vì để backend từ chối sau khi gõ xong. */}
            <span
              className={`rounded-lg border px-3 py-1.5 text-sm tabular-nums ${
                vuotThang
                  ? "bg-danger-soft text-danger border-danger/25"
                  : "bg-card text-muted-foreground border-border"
              }`}
            >
              Tổng: {diem(tong)} / 10
              {vuotThang && " — vượt thang điểm"}
            </span>
          </div>

          <Button
            className="min-h-11 self-start"
            disabled={tao.isPending || !ten || vuotThang || tieuChi.length === 0}
            onClick={() => tao.mutate()}
          >
            {tao.isPending ? "Đang tạo…" : "Tạo bộ tiêu chí"}
          </Button>
        </CardContent>
      </Card>

      {rubrics.length === 0 ? (
        <TrongRong
          Icon={ClipboardList}
          tieuDe="Chưa có bộ tiêu chí nào"
          moTa="Tạo bộ tiêu chí đầu tiên bằng biểu mẫu phía trên; nó sẽ xuất hiện trong ô chọn ở màn hình chấm bài."
        />
      ) : (
        <div className="flex flex-col gap-3">
          {rubrics.map((r) => (
            <Card key={r.id}>
              <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-x-3 gap-y-2">
                <CardTitle className="min-w-0 text-base">{r.name}</CardTitle>
                <Badge className="bg-card text-muted-foreground border-border shrink-0 border tabular-nums">
                  {diem(r.max_points)} điểm
                </Badge>
              </CardHeader>
              <CardContent>
                <ul className="flex flex-col gap-1 text-sm">
                  {r.criteria.map((c) => (
                    <li key={c.id} className="flex items-baseline justify-between gap-3">
                      <span className="min-w-0 text-pretty">{c.description}</span>
                      <span className="text-muted-foreground shrink-0 font-mono tabular-nums">
                        {diem(c.max_points)}
                      </span>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
