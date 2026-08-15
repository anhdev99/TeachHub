import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { GroupDto, RubricDto, SubmissionDto } from "@/lib/api/types";
import { TrangChamBai } from "./trang-cham-bai";

export default async function GradingPage({
  params,
}: {
  params: Promise<{ submissionId: string }>;
}) {
  const { submissionId } = await params;
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const headers = { authorization: `Bearer ${token}` };

  const res = await fetch(`${API_BASE}/api/submissions/${submissionId}`, {
    headers, cache: "no-store",
  });

  if (res.status === 404) notFound();
  if (!res.ok) redirect("/login");

  const bai: SubmissionDto = await res.json();

  // Tên bài tập + rubric + nhóm nạp song song: màn hình này mở giữa hai lượt báo cáo trên
  // lớp, nên mỗi vòng round-trip nối tiếp là thời gian giảng viên đứng chờ trước sinh viên.
  const [rubrics, nhom] = await Promise.all([
    fetch(`${API_BASE}/api/rubrics`, { headers, cache: "no-store" })
      .then((r) => (r.ok ? (r.json() as Promise<RubricDto[]>) : []))
      .catch(() => [] as RubricDto[]),
    fetch(`${API_BASE}/api/offerings/${bai.offering_id}/groups`, { headers, cache: "no-store" })
      .then((r) => (r.ok ? (r.json() as Promise<GroupDto[]>) : []))
      .catch(() => [] as GroupDto[]),
  ]);

  const nhomCuaBai = bai.group_id ? nhom.find((g) => g.id === bai.group_id) : undefined;

  return (
    <main className="flex min-h-svh flex-col">
      <header className="border-border flex flex-wrap items-center gap-3 border-b p-3 sm:p-4">
        <Link
          href={`/offerings/${bai.offering_id}/grading`}
          className="text-muted-foreground hover:text-foreground text-sm"
        >
          ← Danh sách bài nộp
        </Link>
        <span className="font-medium">Chấm bài</span>
      </header>

      <TrangChamBai banDau={bai} rubrics={rubrics} nhom={nhomCuaBai} />
    </main>
  );
}
