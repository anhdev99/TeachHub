import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
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
    <main className="mx-auto flex min-h-svh max-w-3xl flex-col gap-5 p-4 sm:p-6">
      <div className="flex flex-col gap-1">
        {offering && (
          <Link
            href={`/learn/${offering}/quizzes`}
            className="text-muted-foreground hover:text-foreground text-sm"
          >
            ← Trắc nghiệm
          </Link>
        )}
        <h1 className="text-2xl font-semibold">{luot.title}</h1>
        {luot.description && (
          <p className="text-muted-foreground text-sm">{luot.description}</p>
        )}
      </div>

      <LamBai banDau={luot} />
    </main>
  );
}
