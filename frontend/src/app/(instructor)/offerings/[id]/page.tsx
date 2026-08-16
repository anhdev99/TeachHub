import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { AssignmentDto, EnrollmentDto, OfferingDto } from "@/lib/api/types";
import { TaoBaiTap } from "./tao-bai-tap";

export default async function OfferingDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const headers = { authorization: `Bearer ${token}` };

  const [resLop, resSv] = await Promise.all([
    fetch(`${API_BASE}/api/offerings/${id}`, { headers, cache: "no-store" }),
    fetch(`${API_BASE}/api/offerings/${id}/enrollments`, { headers, cache: "no-store" }),
  ]);

  // Backend trả 404 chứ không bao giờ 403 cho tài nguyên không thuộc người gọi, nên giao
  // diện cũng chỉ được nói "không tìm thấy" — viết "bạn không có quyền" là tự tay làm lộ
  // đúng thứ backend vừa che.
  if (resLop.status === 404) notFound();
  if (!resLop.ok) redirect("/login");

  const lop: OfferingDto = await resLop.json();
  const sinhVien: EnrollmentDto[] = resSv.ok ? await resSv.json() : [];

  const resBaiTap = await fetch(
    `${API_BASE}/api/subject-versions/${lop.subject_version_id}/assignments`,
    { headers, cache: "no-store" },
  );
  const baiTap: AssignmentDto[] = resBaiTap.ok ? await resBaiTap.json() : [];

  return (
    <main className="mx-auto flex min-h-svh max-w-5xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-col gap-2">
        <Link href="/offerings" className="text-muted-foreground hover:text-foreground text-sm">
          ← Lớp học phần
        </Link>
        <h1 className="text-2xl font-semibold">
          <span className="text-muted-foreground font-mono">{lop.subject_code}</span>{" "}
          {lop.subject_name}
        </h1>
        <p className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-1 text-sm">
          <span>{lop.class_code}</span>
          <span>{lop.semester}</span>
          {lop.join_code && (
            <span>
              Mã lớp <span className="font-mono font-medium">{lop.join_code}</span>
            </span>
          )}
        </p>

        <nav className="mt-2 flex flex-wrap gap-3 text-sm">
          <Link href={`/offerings/${id}/grading`} className="underline">Bài nộp &amp; chấm điểm</Link>
          <Link href={`/offerings/${id}/groups`} className="underline">Chia nhóm</Link>
          <Link href={`/offerings/${id}/gradebook`} className="underline">Sổ điểm</Link>
          <Link href={`/offerings/${id}/quizzes`} className="underline">Trắc nghiệm</Link>
          <Link href="/rubrics" className="underline">Rubric</Link>
          <Link href="/questions" className="underline">Ngân hàng câu hỏi</Link>
        </nav>
      </div>

      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3">
          <CardTitle className="text-base">Bài tập ({baiTap.length})</CardTitle>
          <TaoBaiTap subjectVersionId={lop.subject_version_id} />
        </CardHeader>
        <CardContent>
          {baiTap.length === 0 ? (
            <p className="text-muted-foreground text-sm">
              Chưa có bài tập nào trong phiên bản lớp này đang ghim.
            </p>
          ) : (
            <ul className="flex flex-col gap-2">
              {baiTap.map((a) => (
                <li key={a.id}>
                  <Link
                    href={`/assignments/${a.id}/edit`}
                    className="border-border hover:border-primary flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 transition-colors"
                  >
                    <span className="font-medium">{a.title}</span>
                    <span className="text-muted-foreground flex flex-wrap items-center gap-2 text-sm">
                      <Badge variant="outline">{a.slots.length} phần nộp</Badge>
                      {a.due_at && (
                        <span>
                          Hạn {new Date(a.due_at).toLocaleString("vi-VN")}
                        </span>
                      )}
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
          <CardTitle className="text-base">Sinh viên ({sinhVien.length})</CardTitle>
        </CardHeader>
        <CardContent>
          {sinhVien.length === 0 ? (
            <p className="text-muted-foreground text-sm">Chưa nhập danh sách sinh viên.</p>
          ) : (
            // Bảng rộng cuộn trong khối riêng, không phải cả trang cuộn ngang.
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>MSSV</TableHead>
                    <TableHead>Họ tên</TableHead>
                    <TableHead>Email</TableHead>
                    <TableHead>Tài khoản</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {sinhVien.map((e) => (
                    <TableRow key={e.id}>
                      <TableCell className="font-mono">{e.student_code}</TableCell>
                      <TableCell>{e.full_name}</TableCell>
                      <TableCell className="text-muted-foreground">{e.email ?? "—"}</TableCell>
                      <TableCell>
                        {e.has_account ? (
                          <Badge variant="secondary">Đã có</Badge>
                        ) : (
                          <Badge variant="outline">Chưa tạo</Badge>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>
    </main>
  );
}
