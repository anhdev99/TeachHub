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
import { EmptyState } from "@/components/shell/page-shell";
import { apiFetch } from "@/lib/api/fetcher";
import { formatScore } from "@/lib/format";
import type { RubricDto } from "@/lib/api/types";

type Criterion = { description: string; max_points: number };

export function RubricEditor({ initial }: { initial: RubricDto[] }) {
  const qc = useQueryClient();
  const queryKey = ["rubrics"] as const;

  const { data: rubrics } = useQuery({
    queryKey,
    queryFn: () => apiFetch<RubricDto[]>("/api/rubrics"),
    initialData: initial,
  });

  const [name, setName] = useState("");
  const [criteria, setCriteria] = useState<Criterion[]>([
    { description: "Chạy đúng yêu cầu", max_points: 5 },
    { description: "Xử lý lỗi hợp lý", max_points: 3 },
    { description: "Code sạch, có chú thích", max_points: 2 },
  ]);

  const total = criteria.reduce((sum, c) => sum + (Number(c.max_points) || 0), 0);
  const overScale = total > 10;

  const create = useMutation({
    mutationFn: () =>
      apiFetch<RubricDto>("/api/rubrics", {
        method: "POST",
        body: JSON.stringify({
          name,
          is_template: false,
          criteria: criteria.map((c) => ({
            description: c.description,
            max_points: Number(c.max_points),
          })),
        }),
      }),
    onSuccess: () => {
      toast.success("Đã tạo bộ tiêu chí.");
      setName("");
      qc.invalidateQueries({ queryKey });
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
              id="rubric_name" value={name} placeholder="Rubric bài tập lập trình"
              className="min-h-11"
              onChange={(e) => setName(e.target.value)}
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label>Tiêu chí và điểm tối đa của từng tiêu chí</Label>

            {/* Stack VERTICALLY on a phone: cramming description + points + the delete button
                into one 390px row leaves the description field about 140px wide, i.e. three
                visible words of a criterion that runs two lines long. */}
            {criteria.map((c, i) => (
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
                      setCriteria((prev) =>
                        prev.map((x, j) => (j === i ? { ...x, description: e.target.value } : x)),
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
                      setCriteria((prev) =>
                        prev.map((x, j) =>
                          j === i ? { ...x, max_points: Number(e.target.value) } : x,
                        ),
                      )
                    }
                  />
                  <Button
                    size="sm" variant="ghost"
                    className="text-muted-foreground hover:text-danger min-h-11 shrink-0"
                    onClick={() => setCriteria((prev) => prev.filter((_, j) => j !== i))}
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
              onClick={() => setCriteria((prev) => [...prev, { description: "", max_points: 1 }])}
            >
              + Thêm tiêu chí
            </Button>

            {/* A rubric totalling 12 points for an assignment on a 10-point scale is an easy
                mistake to make and only surfaces halfway through grading — warn right here
                instead of letting the backend reject it after everything has been typed. */}
            <span
              className={`rounded-lg border px-3 py-1.5 text-sm tabular-nums ${
                overScale
                  ? "bg-danger-soft text-danger border-danger/25"
                  : "bg-card text-muted-foreground border-border"
              }`}
            >
              Tổng: {formatScore(total)} / 10
              {overScale && " — vượt thang điểm"}
            </span>
          </div>

          <Button
            className="min-h-11 self-start"
            disabled={create.isPending || !name || overScale || criteria.length === 0}
            onClick={() => create.mutate()}
          >
            {create.isPending ? "Đang tạo…" : "Tạo bộ tiêu chí"}
          </Button>
        </CardContent>
      </Card>

      {rubrics.length === 0 ? (
        <EmptyState
          Icon={ClipboardList}
          title="Chưa có bộ tiêu chí nào"
          description="Tạo bộ tiêu chí đầu tiên bằng biểu mẫu phía trên; nó sẽ xuất hiện trong ô chọn ở màn hình chấm bài."
        />
      ) : (
        <div className="flex flex-col gap-3">
          {rubrics.map((r) => (
            <Card key={r.id}>
              <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-x-3 gap-y-2">
                <CardTitle className="min-w-0 text-base">{r.name}</CardTitle>
                <Badge className="bg-card text-muted-foreground border-border shrink-0 border tabular-nums">
                  {formatScore(r.max_points)} điểm
                </Badge>
              </CardHeader>
              <CardContent>
                <ul className="flex flex-col gap-1 text-sm">
                  {r.criteria.map((c) => (
                    <li key={c.id} className="flex items-baseline justify-between gap-3">
                      <span className="min-w-0 text-pretty">{c.description}</span>
                      <span className="text-muted-foreground shrink-0 font-mono tabular-nums">
                        {formatScore(c.max_points)}
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
