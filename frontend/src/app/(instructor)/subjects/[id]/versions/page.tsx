import { FileQuestion } from "lucide-react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { DauTrang, Trang, TrongRong } from "@/components/shell/trang";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { SubjectDto, SubjectVersionDto } from "@/lib/api/types";
import { DongThoiGian } from "./dong-thoi-gian";

/**
 * Màn hình 6.10 — lịch sử phiên bản. Timeline dọc, mỗi mốc là một version.
 * Server component: token đọc từ cookie httpOnly và không rời khỏi máy chủ.
 */
export default async function VersionsPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const headers = { authorization: `Bearer ${token}` };
  const [subjectRes, versionsRes] = await Promise.all([
    fetch(`${API_BASE}/api/subjects/${id}`, { headers, cache: "no-store" }),
    fetch(`${API_BASE}/api/subjects/${id}/versions`, { headers, cache: "no-store" }),
  ]);

  // Backend trả 404 cho môn của giảng viên khác (không bao giờ 403) — với người dùng thì
  // "không tồn tại" và "không phải của bạn" là cùng một màn hình.
  if (!subjectRes.ok || !versionsRes.ok) {
    return (
      <Trang>
        <DauTrang
          tieuDe="Không tìm thấy môn học"
          quayLai={{ href: "/subjects", nhan: "Môn học" }}
        />
        <TrongRong
          Icon={FileQuestion}
          tieuDe="Không tìm thấy môn học"
          moTa="Môn học này không tồn tại hoặc không thuộc tài khoản của bạn."
        />
      </Trang>
    );
  }

  const subject: SubjectDto = await subjectRes.json();
  const versions: SubjectVersionDto[] = await versionsRes.json();

  return (
    <Trang>
      <DauTrang
        quayLai={{ href: "/subjects", nhan: "Môn học" }}
        tieuDe={subject.name}
        moTa={
          <span className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
            <span className="font-mono text-xs">{subject.code}</span>
            <span aria-hidden>·</span>
            <span>Lịch sử phiên bản nội dung</span>
          </span>
        }
      />

      <DongThoiGian subjectId={id} versions={versions} />
    </Trang>
  );
}
