"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  BookOpen,
  ChevronLeft,
  ChevronRight,
  Circle,
  CircleCheck,
  CircleDashed,
  ClipboardList,
  FileText,
  ListTree,
  Lock,
  TrendingUp,
} from "lucide-react";
import Link from "next/link";
import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { EmptyState } from "@/components/shell/page-shell";
import { apiFetch } from "@/lib/api/fetcher";
import type { MyChapterDto, MyLessonDto, MyProgressDto } from "@/lib/api/types";
import { cn } from "@/lib/utils";
import { PhanBaiHoc } from "./phan-bai-hoc";
import { XemNoiDung } from "./xem-noi-dung";

type BaiPhang = MyLessonDto & { chapterId: string; chapterTitle: string };

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
  const [moMucLuc, setMoMucLuc] = useState(false);

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

  const tatCaBai = useMemo<BaiPhang[]>(
    () =>
      data.chapters.flatMap((c) =>
        c.lessons.map((l) => ({ ...l, chapterId: c.id, chapterTitle: c.title })),
      ),
    [data],
  );

  // Bài đang xem: bài được chọn, hoặc bài mở đầu tiên chưa hoàn thành.
  const bai: BaiPhang | undefined =
    tatCaBai.find((l) => l.id === dangChon) ??
    tatCaBai.find((l) => l.access === "unlocked" && !l.completed) ??
    tatCaBai.find((l) => l.access === "unlocked");

  // Điều hướng trước/sau chỉ đi qua các bài ĐÃ MỞ. Cho nút "Bài tiếp" nhảy vào một bài khóa
  // là hứa một việc rồi hiện ổ khóa — thà không có nút.
  const baiDaMo = tatCaBai.filter((l) => l.access === "unlocked");
  const viTri = bai ? baiDaMo.findIndex((l) => l.id === bai.id) : -1;
  const baiTruoc = viTri > 0 ? baiDaMo[viTri - 1] : undefined;
  const baiSau = viTri >= 0 && viTri < baiDaMo.length - 1 ? baiDaMo[viTri + 1] : undefined;

  const soBaiXong = tatCaBai.filter((l) => l.completed).length;
  const phanTram = Math.round(data.overall_percent);

  function chonBai(id: string) {
    setDangChon(id);
    setMoMucLuc(false);
    // Đổi bài mà giữ nguyên vị trí cuộn là mở bài mới ở giữa trang; trên điện thoại người
    // học tưởng bấm hụt vì phần đầu bài nằm ngoài màn hình.
    window.scrollTo({ top: 0 });
  }

  const mucLuc = (
    <MucLuc
      offeringId={offeringId}
      chapters={data.chapters}
      baiDangXem={bai?.id}
      phanTram={phanTram}
      soBaiXong={soBaiXong}
      tongBai={tatCaBai.length}
      onChon={chonBai}
    />
  );

  return (
    <div className="flex min-h-svh flex-col">
      {/* ── Thanh riêng của trang học ─────────────────────────────────────────────────────
       * Thanh trên dùng chung tự ẩn ở route này, nên thanh này là điều hướng duy nhất. Giữ
       * ĐÚNG MỘT hàng cao 56px: bản cũ xuống ba hàng trên điện thoại, ăn mất 110px chiều dọc
       * trước khi hiện được chữ đầu tiên của bài học. Các đường đi phụ (Bài tập, Trắc nghiệm,
       * Tiến độ) chuyển vào mục lục. */}
      <header className="bg-card/95 border-border sticky top-0 z-30 border-b backdrop-blur">
        <div className="flex h-14 items-center gap-2 px-2 sm:px-4">
          <Link
            href="/learn"
            aria-label="Lớp của tôi"
            title="Lớp của tôi"
            className="text-muted-foreground hover:bg-accent hover:text-foreground grid size-10 shrink-0 place-items-center rounded-lg transition-colors"
          >
            <ChevronLeft className="size-5" aria-hidden />
          </Link>

          <div className="flex min-w-0 flex-1 flex-col">
            <span className="truncate text-sm leading-tight font-medium">{data.subject_name}</span>
            <span className="text-muted-foreground truncate font-mono text-xs leading-tight">
              {data.subject_code} · {data.class_code}
            </span>
          </div>

          <span
            className="text-sm font-semibold tabular-nums"
            title={`Đã hoàn thành ${soBaiXong}/${tatCaBai.length} bài`}
          >
            {phanTram}%
          </span>

          {/* Dưới 768px mục lục thu vào ngăn kéo — một cột cố định 288px trên màn 390px là
              lấy ba phần tư bề ngang cho thứ chỉ dùng lúc chuyển bài. */}
          <Button
            variant="outline"
            className="min-h-10 shrink-0 md:hidden"
            onClick={() => setMoMucLuc(true)}
          >
            <ListTree aria-hidden />
            Mục lục
          </Button>
        </div>

        {/* Thanh tiến độ mảnh: luôn thấy được mà không tốn thêm một hàng nào. */}
        <Progress value={phanTram} className="h-0.5 rounded-none" />
      </header>

      <Sheet open={moMucLuc} onOpenChange={setMoMucLuc}>
        <SheetContent side="left" className="w-[86vw] gap-0 p-0 sm:max-w-sm">
          <SheetHeader className="border-border border-b">
            <SheetTitle>Mục lục</SheetTitle>
            <SheetDescription>{data.subject_name}</SheetDescription>
          </SheetHeader>
          <div className="min-h-0 flex-1 overflow-y-auto p-3">{mucLuc}</div>
        </SheetContent>
      </Sheet>

      <div className="flex flex-1 flex-col md:flex-row">
        <aside className="border-border hidden w-72 shrink-0 border-r md:block lg:w-80">
          <div className="sticky top-14 max-h-[calc(100svh-3.5rem)] overflow-y-auto p-3">
            {mucLuc}
          </div>
        </aside>

        <main className="min-w-0 flex-1 p-4 sm:p-6 lg:p-8">
          {/* max-w ở ĐÂY là bề rộng dòng chữ để đọc được, không phải bề rộng trang: quá 75 ký
              tự một dòng thì mắt bị lạc dòng khi xuống hàng. */}
          <div className="mx-auto w-full max-w-3xl">
            {!bai ? (
              <EmptyState
                Icon={BookOpen}
                title="Lớp này chưa có bài học nào"
                description="Giảng viên chưa xuất bản nội dung cho lớp. Bài học sẽ hiện ở đây ngay khi có."
              />
            ) : bai.access === "locked" ? (
              // Không bao giờ chỉ hiện ổ khóa: luôn nói rõ phải làm gì để mở.
              <EmptyState
                Icon={Lock}
                title={bai.title}
                description={bai.locked_reason ?? "Bài này chưa mở."}
              />
            ) : (
              <article className="flex flex-col gap-5">
                <div className="flex flex-col gap-1">
                  <p className="text-muted-foreground text-xs font-medium">{bai.chapterTitle}</p>
                  <h1 className="text-xl font-semibold tracking-tight text-balance sm:text-2xl">
                    {bai.title}
                  </h1>
                </div>

                <XemNoiDung contentType={bai.content_type} fileUrl={bai.file_url} />

                {bai.components.length > 0 && (
                  <section className="flex flex-col gap-2.5">
                    <div className="flex flex-wrap items-center gap-2">
                      <h2 className="text-sm font-semibold">Các phần trong bài</h2>
                      <span className="text-muted-foreground text-sm tabular-nums">
                        {bai.components.filter((p) => p.completed).length}/{bai.components.length}
                      </span>
                      {bai.required_components_remaining > 0 && (
                        <Badge className="bg-warn-soft text-warn border-warn/25 border">
                          còn {bai.required_components_remaining} bắt buộc
                        </Badge>
                      )}
                    </div>

                    <ul className="flex flex-col gap-2.5">
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

                <div className="border-border flex flex-col gap-4 border-t pt-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    {bai.completed ? (
                      <Badge className="bg-ok-soft text-ok border-ok/25 border">
                        <CircleCheck aria-hidden />
                        Đã hoàn thành bài này
                      </Badge>
                    ) : bai.marked_done ? (
                      // Đã bấm "Đã học xong" nhưng bài vẫn chưa tính là hoàn thành. Phải nói rõ
                      // còn thiếu gì: im lặng ở đây là sinh viên bấm nút, không thấy gì đổi, rồi
                      // nhìn bài kế tiếp vẫn khóa mà không hiểu vì sao.
                      <Badge className="bg-warn-soft text-warn border-warn/25 border text-pretty">
                        Đã xem xong nội dung — còn {bai.required_components_remaining} phần bắt buộc
                      </Badge>
                    ) : (
                      <Button
                        onClick={() => hoanThanh.mutate(bai.id)}
                        disabled={hoanThanh.isPending}
                        className="min-h-11 w-full sm:w-auto"
                      >
                        {hoanThanh.isPending ? "Đang lưu…" : "Đã học xong"}
                      </Button>
                    )}
                  </div>

                  {(baiTruoc || baiSau) && (
                    <nav className="flex items-stretch justify-between gap-2">
                      {baiTruoc ? (
                        <button
                          type="button"
                          onClick={() => chonBai(baiTruoc.id)}
                          className="hover:bg-accent border-border flex min-h-11 min-w-0 flex-1 items-center gap-2 rounded-lg border px-3 py-2 text-left transition-colors"
                        >
                          <ChevronLeft className="text-muted-foreground size-4 shrink-0" aria-hidden />
                          <span className="flex min-w-0 flex-col">
                            <span className="text-muted-foreground text-xs">Bài trước</span>
                            <span className="truncate text-sm font-medium">{baiTruoc.title}</span>
                          </span>
                        </button>
                      ) : (
                        <span className="flex-1" />
                      )}

                      {baiSau && (
                        <button
                          type="button"
                          onClick={() => chonBai(baiSau.id)}
                          className="hover:bg-accent border-border flex min-h-11 min-w-0 flex-1 items-center justify-end gap-2 rounded-lg border px-3 py-2 text-right transition-colors"
                        >
                          <span className="flex min-w-0 flex-col">
                            <span className="text-muted-foreground text-xs">Bài tiếp</span>
                            <span className="truncate text-sm font-medium">{baiSau.title}</span>
                          </span>
                          <ChevronRight className="text-muted-foreground size-4 shrink-0" aria-hidden />
                        </button>
                      )}
                    </nav>
                  )}
                </div>
              </article>
            )}
          </div>
        </main>
      </div>
    </div>
  );
}

