import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { OfferingDto } from "@/lib/api/types";

export default async function OfferingsPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/offerings`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (!res.ok) redirect("/login");

  const offerings: OfferingDto[] = await res.json();

  return (
    <main className="mx-auto flex min-h-svh max-w-4xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">Lớp học phần</h1>
        <Link href="/subjects" className="text-muted-foreground hover:text-foreground text-sm">
          Môn học của tôi →
        </Link>
      </div>

      {offerings.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          Chưa mở lớp nào. Mở lớp từ một phiên bản môn học đã xuất bản.
        </p>
      ) : (
        <ul className="flex flex-col gap-3">
          {offerings.map((o) => (
            <li key={o.id}>
              <Link href={`/offerings/${o.id}`}>
                <Card className="transition-colors hover:border-primary">
                  <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3">
                    <CardTitle className="text-base">
                      <span className="text-muted-foreground font-mono">{o.subject_code}</span>{" "}
                      {o.subject_name}
                    </CardTitle>
                    <Badge variant={o.status === "active" ? "default" : "secondary"}>
                      {o.status === "active" ? "Đang chạy" : o.status}
                    </Badge>
                  </CardHeader>
                  <CardContent className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-1 text-sm">
                    <span>{o.class_code}</span>
                    <span>{o.semester}</span>
                    <span>{o.enrollment_count} sinh viên</span>
                    {o.join_code && (
                      <span>
                        Mã lớp <span className="font-mono">{o.join_code}</span>
                      </span>
                    )}
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
