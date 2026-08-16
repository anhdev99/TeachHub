import { FileQuestion } from "lucide-react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { DauTrang, Trang, TrongRong } from "@/components/shell/trang";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { SubjectDto, SubjectVersionDto } from "@/lib/api/types";
import { TrinhSoanThao } from "./trinh-soan-thao";

/**
 * Màn hình 6.2 — trình soạn thảo chương. Server component chỉ nạp dữ liệu khung
 * (môn + phiên bản) rồi giao phần tương tác cho client component.
 */
export default async function EditorPage({
  params,
}: {
  params: Promise<{ id: string; versionId: string }>;
}) {
  const { id, versionId } = await params;

  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const headers = { authorization: `Bearer ${token}` };
  const [subjectRes, versionsRes] = await Promise.all([
    fetch(`${API_BASE}/api/subjects/${id}`, { headers, cache: "no-store" }),
    fetch(`${API_BASE}/api/subjects/${id}/versions`, { headers, cache: "no-store" }),
  ]);

  if (!subjectRes.ok || !versionsRes.ok) return <KhongThayPhienBan subjectId={id} />;

  const subject: SubjectDto = await subjectRes.json();
  const versions: SubjectVersionDto[] = await versionsRes.json();
  const version = versions.find((v) => v.id === versionId);

  if (!version) return <KhongThayPhienBan subjectId={id} />;

  return <TrinhSoanThao subject={subject} version={version} />;
}

/**
 * Backend trả 404 chứ không bao giờ 403 cho tài nguyên của giảng viên khác, nên màn hình
 * này chỉ được nói "không tìm thấy". Viết "bạn không có quyền" là xác nhận id có tồn tại —
 * đúng thứ backend cố ý giấu.
 */
function KhongThayPhienBan({ subjectId }: { subjectId: string }) {
  return (
    <Trang>
      <DauTrang
        tieuDe="Không tìm thấy phiên bản"
        quayLai={{ href: `/subjects/${subjectId}/versions`, nhan: "Lịch sử phiên bản" }}
      />
      <TrongRong
        Icon={FileQuestion}
        tieuDe="Không tìm thấy phiên bản"
        moTa="Phiên bản này không tồn tại hoặc không thuộc tài khoản của bạn."
      />
    </Trang>
  );
}
