import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { PageHeader, PageShell } from "@/components/shell/page-shell";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { QuizQuestionDto } from "@/lib/api/types";
import { QuestionBank } from "./question-bank";

export default async function QuestionsPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/questions`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (!res.ok) redirect("/login");

  const questions: QuizQuestionDto[] = await res.json();

  return (
    <PageShell>
      <PageHeader
        title="Ngân hàng câu hỏi"
        description="Câu hỏi thuộc về bạn chứ không thuộc một môn — dùng lại được cho mọi bài trắc nghiệm của mọi lớp."
      />

      <QuestionBank initial={questions} />
    </PageShell>
  );
}
