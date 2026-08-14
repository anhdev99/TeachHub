import { cookies } from "next/headers";
import { redirect } from "next/navigation";
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
      <main className="mx-auto max-w-3xl p-6">
        <h1 className="text-xl font-semibold">Không tìm thấy môn học</h1>
        <p className="text-muted-foreground mt-2 text-sm">
          Môn học này không tồn tại hoặc không thuộc tài khoản của bạn.
        </p>
      </main>
    );
  }

  const subject: SubjectDto = await subjectRes.json();
  const versions: SubjectVersionDto[] = await versionsRes.json();

  return (
    <main className="mx-auto flex min-h-svh max-w-3xl flex-col gap-6 p-6">
      <header className="flex flex-col gap-1">
        <p className="text-muted-foreground font-mono text-sm">{subject.code}</p>
        <h1 className="text-2xl font-semibold">{subject.name}</h1>
        <p className="text-muted-foreground text-sm">Lịch sử phiên bản nội dung</p>
      </header>

      <DongThoiGian subjectId={id} versions={versions} />
    </main>
  );
}
