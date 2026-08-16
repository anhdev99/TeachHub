import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { PageHeader, PageShell } from "@/components/shell/page-shell";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { AssignmentDto } from "@/lib/api/types";
import { SoanBaiTap } from "./soan-bai-tap";

export default async function AssignmentEditPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/assignments/${id}`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (res.status === 404) notFound();
  if (!res.ok) redirect("/login");

  const bai: AssignmentDto = await res.json();

  return (
    <PageShell>
      <PageHeader
        title={bai.title}
        description={bai.description ?? "Bài tập chưa có mô tả."}
        backTo={{ href: "/offerings", label: "Lớp học phần" }}
      />

      <SoanBaiTap banDau={bai} />
    </PageShell>
  );
}
