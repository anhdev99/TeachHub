import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
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
    <main className="mx-auto flex min-h-svh max-w-4xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-col gap-1">
        <Link href={`/offerings/${id}`} className="text-muted-foreground hover:text-foreground text-sm">
          ← Lớp học phần
        </Link>
        <h1 className="text-2xl font-semibold">Chia nhóm</h1>
      </div>

      <ChiaNhom offeringId={id} banDau={nhom} siSo={sinhVien.length} />
    </main>
  );
}
