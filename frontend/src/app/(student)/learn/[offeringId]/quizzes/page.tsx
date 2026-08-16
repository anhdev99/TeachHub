import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { PageHeader, PageShell } from "@/components/shell/page-shell";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { MyQuizDto } from "@/lib/api/types";
import { DanhSachQuiz } from "./danh-sach-quiz";

export default async function MyQuizzesPage({
  params,
}: {
  params: Promise<{ offeringId: string }>;
}) {
  const { offeringId } = await params;
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/offerings/${offeringId}/quizzes`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (res.status === 404) notFound();
  if (!res.ok) redirect("/login");

  const ds: MyQuizDto[] = await res.json();

  return (
    <PageShell>
      <PageHeader
        title="Trắc nghiệm"
        description="Bài trắc nghiệm của lớp này và số lượt bạn đã dùng."
        backTo={{ href: `/learn/${offeringId}`, label: "Trang học" }}
      />

      <DanhSachQuiz offeringId={offeringId} banDau={ds} />
    </PageShell>
  );
}
