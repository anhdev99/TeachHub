import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { DauTrang, Trang } from "@/components/shell/trang";
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
    // `rong` vì đây là màn hình duy nhất phải hiển thị ĐỒNG THỜI mã nguồn của sinh viên và
    // bảng tiêu chí. Ép nó vào bề rộng mặc định là buộc giảng viên cuộn qua cuộn lại giữa thứ
    // đang đọc và ô đang gõ — và đó là thao tác lặp lại 40 lần một buổi.
    <Trang rong>
      <DauTrang
        tieuDe="Chấm bài"
        moTa="Đọc bài nộp ở bên trái, cho điểm theo tiêu chí ở bên phải."
        quayLai={{
          href: `/offerings/${bai.offering_id}/grading`,
          nhan: "Danh sách bài nộp",
        }}
      />

      <TrangChamBai banDau={bai} rubrics={rubrics} nhom={nhomCuaBai} />
    </Trang>
  );
}
