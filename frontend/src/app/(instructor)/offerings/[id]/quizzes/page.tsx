import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
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
    <main className="mx-auto flex min-h-svh max-w-3xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-col gap-1">
        <Link href={`/offerings/${id}`} className="text-muted-foreground hover:text-foreground text-sm">
          ← Lớp học phần
        </Link>
        <h1 className="text-2xl font-semibold">Trắc nghiệm</h1>
        <p className="text-muted-foreground text-sm">
          Câu hỏi lấy từ{" "}
          <Link href="/questions" className="underline">ngân hàng câu hỏi</Link> của bạn.
        </p>
      </div>

      <SoanQuiz offeringId={id} banDau={quiz} nganHang={cau} />
    </main>
  );
}
