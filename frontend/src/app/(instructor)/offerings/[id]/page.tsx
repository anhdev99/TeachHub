import {
  CalendarClock,
  ChevronRight,
  ClipboardList,
  FileText,
  KeyRound,
  ListChecks,
  PenLine,
  Table2,
  Users,
} from "lucide-react";
import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";
import { CuonNgang, DauTrang, Trang, TrongRong } from "@/components/shell/trang";
import { DongLop } from "./dong-lop";
import { ngayGio } from "@/lib/dinh-dang";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type {
  AssignmentDto, EnrollmentDto, OfferingDto, ToGradeItemDto,
} from "@/lib/api/types";
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

  const [resLop, resSv, resNop] = await Promise.all([
    fetch(`${API_BASE}/api/offerings/${id}`, { headers, cache: "no-store" }),
    fetch(`${API_BASE}/api/offerings/${id}/enrollments`, { headers, cache: "no-store" }),
    // Chỉ để ĐẾM bài chưa chấm cho ô điều hướng. Trang này là nơi giảng viên vào đầu tiên
    // khi mở một lớp; không có con số đó thì phải bấm vào từng mục mới biết chỗ nào có việc.
    fetch(`${API_BASE}/api/offerings/${id}/submissions`, { headers, cache: "no-store" }),
  ]);

  // Backend trả 404 chứ không bao giờ 403 cho tài nguyên không thuộc người gọi, nên giao
  // diện cũng chỉ được nói "không tìm thấy" — viết "bạn không có quyền" là tự tay làm lộ
  // đúng thứ backend vừa che.
  if (resLop.status === 404) notFound();
  if (!resLop.ok) redirect("/login");

  const lop: OfferingDto = await resLop.json();
  const sinhVien: EnrollmentDto[] = resSv.ok ? await resSv.json() : [];
  const baiNop: ToGradeItemDto[] = resNop.ok ? await resNop.json() : [];
  const chuaCham = baiNop.filter((s) => s.status === "submitted").length;

  const resBaiTap = await fetch(
    `${API_BASE}/api/subject-versions/${lop.subject_version_id}/assignments`,
    { headers, cache: "no-store" },
  );
  const baiTap: AssignmentDto[] = resBaiTap.ok ? await resBaiTap.json() : [];

  return (
    <Trang>
      <DauTrang
        quayLai={{ href: "/offerings", nhan: "Lớp học phần" }}
        tieuDe={lop.subject_name}
        moTa={
          <span className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
            <span className="font-mono text-xs">{lop.subject_code}</span>
            <span aria-hidden>·</span>
            <span className="font-mono text-xs">{lop.class_code}</span>
            <span aria-hidden>·</span>
            <span>{lop.semester}</span>
            <span aria-hidden>·</span>
            <span>{lop.enrollment_count} sinh viên</span>
          </span>
        }
        hanhDong={
          <>
            <DongLop lop={lop} />
            <TaoBaiTap subjectVersionId={lop.subject_version_id} />
          </>
        }
      />

      {lop.join_code && (
        // Mã lớp là thứ giảng viên đọc to cho cả phòng, nên nó phải to, đơn cách, và giãn
        // chữ — không phải một mẩu chữ nhỏ lẫn trong dòng thông tin phụ.
        <div className="bg-card border-border flex flex-wrap items-center gap-x-4 gap-y-2 rounded-xl border px-4 py-3">
          <KeyRound className="text-muted-foreground size-4 shrink-0" aria-hidden />
          <span className="text-muted-foreground text-sm">Mã tham gia lớp</span>
          <span className="font-mono text-xl font-semibold tracking-[0.2em]">
            {lop.join_code}
          </span>
          {lop.join_code_expires_at && (
            <span className="text-muted-foreground text-xs">
              hết hạn{" "}
              <time dateTime={lop.join_code_expires_at}>
                {ngayGio(lop.join_code_expires_at)}
              </time>
            </span>
          )}
        </div>
      )}

      {/* Trung tâm điều hướng của lớp. Bản cũ là một hàng link gạch chân — nhìn không ra
          chỗ nào đang có việc, và mọi mục trông quan trọng như nhau. */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <TheDieuHuong
          href={`/offerings/${id}/grading`}
          Icon={PenLine}
          tieuDe="Bài nộp & chấm điểm"
          moTa={`${baiNop.length} bài đã nộp`}
          nhan={
            chuaCham > 0 ? (
              <Badge className="bg-warn-soft text-warn border-warn/25 shrink-0 border">
                {chuaCham} chờ chấm
              </Badge>
            ) : baiNop.length > 0 ? (
              <Badge className="bg-ok-soft text-ok border-ok/25 shrink-0 border">
                Đã chấm hết
              </Badge>
            ) : undefined
          }
        />
        <TheDieuHuong
          href={`/offerings/${id}/gradebook`}
          Icon={Table2}
          tieuDe="Sổ điểm"
          moTa="Cột điểm, trọng số, điểm cộng, xuất bảng"
        />
        <TheDieuHuong
          href={`/offerings/${id}/groups`}
          Icon={Users}
          tieuDe="Chia nhóm"
          moTa="Chia ngẫu nhiên hoặc kéo-thả từng sinh viên"
        />
        <TheDieuHuong
          href={`/offerings/${id}/quizzes`}
          Icon={ListChecks}
          tieuDe="Trắc nghiệm"
          moTa="Bài trắc nghiệm riêng của lớp này"
        />
      </div>

      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <ClipboardList className="text-muted-foreground size-4" aria-hidden />
            Bài tập
            <Badge variant="secondary" className="font-normal">
              {baiTap.length}
            </Badge>
          </CardTitle>
        </CardHeader>
        <CardContent>
          {baiTap.length === 0 ? (
            <TrongRong
              Icon={FileText}
              tieuDe="Chưa có bài tập nào"
              moTa="Bài tập thuộc về phiên bản nội dung mà lớp này đang ghim, nên mọi lớp dùng cùng phiên bản đều thấy nó."
              hanhDong={<TaoBaiTap subjectVersionId={lop.subject_version_id} />}
            />
          ) : (
            <ul className="flex flex-col gap-1.5">
              {baiTap.map((a) => (
                <li key={a.id}>
                  <Link
                    href={`/assignments/${a.id}/edit`}
                    className="hover:bg-accent group flex items-center gap-3 rounded-lg px-3 py-2.5 transition-colors"
                  >
                    <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span className="truncate font-medium">{a.title}</span>
                      <span className="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-0.5 text-sm">
                        <span>{a.slots.length} phần nộp</span>
                        <span aria-hidden>·</span>
                        <span>Tối đa {a.max_score} điểm</span>
                        {a.due_at && (
                          <>
                            <span aria-hidden>·</span>
                            <span className="inline-flex items-center gap-1">
                              <CalendarClock className="size-3.5 shrink-0" aria-hidden />
                              Hạn <time dateTime={a.due_at}>{ngayGio(a.due_at)}</time>
                            </span>
                          </>
                        )}
                      </span>
                    </span>

                    {a.submission_mode === "group" && (
                      <Badge variant="outline" className="shrink-0 font-normal">
                        Nộp theo nhóm
                      </Badge>
                    )}
                    <ChevronRight
                      className="text-muted-foreground group-hover:text-foreground size-4 shrink-0 transition-colors"
                      aria-hidden
                    />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Users className="text-muted-foreground size-4" aria-hidden />
            Sinh viên
            <Badge variant="secondary" className="font-normal">
              {sinhVien.length}
            </Badge>
          </CardTitle>
        </CardHeader>
        <CardContent>
          {sinhVien.length === 0 ? (
            <TrongRong
              Icon={Users}
              tieuDe="Chưa nhập danh sách sinh viên"
              moTa="Nhập danh sách từ tệp Excel, hoặc đưa mã tham gia lớp cho sinh viên tự vào."
            />
          ) : (
            // Bảng rộng cuộn trong khối riêng, không phải cả trang cuộn ngang.
            <CuonNgang>
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
                      <TableCell className="font-mono text-xs">{e.student_code}</TableCell>
                      <TableCell className="whitespace-nowrap">{e.full_name}</TableCell>
                      <TableCell className="text-muted-foreground">{e.email ?? "—"}</TableCell>
                      <TableCell>
                        {e.has_account ? (
                          <Badge className="bg-ok-soft text-ok border-ok/25 border">Đã có</Badge>
                        ) : (
                          <Badge variant="outline" className="font-normal">
                            Chưa tạo
                          </Badge>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CuonNgang>
          )}
        </CardContent>
      </Card>
    </Trang>
  );
}

function TheDieuHuong({
  href,
  Icon,
  tieuDe,
  moTa,
  nhan,
}: {
  href: string;
  Icon: React.ComponentType<{ className?: string }>;
  tieuDe: string;
  moTa: string;
  nhan?: React.ReactNode;
}) {
  return (
    <Link
      href={href}
      className="bg-card border-border hover:border-primary hover:bg-accent group flex min-h-11 items-center gap-3 rounded-xl border px-4 py-3.5 transition-colors"
    >
      <span className="bg-muted text-muted-foreground group-hover:bg-primary/10 group-hover:text-primary grid size-9 shrink-0 place-items-center rounded-lg transition-colors">
        <Icon className="size-4" />
      </span>
      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="truncate font-medium">{tieuDe}</span>
        <span className="text-muted-foreground truncate text-sm">{moTa}</span>
      </span>
      {nhan}
      <ChevronRight
        className="text-muted-foreground group-hover:text-foreground size-4 shrink-0 transition-colors"
        aria-hidden
      />
    </Link>
  );
}