/**
 * Mục lục — dùng CHUNG cho ngăn kéo (điện thoại) và cột trái (máy tính).
 *
 * Một bản duy nhất chứ không phải hai: hai bản "gần giống nhau" là hai chỗ phải sửa mỗi lần
 * luật hiển thị trạng thái bài đổi, và một trong hai sẽ bị quên.
 */
function MucLuc({
  offeringId,
  chapters,
  baiDangXem,
  phanTram,
  soBaiXong,
  tongBai,
  onChon,
}: {
  offeringId: string;
  chapters: MyChapterDto[];
  baiDangXem?: string;
  phanTram: number;
  soBaiXong: number;
  tongBai: number;
  onChon: (lessonId: string) => void;
}) {
  return (
    <nav className="flex flex-col gap-5">
      <div className="flex flex-col gap-1.5 px-1">
        <div className="flex items-baseline justify-between gap-2 text-sm">
          <span className="font-medium">Tiến độ lớp</span>
          <span className="text-muted-foreground tabular-nums">
            {soBaiXong}/{tongBai} bài · {phanTram}%
          </span>
        </div>
        <Progress value={phanTram} className="h-1.5" />
      </div>

      <ul className="flex flex-col gap-0.5">
        <LienKetLop href={`/learn/${offeringId}/assignments`} Icon={FileText} nhan="Bài tập" />
        <LienKetLop href={`/learn/${offeringId}/quizzes`} Icon={ClipboardList} nhan="Trắc nghiệm" />
        <LienKetLop href={`/learn/${offeringId}/tien-do`} Icon={TrendingUp} nhan="Tiến độ của tôi" />
      </ul>

      <div className="flex flex-col gap-4">
        {chapters.map((c) => {
          const xong = c.lessons.filter((l) => l.completed).length;

          return (
            <div key={c.id} className="flex flex-col gap-1.5">
              <div className="flex flex-col gap-1 px-1">
                <div className="flex items-baseline justify-between gap-2">
                  <h2 className="text-sm font-semibold text-pretty">{c.title}</h2>
                  <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
                    {xong}/{c.lessons.length}
                  </span>
                </div>
                <Progress value={c.completion_percent} className="h-1" />
              </div>

              <ul className="flex flex-col">
                {c.lessons.map((l) => {
                  const khoaLai = l.access === "locked";
                  const dangXem = baiDangXem === l.id;

                  return (
                    <li key={l.id}>
                      <button
                        type="button"
                        disabled={khoaLai}
                        title={khoaLai ? (l.locked_reason ?? "Bài này chưa mở.") : undefined}
                        onClick={() => onChon(l.id)}
                        aria-current={dangXem ? "true" : undefined}
                        className={cn(
                          "flex min-h-11 w-full items-center gap-2.5 rounded-lg px-2 py-2 text-left text-sm transition-colors",
                          dangXem
                            ? "bg-accent text-accent-foreground font-medium"
                            : khoaLai
                              ? "text-muted-foreground cursor-not-allowed"
                              : "hover:bg-muted",
                        )}
                      >
                        <IconTrangThaiBai bai={l} />
                        <span className="min-w-0 flex-1 text-pretty">{l.title}</span>
                        {!khoaLai && !l.completed && l.required_components_remaining > 0 && (
                          <span className="bg-warn-soft text-warn shrink-0 rounded-full px-1.5 py-0.5 text-[0.65rem] font-medium tabular-nums">
                            còn {l.required_components_remaining}
                          </span>
                        )}
                      </button>
                    </li>
                  );
                })}
              </ul>
            </div>
          );
        })}
      </div>
    </nav>
  );
}

