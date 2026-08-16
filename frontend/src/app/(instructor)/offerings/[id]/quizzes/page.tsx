import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { PageHeader, PageShell } from "@/components/shell/page-shell";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { QuizDto, QuizQuestionDto } from "@/lib/api/types";
import { SoanQuiz } from "./soan-quiz";

export default async function OfferingQuizzesPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const headers = { authorization: `Bearer ${token}` };

  const res = await fetch(`${API_BASE}/api/quizzes?offering_id=${id}`, {
    headers, cache: "no-store",
  });

  if (res.status === 404) notFound();
  if (!res.ok) redirect("/login");

  const quiz: QuizDto[] = await res.json();

  const cau: QuizQuestionDto[] = await fetch(`${API_BASE}/api/questions`, {
    headers, cache: "no-store",
  }).then((r) => (r.ok ? r.json() : [])).catch(() => []);

  return (
    <PageShell>
      <PageHeader
        backTo={{ href: `/offerings/${id}`, label: "Lớp học phần" }}
        title="Trắc nghiệm"
        description={
          <>
            Câu hỏi lấy từ{" "}
            <Link href="/questions" className="text-foreground underline underline-offset-2">
              ngân hàng câu hỏi
            </Link>{" "}
            của bạn — soạn một lần, dùng cho mọi lớp.
          </>
        }
      />

      <SoanQuiz offeringId={id} banDau={quiz} nganHang={cau} />
    </PageShell>
  );
}
