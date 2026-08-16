import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { PageHeader, PageShell } from "@/components/shell/page-shell";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { RubricDto } from "@/lib/api/types";
import { RubricEditor } from "./rubric-editor";

export default async function RubricsPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/rubrics`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (!res.ok) redirect("/login");

  const rubrics: RubricDto[] = await res.json();

  return (
    <PageShell>
      <PageHeader
        title="Bộ tiêu chí chấm"
        description="Bộ tiêu chí thuộc về bạn chứ không thuộc về một môn — dùng lại được cho mọi bài của mọi lớp."
      />

      <RubricEditor initial={rubrics} />
    </PageShell>
  );
}
