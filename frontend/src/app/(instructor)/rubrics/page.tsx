import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { RubricDto } from "@/lib/api/types";
import { SoanRubric } from "./soan-rubric";

export default async function RubricsPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/rubrics`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (!res.ok) redirect("/login");

  const rubrics: RubricDto[] = await res.json();

  return (
    <main className="mx-auto flex min-h-svh max-w-3xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-col gap-1">
        <Link href="/offerings" className="text-muted-foreground hover:text-foreground text-sm">
          ← Lớp học phần
        </Link>
        <h1 className="text-2xl font-semibold">Rubric chấm điểm</h1>
        <p className="text-muted-foreground text-sm">
          Rubric thuộc về bạn chứ không thuộc về một môn — dùng lại được cho mọi bài của mọi lớp.
        </p>
      </div>

      <SoanRubric banDau={rubrics} />
    </main>
  );
}
