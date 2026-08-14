import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { MyOfferingDto } from "@/lib/api/types";
import { NhapMaLop } from "./nhap-ma-lop";

export default async function LearnPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/me/offerings`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  // 403 kèm must_change_password: middleware chặn cho tới khi đổi mật khẩu lần đầu.
  if (res.status === 403) {
    const problem = await res.json().catch(() => null);
    if (problem?.code === "must_change_password") redirect("/doi-mat-khau");
  }
  if (!res.ok) redirect("/login");

  const offerings: MyOfferingDto[] = await res.json();

  return (
    <main className="mx-auto flex min-h-svh max-w-3xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">Lớp của tôi</h1>
        <NhapMaLop />
      </div>

      {offerings.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          Bạn chưa vào lớp nào. Nhập mã lớp giảng viên đọc trên lớp để bắt đầu.
        </p>
      ) : (
        <ul className="flex flex-col gap-3">
          {offerings.map((o) => (
            <li key={o.id}>
              <Link href={`/learn/${o.id}`}>
                <Card className="transition-colors hover:border-primary">
                  <CardHeader>
                    <CardTitle className="text-base">
                      <span className="text-muted-foreground font-mono">{o.subject_code}</span>{" "}
                      {o.subject_name}
                    </CardTitle>
                  </CardHeader>
                  <CardContent className="text-muted-foreground text-sm">
                    {o.class_code} · {o.semester}
                  </CardContent>
                </Card>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
