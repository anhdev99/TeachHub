import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { PageHeader, PageShell } from "@/components/shell/page-shell";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { MyAssignmentDto, MyAssignmentSubmissionDto } from "@/lib/api/types";
import { TrangNopBai } from "./trang-nop-bai";

export default async function SubmitPage({
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

  const headers = { authorization: `Bearer ${token}` };

  const res = await fetch(`${API_BASE}/api/assignments/${id}/my-submission`, {
    headers, cache: "no-store",
  });

  if (res.status === 404) notFound();
  if (!res.ok) redirect("/login");

  const baiNop: MyAssignmentSubmissionDto = await res.json();

  // Cấu hình slot đi kèm danh sách bài tập của lớp; không có offering trên URL thì dò qua
  // các lớp mình học. Sinh viên một lớp là ca thường gặp nên vòng dò này gần như luôn một bước.
  const dsLop: { id: string }[] = offering
    ? [{ id: offering }]
    : await fetch(`${API_BASE}/api/me/offerings`, { headers, cache: "no-store" })
        .then((r) => (r.ok ? r.json() : []))
        .catch(() => []);

  let baiTap: MyAssignmentDto | undefined;
  let offeringId = offering ?? "";

  for (const lop of dsLop) {
    const r = await fetch(`${API_BASE}/api/offerings/${lop.id}/assignments`, {
      headers, cache: "no-store",
    });
    if (!r.ok) continue;

    const ds: MyAssignmentDto[] = await r.json();
    const tim = ds.find((a) => a.id === id);
    if (tim) {
      baiTap = tim;
      offeringId = lop.id;
      break;
    }
  }

  if (!baiTap) notFound();

  // Mốc "bây giờ" đọc ở MÁY CHỦ rồi truyền xuống: trang render với `cache: "no-store"` nên
  // luôn mới, và client không phải đọc lại đồng hồ (đọc lại là lệch hydrate — xem chú thích
  // của prop `quaHan`).
  // eslint-disable-next-line react-hooks/purity -- server component render một lần mỗi request
  const quaHan = baiTap.due_at !== null && new Date(baiTap.due_at).getTime() < Date.now();

  return (
    <PageShell>
      <PageHeader
        title={baiTap.title}
        description={baiTap.description}
        backTo={{ href: `/learn/${offeringId}/assignments`, label: "Bài tập" }}
      />

      <TrangNopBai baiTap={baiTap} banDau={baiNop} quaHan={quaHan} />
    </PageShell>
  );
}
