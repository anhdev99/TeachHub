import { cookies } from "next/headers";
import { redirect } from "next/navigation";
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

  if (!subjectRes.ok || !versionsRes.ok) {
    return (
      <main className="mx-auto max-w-3xl p-6">
        <h1 className="text-xl font-semibold">Không tìm thấy phiên bản</h1>
      </main>
    );
  }

  const subject: SubjectDto = await subjectRes.json();
  const versions: SubjectVersionDto[] = await versionsRes.json();
  const version = versions.find((v) => v.id === versionId);

  if (!version) {
    return (
      <main className="mx-auto max-w-3xl p-6">
        <h1 className="text-xl font-semibold">Không tìm thấy phiên bản</h1>
      </main>
    );
  }

  return <TrinhSoanThao subject={subject} version={version} />;
}
