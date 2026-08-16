import { FileQuestion } from "lucide-react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { EmptyState, PageHeader, PageShell } from "@/components/shell/page-shell";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { SubjectDto, SubjectVersionDto } from "@/lib/api/types";
import { VersionTimeline } from "./version-timeline";

/**
 * Screen 6.10 — version history. A vertical timeline, one milestone per version.
 * Server component: the token is read from the httpOnly cookie and never leaves the server.
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

  // The backend returns 404 for another instructor's subject (never 403) — as far as the user
  // is concerned, "does not exist" and "is not yours" are the same screen.
  if (!subjectRes.ok || !versionsRes.ok) {
    return (
      <PageShell>
        <PageHeader
          title="Không tìm thấy môn học"
          backTo={{ href: "/subjects", label: "Môn học" }}
        />
        <EmptyState
          Icon={FileQuestion}
          title="Không tìm thấy môn học"
          description="Môn học này không tồn tại hoặc không thuộc tài khoản của bạn."
        />
      </PageShell>
    );
  }

  const subject: SubjectDto = await subjectRes.json();
  const versions: SubjectVersionDto[] = await versionsRes.json();

  return (
    <PageShell>
      <PageHeader
        backTo={{ href: "/subjects", label: "Môn học" }}
        title={subject.name}
        description={
          <span className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
            <span className="font-mono text-xs">{subject.code}</span>
            <span aria-hidden>·</span>
            <span>Lịch sử phiên bản nội dung</span>
          </span>
        }
      />

      <VersionTimeline subjectId={id} versions={versions} />
    </PageShell>
  );
}
