import { ChevronRight, ScanSearch } from "lucide-react";
import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { DauTrang, Trang, TrongRong } from "@/components/shell/trang";
import { CanhBaoTrungLap } from "@/components/submission/canh-bao-trung-lap";
import { diem } from "@/lib/dinh-dang";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { components } from "@/lib/api/schema";

type SimilarityReportDto = components["schemas"]["SimilarityReportDto"];

export default async function SimilarityPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ offering?: string }>;
}) {
  const { id } = await params;
  const { offering } = await searchParams;

  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");
  if (!offering) notFound();

  const res = await fetch(
    `${API_BASE}/api/assignments/${id}/similarity?offering_id=${offering}`,
    { headers: { authorization: `Bearer ${token}` }, cache: "no-store" },
  );

  if (res.status === 404) notFound();
  if (!res.ok) redirect("/login");

  const bc: SimilarityReportDto = await res.json();

  return (
    <Trang>
      <DauTrang
        tieuDe="Báo cáo trùng lặp mã nguồn"
        moTa={`So ${bc.submissions_compared} bài nộp của lớp, ngưỡng cảnh báo ${diem(bc.threshold_percent)}%.`}
        quayLai={{ href: `/offerings/${offering}/grading`, nhan: "Bài nộp" }}
      />

      <CanhBaoTrungLap />

      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-x-3 gap-y-2">
          <CardTitle className="text-base">Các cặp giống nhau nhất</CardTitle>
          {/* Hổ phách chứ không đỏ: con số này mở một cuộc đọc bài, không mở một vụ kỷ luật. */}
          <Badge
            className={
              bc.warning_count > 0
                ? "bg-warn-soft text-warn border-warn/25 border"
                : "bg-ok-soft text-ok border-ok/25 border"
            }
          >
            {bc.warning_count > 0
              ? `${bc.warning_count} cặp vượt ngưỡng`
              : "Không cặp nào vượt ngưỡng"}
          </Badge>
        </CardHeader>

        <CardContent className="flex flex-col gap-3">
          {bc.submissions_without_fingerprints > 0 && (
            // Không nói ra thì "0 cặp" bị đọc thành "cả lớp trong sạch", trong khi thật ra
            // một phần lớp còn chưa được đem ra so.
            <p className="text-muted-foreground text-sm text-pretty">
              {bc.submissions_without_fingerprints} bài chưa băm xong hoặc không có mã nguồn nên
              không nằm trong phép so này.
            </p>
          )}

          {bc.pairs.length === 0 ? (
            <TrongRong
              Icon={ScanSearch}
              tieuDe="Không có cặp nào đáng chú ý"
              moTa="Không cặp bài nộp nào đủ giống để cần bạn đọc đối chiếu. Bài nộp mới sẽ được so lại sau khi băm xong."
            />
          ) : (
            <ul className="flex flex-col gap-1.5">
              {bc.pairs.map((c) => (
                <li key={`${c.submission_a_id}-${c.submission_b_id}`}>
                  {/* Chuyển tiếp cả tên hai bên: API so sánh chỉ trả id, mà cột mã nguồn
                      không ghi rõ của ai thì giảng viên đọc xong vẫn phải quay lại đây dò. */}
                  <Link
                    href={
                      `/submissions/${c.submission_a_id}/compare/${c.submission_b_id}` +
                      `?offering=${offering}&assignment=${id}` +
                      `&a=${encodeURIComponent(c.owner_a)}&b=${encodeURIComponent(c.owner_b)}`
                    }
                    className="hover:bg-accent group flex items-center gap-3 rounded-lg px-3 py-2.5 transition-colors"
                  >
                    <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span className="flex flex-wrap items-center gap-x-1.5 font-medium">
                        <span className="truncate">{c.owner_a}</span>
                        <span aria-hidden>↔</span>
                        <span className="truncate">{c.owner_b}</span>
                      </span>
                      <span className="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-0.5 text-sm">
                        <span>Jaccard {diem(c.jaccard_percent)}%</span>
                        <span aria-hidden>·</span>
                        <span className="font-mono text-xs">
                          {c.shared_fingerprints}/{c.total_a}
                        </span>
                        <span>đoạn chung</span>
                      </span>
                    </span>

                    <Badge
                      className={`shrink-0 border tabular-nums ${
                        c.is_warning
                          ? "bg-warn-soft text-warn border-warn/25"
                          : "bg-card text-muted-foreground border-border"
                      }`}
                    >
                      {diem(c.similarity_percent)}% trùng
                    </Badge>

                    <ChevronRight
                      className="text-muted-foreground group-hover:text-foreground size-4 shrink-0 transition-colors"
                      aria-hidden
                    />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </Trang>
  );
}
