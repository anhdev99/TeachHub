import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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
    <main className="mx-auto flex min-h-svh max-w-4xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-col gap-1">
        <Link
          href={`/offerings/${offering}/grading`}
          className="text-muted-foreground hover:text-foreground text-sm"
        >
          ← Bài nộp
        </Link>
        <h1 className="text-2xl font-semibold">Báo cáo trùng lặp mã nguồn</h1>
      </div>

      {/* Cảnh báo, KHÔNG BAO GIỜ tự trừ điểm (Rủi ro #3 của SRS). Con số ở đây là gợi ý để
          giảng viên đọc bài, không phải bằng chứng — bài nhập môn dùng chung starter code
          trùng 70–90% một cách hoàn toàn chính đáng. */}
      <div className="border-border rounded-lg border border-l-4 border-l-amber-500 p-3 text-sm">
        Đây là <strong>gợi ý để đọc kỹ</strong>, không phải kết luận. Hệ thống không bao giờ tự
        trừ điểm. Bài dùng chung mã khởi đầu sẽ trùng cao một cách chính đáng — hãy mở bài ra
        xem trước khi kết luận.
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            {bc.warning_count} cặp vượt ngưỡng {bc.threshold_percent}%
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <p className="text-muted-foreground text-sm">
            So {bc.submissions_compared} bài nộp.
            {bc.submissions_without_fingerprints > 0 &&
              ` ${bc.submissions_without_fingerprints} bài chưa băm xong hoặc không có mã nguồn.`}
          </p>

          {bc.pairs.length === 0 ? (
            <p className="text-muted-foreground text-sm">
              Không có cặp nào đáng chú ý.
            </p>
          ) : (
            <ul className="flex flex-col gap-2">
              {bc.pairs.map((c) => (
                <li
                  key={`${c.submission_a_id}-${c.submission_b_id}`}
                  className={`border-border flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 ${
                    c.is_warning ? "border-l-4 border-l-destructive" : ""
                  }`}
                >
                  <span className="text-sm">
                    <strong>{c.owner_a}</strong> ↔ <strong>{c.owner_b}</strong>
                  </span>
                  <span className="flex flex-wrap items-center gap-2">
                    <Badge variant={c.is_warning ? "destructive" : "secondary"}>
                      {c.similarity_percent}% trùng
                    </Badge>
                    <span className="text-muted-foreground text-xs">
                      Jaccard {c.jaccard_percent}% · {c.shared_fingerprints}/{c.total_a} đoạn chung
                    </span>
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </main>
  );
}
