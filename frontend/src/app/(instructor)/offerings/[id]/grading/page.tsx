import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import { NHAN_TRANG_THAI_NOP, type ToGradeItemDto } from "@/lib/api/types";

export default async function GradingListPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/offerings/${id}/submissions`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (res.status === 404) notFound();
  if (!res.ok) redirect("/login");

  const ds: ToGradeItemDto[] = await res.json();
  const chuaCham = ds.filter((s) => s.status === "submitted");

  return (
    <main className="mx-auto flex min-h-svh max-w-5xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-col gap-1">
        <Link href={`/offerings/${id}`} className="text-muted-foreground hover:text-foreground text-sm">
          ← Lớp học phần
        </Link>
        <h1 className="text-2xl font-semibold">Bài nộp</h1>
        <p className="text-muted-foreground text-sm">
          {chuaCham.length} bài chưa chấm trên tổng {ds.length} bài đã nộp.
        </p>
      </div>

      {ds.length === 0 ? (
        <p className="text-muted-foreground text-sm">Chưa có bài nộp nào.</p>
      ) : (
        <div className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Bài tập</TableHead>
                <TableHead>Người nộp</TableHead>
                <TableHead>Nộp lúc</TableHead>
                <TableHead>Trạng thái</TableHead>
                <TableHead>Điểm</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {ds.map((s) => (
                <TableRow key={s.submission_id}>
                  <TableCell>
                    <Link href={`/grading/${s.submission_id}`} className="hover:underline">
                      {s.title}
                    </Link>
                    {s.assignment_id && (
                      <Link
                        href={`/assignments/${s.assignment_id}/similarity?offering=${id}`}
                        className="text-muted-foreground ml-2 text-xs underline"
                      >
                        trùng lặp
                      </Link>
                    )}
                  </TableCell>
                  <TableCell>
                    {s.group_name && <span className="font-medium">{s.group_name} · </span>}
                    <span className="text-muted-foreground">{s.student_names}</span>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {s.submitted_at ? new Date(s.submitted_at).toLocaleString("vi-VN") : "—"}
                    {s.is_late && <span className="text-destructive"> · trễ</span>}
                  </TableCell>
                  <TableCell>
                    <Badge variant={s.status === "submitted" ? "outline" : "default"}>
                      {NHAN_TRANG_THAI_NOP[s.status]}
                    </Badge>
                  </TableCell>
                  <TableCell className="font-mono">{s.score ?? "—"}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </main>
  );
}
