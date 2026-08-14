import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { MyProgressDto } from "@/lib/api/types";
import { TrangHoc } from "./trang-hoc";

/** Màn hình 6.3 — trang học của sinh viên. */
export default async function LearnOfferingPage({
  params,
}: {
  params: Promise<{ offeringId: string }>;
}) {
  const { offeringId } = await params;

  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/offerings/${offeringId}/my-progress`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (res.status === 403) {
    const problem = await res.json().catch(() => null);
    if (problem?.code === "must_change_password") redirect("/doi-mat-khau");
  }

  if (!res.ok) {
    return (
      <main className="mx-auto max-w-3xl p-6">
        <h1 className="text-xl font-semibold">Không tìm thấy lớp học</h1>
        <p className="text-muted-foreground mt-2 text-sm">
          Lớp này không tồn tại hoặc bạn không có tên trong danh sách.
        </p>
      </main>
    );
  }

  const progress: MyProgressDto = await res.json();
  return <TrangHoc offeringId={offeringId} banDau={progress} />;
}
