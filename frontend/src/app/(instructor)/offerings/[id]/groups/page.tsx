import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { DauTrang, Trang } from "@/components/shell/trang";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { EnrollmentDto, GroupDto } from "@/lib/api/types";
import { ChiaNhom } from "./chia-nhom";

export default async function GroupsPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const headers = { authorization: `Bearer ${token}` };

  const [resNhom, resSv] = await Promise.all([
    fetch(`${API_BASE}/api/offerings/${id}/groups`, { headers, cache: "no-store" }),
    fetch(`${API_BASE}/api/offerings/${id}/enrollments`, { headers, cache: "no-store" }),
  ]);

  if (resNhom.status === 404) notFound();
  if (!resNhom.ok) redirect("/login");

  const nhom: GroupDto[] = await resNhom.json();
  const sinhVien: EnrollmentDto[] = resSv.ok ? await resSv.json() : [];

  return (
    <Trang>
      <DauTrang
        quayLai={{ href: `/offerings/${id}`, nhan: "Lớp học phần" }}
        tieuDe="Chia nhóm"
        moTa="Chia ngẫu nhiên theo sĩ số, rồi kéo-thả từng sinh viên để chỉnh lại."
      />

      {/* Truyền cả danh sách sinh viên chứ không chỉ sĩ số: màn hình cần biết AI chưa có nhóm
          để dựng vùng thả "Chưa có nhóm". Chỉ có con số thì kéo một sinh viên ra khỏi nhóm
          xong là người đó biến mất khỏi màn hình. */}
      <ChiaNhom offeringId={id} banDau={nhom} sinhVien={sinhVien} />
    </Trang>
  );
}
