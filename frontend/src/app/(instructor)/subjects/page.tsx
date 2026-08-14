import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { SubjectDto } from "@/lib/api/types";
import { TaoMonMoi } from "./tao-mon-moi";

export default async function SubjectsPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/subjects`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (!res.ok) redirect("/login");

  const subjects: SubjectDto[] = await res.json();

  return (
    <main className="mx-auto flex min-h-svh max-w-4xl flex-col gap-6 p-6">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold">Môn học của tôi</h1>
        <TaoMonMoi />
      </div>

      {subjects.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          Chưa có môn học nào. Tạo môn đầu tiên để bắt đầu soạn nội dung.
        </p>
      ) : (
        <ul className="flex flex-col gap-3">
          {subjects.map((s) => (
            <li key={s.id}>
              <Link href={`/subjects/${s.id}/versions`}>
                <Card className="transition-colors hover:border-primary">
                  <CardHeader className="flex flex-row items-center justify-between gap-3">
                    <CardTitle className="text-base">
                      <span className="text-muted-foreground font-mono">{s.code}</span>{" "}
                      {s.name}
                    </CardTitle>
                    {s.current_version_id ? (
                      <Badge>Đang có bản xuất bản</Badge>
                    ) : (
                      <Badge variant="secondary">Chưa xuất bản</Badge>
                    )}
                  </CardHeader>
                  {s.description && (
                    <CardContent className="text-muted-foreground text-sm">
                      {s.description}
                    </CardContent>
                  )}
                </Card>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
