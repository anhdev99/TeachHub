import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { AssignmentDto, FinalAssessmentDto, GradebookDto } from "@/lib/api/types";
import { SoDiem } from "./so-diem";

export default async function GradebookPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const headers = { authorization: `Bearer ${token}` };

  const res = await fetch(`${API_BASE}/api/offerings/${id}/gradebook`, {
    headers, cache: "no-store",
  });

  if (res.status === 404) notFound();
  if (!res.ok) redirect("/login");

  const so: GradebookDto = await res.json();

  // Nguồn điểm để gắn vào cột: bài tập của phiên bản lớp ghim + đợt cuối môn của lớp.
  const lop = await fetch(`${API_BASE}/api/offerings/${id}`, { headers, cache: "no-store" })
    .then((r) => r.json() as Promise<{ subject_version_id: string }>);

  const [baiTap, cuoiMon] = await Promise.all([
    fetch(`${API_BASE}/api/subject-versions/${lop.subject_version_id}/assignments`, {
      headers, cache: "no-store",
    }).then((r) => (r.ok ? (r.json() as Promise<AssignmentDto[]>) : [])).catch(() => []),
    fetch(`${API_BASE}/api/offerings/${id}/final-assessments`, { headers, cache: "no-store" })
      .then((r) => (r.ok ? (r.json() as Promise<FinalAssessmentDto[]>) : [])).catch(() => []),
  ]);

  return (
    <main className="mx-auto flex min-h-svh max-w-full flex-col gap-5 p-4 sm:p-6">
      <div className="flex flex-col gap-1">
        <Link href={`/offerings/${id}`} className="text-muted-foreground hover:text-foreground text-sm">
          ← Lớp học phần
        </Link>
        <h1 className="text-2xl font-semibold">
          Sổ điểm <span className="text-muted-foreground font-normal">· {so.class_code}</span>
        </h1>
      </div>

      <SoDiem
        offeringId={id}
        banDau={so}
        nguon={[
          ...baiTap.map((a) => ({ type: "assignment" as const, id: a.id, title: a.title })),
          ...cuoiMon.map((f) => ({ type: "final_assessment" as const, id: f.id, title: f.title })),
        ]}
      />
    </main>
  );
}
