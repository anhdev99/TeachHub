import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import { NHAN_TRANG_THAI_NOP, type MyAssignmentDto } from "@/lib/api/types";

export default async function MyAssignmentsPage({
  params,
}: {
  params: Promise<{ offeringId: string }>;
}) {
  const { offeringId } = await params;
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/offerings/${offeringId}/assignments`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (res.status === 404) notFound();
  if (!res.ok) redirect("/login");

  const baiTap: MyAssignmentDto[] = await res.json();
  const bayGio = Date.now();

  return (
    <main className="mx-auto flex min-h-svh max-w-3xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-col gap-1">
        <Link
          href={`/learn/${offeringId}`}
          className="text-muted-foreground hover:text-foreground text-sm"
        >
          ← Trang học
        </Link>
        <h1 className="text-2xl font-semibold">Bài tập</h1>
      </div>

      {baiTap.length === 0 ? (
        <p className="text-muted-foreground text-sm">Lớp này chưa có bài tập nào.</p>
      ) : (
        <ul className="flex flex-col gap-3">
          {baiTap.map((a) => {
            const quaHan = a.due_at !== null && new Date(a.due_at).getTime() < bayGio;

            return (
              <li key={a.id}>
                <Link href={`/assignments/${a.id}/submit`}>
                  <Card className="transition-colors hover:border-primary">
                    <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
                      <CardTitle className="text-base">{a.title}</CardTitle>
                      {a.status ? (
                        <Badge variant={a.status === "draft" ? "secondary" : "default"}>
                          {NHAN_TRANG_THAI_NOP[a.status]}
                          {a.is_late && " · trễ"}
                        </Badge>
                      ) : (
                        <Badge variant="outline">Chưa nộp</Badge>
                      )}
                    </CardHeader>

                    <CardContent className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-1 text-sm">
                      {a.due_at ? (
                        <span className={quaHan ? "text-destructive" : undefined}>
                          Hạn {new Date(a.due_at).toLocaleString("vi-VN")}
                          {quaHan && (a.allow_late_submission ? " · còn nhận nộp trễ" : " · đã đóng")}
                        </span>
                      ) : (
                        <span>Không có hạn nộp</span>
                      )}
                      <span>{a.slots.length} phần nộp</span>
                      {a.score !== null && <span>Điểm: {a.score}/{a.max_score}</span>}
                    </CardContent>
                  </Card>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </main>
  );
}
