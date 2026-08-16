import { CalendarClock, ChevronRight, GraduationCap, Inbox, PenLine } from "lucide-react";
import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { DauTrang, Trang, TrongRong } from "@/components/shell/trang";
import { khoangCach, ngayGio } from "@/lib/dinh-dang";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { InstructorDashboardDto } from "@/lib/api/types";

export default async function DashboardPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/instructor/dashboard`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  // Sinh viên đăng nhập rơi vào đây thì đưa về trang học, không hiện dashboard rỗng.
  // (Danh tính người dùng do layout của route group lo — trang này không gọi /auth/me nữa.)
  if (!res.ok) redirect("/learn");

  const d: InstructorDashboardDto = await res.json();

  return (
    <Trang>
      <DauTrang
        tieuDe="Tổng quan"
        moTa="Việc đang chờ bạn, và các lớp đang chạy."
      />

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <TheSo
          nhan="Lớp đang dạy"
          so={d.so_lop_dang_day}
          Icon={GraduationCap}
          href="/offerings"
        />
        <TheSo
          nhan="Bài chưa chấm"
          so={d.so_bai_chua_cham}
          Icon={PenLine}
          // Chỉ tô cảnh báo khi thật sự có việc: một số 0 màu vàng dạy người dùng bỏ qua màu
          // vàng, và tới lúc có 30 bài dồn thì màu đó không còn nghĩa gì nữa.
          nhanManh={d.so_bai_chua_cham > 0}
        />
        <TheSo
          nhan="Bài tập tới hạn trong 7 ngày"
          so={d.so_bai_tap_sap_den_han}
          Icon={CalendarClock}
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Cần xử lý</CardTitle>
        </CardHeader>
        <CardContent>
          {d.can_xu_ly.length === 0 ? (
            <TrongRong
              Icon={Inbox}
              tieuDe="Không còn bài nào chờ chấm"
              moTa="Bài mới nộp sẽ xuất hiện ở đây."
            />
          ) : (
            <ul className="flex flex-col gap-1.5">
              {d.can_xu_ly.map((c) => (
                <li key={c.submission_id}>
                  <Link
                    href={`/grading/${c.submission_id}`}
                    className="hover:bg-accent group flex items-center gap-3 rounded-lg px-3 py-2.5 transition-colors"
                  >
                    <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span className="truncate font-medium">{c.title}</span>
                      <span className="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-0.5 text-sm">
                        <span className="truncate">{c.who}</span>
                        <span aria-hidden>·</span>
                        <span className="font-mono text-xs">{c.class_code}</span>
                        {c.submitted_at && (
                          <>
                            <span aria-hidden>·</span>
                            {/* Tương đối để trả lời "có gấp không"; mốc chính xác giữ trong
                                title cho người cần con số thật. */}
                            <time dateTime={c.submitted_at} title={ngayGio(c.submitted_at)}>
                              {khoangCach(c.submitted_at)}
                            </time>
                          </>
                        )}
                      </span>
                    </span>

                    {c.is_late && (
                      <Badge className="bg-danger-soft text-danger border-danger/25 shrink-0 border">
                        Trễ hạn
                      </Badge>
                    )}
                    {/* Mũi tên: dấu hiệu duy nhất cho biết cả dòng bấm được. Bản cũ chỉ có
                        viền, nên không phân biệt được dòng bấm được với dòng chỉ để đọc. */}
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
          <CardTitle className="text-base">Lớp của tôi</CardTitle>
        </CardHeader>
        <CardContent>
          {d.lop.length === 0 ? (
            <TrongRong
              Icon={GraduationCap}
              tieuDe="Chưa mở lớp nào"
              moTa="Tạo môn học và phiên bản nội dung trước, rồi mở lớp học phần từ phiên bản đã xuất bản."
              hanhDong={
                <Link
                  href="/subjects"
                  className="bg-primary text-primary-foreground inline-flex min-h-11 items-center rounded-lg px-4 text-sm font-medium"
                >
                  Tới Môn học
                </Link>
              }
            />
          ) : (
            <ul className="flex flex-col gap-1.5">
              {d.lop.map((l) => (
                <li key={l.offering_id}>
                  <Link
                    href={`/offerings/${l.offering_id}`}
                    className="hover:bg-accent group flex items-center gap-3 rounded-lg px-3 py-2.5 transition-colors"
                  >
                    <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span className="truncate font-medium">{l.subject_name}</span>
                      <span className="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-0.5 text-sm">
                        <span className="font-mono text-xs">{l.subject_code}</span>
                        <span aria-hidden>·</span>
                        <span className="font-mono text-xs">{l.class_code}</span>
                        <span aria-hidden>·</span>
                        <span>{l.student_count} sinh viên</span>
                      </span>
                    </span>

                    {l.chua_cham > 0 && (
                      <Badge className="bg-warn-soft text-warn border-warn/25 shrink-0 border">
                        {l.chua_cham} chờ chấm
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
    </Trang>
  );
}

function TheSo({
  nhan,
  so,
  Icon,
  href,
  nhanManh = false,
}: {
  nhan: string;
  so: number;
  Icon: React.ComponentType<{ className?: string }>;
  href?: string;
  nhanManh?: boolean;
}) {
  const noi = (
    <Card
      className={`h-full gap-0 py-4 transition-colors ${href ? "hover:border-primary" : ""}`}
    >
      <CardHeader className="px-4 pb-0">
        <CardTitle className="text-muted-foreground flex items-center gap-2 text-sm font-normal">
          <Icon className="size-4 shrink-0" />
          <span className="text-pretty">{nhan}</span>
        </CardTitle>
      </CardHeader>
      <CardContent className="px-4 pt-1">
        {/* `tabular-nums` để các số thẳng cột với nhau khi đứng cạnh; thiếu nó thì ba thẻ số
            có bề rộng chữ số khác nhau và trông xô lệch. */}
        <span
          className={`text-3xl font-semibold tabular-nums ${nhanManh ? "text-warn" : ""}`}
        >
          {so}
        </span>
      </CardContent>
    </Card>
  );

  return href ? (
    <Link href={href} className="block">
      {noi}
    </Link>
  ) : (
    noi
  );
}
