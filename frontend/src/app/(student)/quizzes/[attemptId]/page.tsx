import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { PageHeader, PageShell } from "@/components/shell/page-shell";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { TakeAttemptDto } from "@/lib/api/types";
import { LamBai } from "./lam-bai";

export default async function TakeQuizPage({
  params,
  searchParams,
}: {
  params: Promise<{ attemptId: string }>;
  searchParams: Promise<{ offering?: string }>;
}) {
  const { attemptId } = await params;
  const { offering } = await searchParams;

  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/quiz-attempts/${attemptId}`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (res.status === 404) notFound();
  if (!res.ok) redirect("/login");

  const luot: TakeAttemptDto = await res.json();

  return (
    <PageShell>
      <PageHeader
        title={luot.title}
        description={luot.description}
        backTo={
          offering ? { href: `/learn/${offering}/quizzes`, label: "Trắc nghiệm" } : undefined
        }
      />

      <LamBai banDau={luot} />
    </PageShell>
  );
}
