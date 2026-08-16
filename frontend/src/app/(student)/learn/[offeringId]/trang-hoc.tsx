"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { apiFetch } from "@/lib/api/fetcher";
import type { MyLessonDto, MyProgressDto } from "@/lib/api/types";
import { PhanBaiHoc } from "./phan-bai-hoc";
import { XemNoiDung } from "./xem-noi-dung";

export function TrangHoc({
  offeringId,
  banDau,
}: {
  offeringId: string;
  banDau: MyProgressDto;
}) {
  const qc = useQueryClient();
  const khoa = ["my-progress", offeringId] as const;
  const [dangChon, setDangChon] = useState<string | null>(null);
  const [moSidebar, setMoSidebar] = useState(false);

  const { data } = useQuery({
    queryKey: khoa,
    queryFn: () => apiFetch<MyProgressDto>(`/api/offerings/${offeringId}/my-progress`),
    initialData: banDau,
  });

  const hoanThanh = useMutation({
    mutationFn: (lessonId: string) =>
      apiFetch(`/api/lessons/${lessonId}/complete`, { method: "POST" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: khoa }),
  });

  const hoanThanhPhan = useMutation({
    mutationFn: (componentId: string) =>
      apiFetch(`/api/lesson-components/${componentId}/complete`, { method: "POST" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: khoa }),
  });

  const tatCaBai = useMemo(
    () => data.chapters.flatMap((c) => c.lessons.map((l) => ({ ...l, chapterId: c.id }))),
    [data],
  );

  // Bài đang xem: bài được chọn, hoặc bài mở đầu tiên chưa hoàn thành.
  const bai: (MyLessonDto & { chapterId: string }) | undefined =
    tatCaBai.find((l) => l.id === dangChon) ??
    tatCaBai.find((l) => l.access === "unlocked" && !l.completed) ??
    tatCaBai.find((l) => l.access === "unlocked");

  return (
    <div className="flex min-h-svh flex-col">
      <header className="border-border flex flex-wrap items-center gap-3 border-b p-3 sm:p-4">
        <Link href="/learn" className="text-muted-foreground hover:text-foreground text-sm">
          ← Lớp của tôi
        </Link>
        <span className="font-medium">
          <span className="text-muted-foreground font-mono">{data.subject_code}</span>{" "}
          {data.subject_name}
        </span>
        <Link
          href={`/learn/${offeringId}/assignments`}
          className="text-muted-foreground hover:text-foreground ml-auto text-sm"
        >
          Bài tập
        </Link>

        <Link
          href={`/learn/${offeringId}/quizzes`}
          className="text-muted-foreground hover:text-foreground text-sm"
        >
          Trắc nghiệm
        </Link>

        <Link
          href={`/learn/${offeringId}/tien-do`}
          className="text-muted-foreground hover:text-foreground text-sm"
        >
          Tiến độ
        </Link>

        <Badge variant="secondary">Hoàn thành {data.overall_percent}%</Badge>

        {/* Dưới 768px sidebar thu vào drawer — SRS mục 9: SV hay xem trên điện thoại. */}
        <Button
          size="sm"
          variant="outline"
          className="md:hidden"
          onClick={() => setMoSidebar((v) => !v)}
        >
          {moSidebar ? "Ẩn mục lục" : "Mục lục"}
        </Button>
      </header>

      <div className="flex flex-1 flex-col md:flex-row">
        <aside
          className={`border-border w-full shrink-0 border-b p-3 md:block md:w-72 md:border-r md:border-b-0 ${
            moSidebar ? "block" : "hidden"
          }`}
        >
          <nav className="flex flex-col gap-4">
            {data.chapters.map((c) => (
              <div key={c.id} className="flex flex-col gap-1">
                <div className="flex items-baseline justify-between gap-2">
                  <h2 className="text-sm font-medium">{c.title}</h2>
                  <span className="text-muted-foreground text-xs tabular-nums">
                    {c.completion_percent}%
                  </span>
                </div>

                <ul className="flex flex-col">
                  {c.lessons.map((l) => {
                    const khoaLai = l.access === "locked";
                    return (
                      <li key={l.id}>
                        <button
                          type="button"
                          disabled={khoaLai}
                          onClick={() => {
                            setDangChon(l.id);
                            setMoSidebar(false);
                          }}
                          className={`flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm ${
                            khoaLai
                              ? "text-muted-foreground cursor-not-allowed"
                              : "hover:bg-muted"
                          } ${bai?.id === l.id ? "bg-muted font-medium" : ""}`}
                        >
                          {/* Bốn trạng thái, không phải hai: khóa · hoàn thành · đã xem
                              nhưng còn phần bắt buộc · chưa động tới. Gộp hai trạng thái
                              giữa lại thành ✓ là lý do sinh viên từng thấy bài đã ✓ mà bài
                              kế tiếp vẫn khóa vì "chưa hoàn thành" chính bài đó. */}
                          <span aria-hidden>
                            {khoaLai ? "🔒" : l.completed ? "✓" : l.marked_done ? "◐" : "○"}
                          </span>
                          <span className="flex-1">{l.title}</span>
                        </button>
                      </li>
                    );
                  })}
                </ul>
              </div>
            ))}
          </nav>
        </aside>

        <main className="flex-1 p-4 sm:p-6">
          {!bai ? (
            <p className="text-muted-foreground text-sm">Lớp này chưa có bài học nào.</p>
          ) : bai.access === "locked" ? (
            <div className="border-border mx-auto max-w-xl rounded-lg border p-6 text-center">
              <p className="text-2xl" aria-hidden>🔒</p>
              <h2 className="mt-2 font-medium">{bai.title}</h2>
              {/* Không bao giờ chỉ hiện ổ khóa: luôn nói rõ phải làm gì để mở. */}
              <p className="text-muted-foreground mt-2 text-sm">
                {bai.locked_reason ?? "Bài này chưa mở."}
              </p>
            </div>
          ) : (
            <article className="mx-auto flex max-w-3xl flex-col gap-4">
              <h1 className="text-xl font-semibold">{bai.title}</h1>

              <XemNoiDung contentType={bai.content_type} fileUrl={bai.file_url} />

              {bai.components.length > 0 && (
                <section className="flex flex-col gap-2">
                  <h2 className="text-sm font-medium">Các phần trong bài</h2>
                  <ul className="flex flex-col gap-2">
                    {bai.components.map((p) => (
                      <PhanBaiHoc
                        key={p.id}
                        phan={p}
                        offeringId={offeringId}
                        dangLuu={hoanThanhPhan.isPending && hoanThanhPhan.variables === p.id}
                        onHoanThanh={(id) => hoanThanhPhan.mutate(id)}
                      />
                    ))}
                  </ul>
                </section>
              )}

              <div className="border-border flex flex-wrap items-center justify-between gap-3 border-t pt-4">
                <span className="text-muted-foreground text-sm">
                  {bai.component_count > 0 ? `${bai.component_count} phần trong bài` : "Không có phần thêm"}
                </span>

                {bai.completed ? (
                  <Badge>Đã hoàn thành</Badge>
                ) : bai.marked_done ? (
                  // Đã bấm "Đã học xong" nhưng bài vẫn chưa tính là hoàn thành. Phải nói rõ
                  // còn thiếu gì: im lặng ở đây là sinh viên bấm nút, không thấy gì đổi, rồi
                  // nhìn bài kế tiếp vẫn khóa mà không hiểu vì sao.
                  <Badge variant="secondary">
                    Đã xem xong nội dung — còn {bai.required_components_remaining} phần bắt buộc
                  </Badge>
                ) : (
                  <Button
                    onClick={() => hoanThanh.mutate(bai.id)}
                    disabled={hoanThanh.isPending}
                  >
                    {hoanThanh.isPending ? "Đang lưu…" : "Đã học xong"}
                  </Button>
                )}
              </div>
            </article>
          )}
        </main>
      </div>
    </div>
  );
}
