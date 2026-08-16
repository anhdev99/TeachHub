import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { DauTrang, Trang } from "@/components/shell/trang";
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
    // `rong`: sổ điểm là bảng nhiều cột, đây đúng là màn hình cần bề ngang. Bề rộng do
    // <Trang> quyết định chứ không phải mỗi trang tự khai một kiểu.
    <Trang rong>
      <DauTrang
        quayLai={{ href: `/offerings/${id}`, nhan: "Lớp học phần" }}
        tieuDe="Sổ điểm"
        moTa={
          <span className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
            <span className="font-mono text-xs">{so.subject_code}</span>
            <span aria-hidden>·</span>
            <span className="font-mono text-xs">{so.class_code}</span>
            <span aria-hidden>·</span>
            <span>{so.subject_name}</span>
          </span>
        }
      />

      <SoDiem
        offeringId={id}
        banDau={so}
        nguon={[
          ...baiTap.map((a) => ({ type: "assignment" as const, id: a.id, title: a.title })),
          ...cuoiMon.map((f) => ({ type: "final_assessment" as const, id: f.id, title: f.title })),
        ]}
      />
    </Trang>
  );
}
