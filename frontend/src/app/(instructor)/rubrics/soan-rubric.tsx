"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { apiFetch } from "@/lib/api/fetcher";
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
      toast.success("Đã tạo rubric.");
      setTen("");
      qc.invalidateQueries({ queryKey: khoa });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Rubric mới</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <div className="flex flex-col gap-1">
            <Label htmlFor="rubric_name">Tên rubric</Label>
            <Input
              id="rubric_name" value={ten} placeholder="Rubric bài tập lập trình"
              onChange={(e) => setTen(e.target.value)}
            />
          </div>

          {tieuChi.map((c, i) => (
            <div key={i} className="flex flex-wrap items-end gap-2">
              <div className="flex min-w-48 flex-1 flex-col gap-1">
                <Label>Tiêu chí {i + 1}</Label>
                <Input
                  value={c.description}
                  onChange={(e) =>
                    setTieuChi((cu) =>
                      cu.map((x, j) => (j === i ? { ...x, description: e.target.value } : x)),
                    )
                  }
                />
              </div>

              <div className="flex flex-col gap-1">
                <Label>Điểm tối đa</Label>
                <Input
                  type="number" step="0.5" min={0} max={10} className="w-24"
                  value={c.max_points}
                  onChange={(e) =>
                    setTieuChi((cu) =>
                      cu.map((x, j) =>
                        j === i ? { ...x, max_points: Number(e.target.value) } : x,
                      ),
                    )
                  }
                />
              </div>

              <Button
                size="sm" variant="ghost"
                onClick={() => setTieuChi((cu) => cu.filter((_, j) => j !== i))}
              >
                Xóa
              </Button>
            </div>
          ))}

          <div className="flex flex-wrap items-center gap-3">
            <Button
              size="sm" variant="outline"
              onClick={() => setTieuChi((cu) => [...cu, { description: "", max_points: 1 }])}
            >
              + Thêm tiêu chí
            </Button>

            {/* Rubric tổng 12 điểm cho bài thang 10 là lỗi rất dễ mắc và chỉ lộ ra lúc đang
                chấm dở — cảnh báo ngay tại đây thay vì để backend từ chối sau khi gõ xong. */}
            <span className={`text-sm ${tong > 10 ? "text-destructive" : "text-muted-foreground"}`}>
              Tổng: {tong} / 10
              {tong > 10 && " — vượt thang điểm"}
            </span>
          </div>

          <Button
            disabled={tao.isPending || !ten || tong > 10 || tieuChi.length === 0}
            onClick={() => tao.mutate()}
          >
            {tao.isPending ? "Đang tạo…" : "Tạo rubric"}
          </Button>
        </CardContent>
      </Card>

      {rubrics.length > 0 && (
        <div className="flex flex-col gap-3">
          {rubrics.map((r) => (
            <Card key={r.id}>
              <CardHeader className="flex flex-row items-center justify-between gap-2">
                <CardTitle className="text-base">{r.name}</CardTitle>
                <Badge variant="secondary">{r.max_points} điểm</Badge>
              </CardHeader>
              <CardContent className="flex flex-col gap-1 text-sm">
                {r.criteria.map((c) => (
                  <div key={c.id} className="flex justify-between gap-2">
                    <span>{c.description}</span>
                    <span className="text-muted-foreground font-mono">{c.max_points}</span>
                  </div>
                ))}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