function LienKetLop({
  href,
  Icon,
  nhan,
}: {
  href: string;
  Icon: React.ComponentType<{ className?: string }>;
  nhan: string;
}) {
  return (
    <li>
      <Link
        href={href}
        className="hover:bg-accent group flex min-h-11 items-center gap-2.5 rounded-lg px-2 text-sm transition-colors"
      >
        <Icon className="text-muted-foreground size-4 shrink-0" />
        <span className="flex-1">{nhan}</span>
        <ChevronRight
          className="text-muted-foreground group-hover:text-foreground size-4 shrink-0 transition-colors"
          aria-hidden
        />
      </Link>
    </li>
  );
}

/**
 * BỐN trạng thái, không phải hai: khóa · hoàn thành · đã xem nhưng còn phần bắt buộc · chưa
 * động tới. Gộp hai trạng thái giữa lại thành một dấu ✓ là lý do sinh viên từng thấy bài đã ✓
 * mà bài kế tiếp vẫn khóa vì "chưa hoàn thành" chính bài đó.
 *
 * Dùng icon có `aria-label` thay cho emoji: emoji đọc lên bằng trình đọc màn hình thành
 * "dấu kiểm nặng", và mỗi hệ điều hành vẽ một kiểu nên kích thước hàng nhảy lung tung.
 */
function IconTrangThaiBai({ bai }: { bai: MyLessonDto }) {
  if (bai.access === "locked") {
    return <Lock className="text-muted-foreground size-4 shrink-0" aria-label="Chưa mở" />;
  }
  if (bai.completed) {
    return <CircleCheck className="text-ok size-4 shrink-0" aria-label="Đã hoàn thành" />;
  }
  if (bai.marked_done) {
    return (
      <CircleDashed className="text-warn size-4 shrink-0" aria-label="Còn phần bắt buộc" />
    );
  }
  return <Circle className="text-muted-foreground/50 size-4 shrink-0" aria-label="Chưa học" />;
}
