import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
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
    <main className="mx-auto flex min-h-svh max-w-3xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-col gap-1">
        <Link
          href={`/learn/${offeringId}`}
          className="text-muted-foreground hover:text-foreground text-sm"
        >
          ← Trang học
        </Link>
        <h1 className="text-2xl font-semibold">Trắc nghiệm</h1>
      </div>

      <DanhSachQuiz offeringId={offeringId} banDau={ds} />
    </main>
  );
}
