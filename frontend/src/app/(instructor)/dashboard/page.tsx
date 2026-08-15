import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { InstructorDashboardDto } from "@/lib/api/types";

export default async function DashboardPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const headers = { authorization: `Bearer ${token}` };

  const [resMe, resDash] = await Promise.all([
    fetch(`${API_BASE}/api/auth/me`, { headers, cache: "no-store" }),
    fetch(`${API_BASE}/api/instructor/dashboard`, { headers, cache: "no-store" }),
  ]);

  if (!resMe.ok) redirect("/login");

  const me: { full_name?: string; email?: string; roles?: string[] } = await resMe.json();

  // Sinh viên đăng nhập rơi vào đây thì đưa về trang học, không hiện dashboard rỗng.
  if (!resDash.ok) redirect("/learn");

  const d: InstructorDashboardDto = await resDash.json();

  return (
    <main className="mx-auto flex min-h-svh max-w-5xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-baseline justify-between gap-3">
        <h1 className="text-2xl font-semibold">Bảng điều khiển</h1>
        <p className="text-muted-foreground text-sm">
          {me.full_name} · {me.email}
        </p>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <TheSo nhan="Lớp đang dạy" so={d.so_lop_dang_day} href="/offerings" />
        <TheSo nhan="Bài chưa chấm" so={d.so_bai_chua_cham} />
        <TheSo nhan="Bài tập tới hạn trong 7 ngày" so={d.so_bai_tap_sap_den_han} />
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Cần xử lý</CardTitle>
        </CardHeader>
        <CardContent>
          {d.can_xu_ly.length === 0 ? (
            <p className="text-muted-foreground text-sm">Không còn bài nào chờ chấm.</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {d.can_xu_ly.map((c) => (
                <li key={c.submission_id}>
                  <Link
                    href={`/grading/${c.submission_id}`}
                    className="border-border hover:border-primary flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 transition-colors"
                  >
                    <span>
                      <span className="font-medium">{c.title}</span>{" "}
                      <span className="text-muted-foreground">— {c.who}</span>
                    </span>
                    <span className="text-muted-foreground flex items-center gap-2 text-sm">
                      <span>{c.class_code}</span>
                      {c.submitted_at && (
                        <span>{new Date(c.submitted_at).toLocaleString("vi-VN")}</span>
                      )}
                      {c.is_late && <Badge variant="destructive">Trễ</Badge>}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Lớp của tôi</CardTitle>
        </CardHeader>
        <CardContent>
          {d.lop.length === 0 ? (
            <p className="text-muted-foreground text-sm">Chưa mở lớp nào.</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {d.lop.map((l) => (
                <li key={l.offering_id}>
                  <Link
                    href={`/offerings/${l.offering_id}`}
                    className="border-border hover:border-primary flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 transition-colors"
                  >
                    <span>
                      <span className="text-muted-foreground font-mono">{l.subject_code}</span>{" "}
                      {l.subject_name}
                    </span>
                    <span className="text-muted-foreground flex flex-wrap items-center gap-3 text-sm">
                      <span>{l.class_code}</span>
                      <span>{l.student_count} SV</span>
                      {l.chua_cham > 0 && <Badge>{l.chua_cham} bài chờ chấm</Badge>}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </main>
  );
}

function TheSo({ nhan, so, href }: { nhan: string; so: number; href?: string }) {
  const noi = (
    <Card className="h-full transition-colors hover:border-primary">
      <CardHeader className="pb-2">
        <CardTitle className="text-muted-foreground text-sm font-normal">{nhan}</CardTitle>
      </CardHeader>
      <CardContent>
        <span className="text-3xl font-semibold tabular-nums">{so}</span>
      </CardContent>
    </Card>
  );

  return href ? <Link href={href}>{noi}</Link> : noi;
}
