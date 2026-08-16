import { CalendarClock, ChevronRight, GraduationCap, Inbox, PenLine } from "lucide-react";
import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { EmptyState, PageHeader, PageShell } from "@/components/shell/page-shell";
import { formatDateTime, formatRelative } from "@/lib/format";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { InstructorDashboardDto } from "@/lib/api/types";

export default async function DashboardPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/instructor/dashboard`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  // A signed-in student who lands here goes to the learning page rather than being shown an
  // empty dashboard. (Identity is handled by the route group's layout — this page no longer
  // calls /auth/me itself.)
  if (!res.ok) redirect("/learn");

  const d: InstructorDashboardDto = await res.json();

  return (
    <PageShell>
      <PageHeader
        title="Tổng quan"
        description="Việc đang chờ bạn, và các lớp đang chạy."
      />

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <StatCard
          label="Lớp đang dạy"
          value={d.so_lop_dang_day}
          Icon={GraduationCap}
          href="/offerings"
        />
        <StatCard
          label="Bài chưa chấm"
          value={d.so_bai_chua_cham}
          Icon={PenLine}
          // Only tint it as a warning when there is genuinely work to do: a yellow zero
          // teaches the user to ignore yellow, and by the time 30 submissions have piled up
          // the color means nothing anymore.
          emphasized={d.so_bai_chua_cham > 0}
        />
        <StatCard
          label="Bài tập tới hạn trong 7 ngày"
          value={d.so_bai_tap_sap_den_han}
          Icon={CalendarClock}
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Cần xử lý</CardTitle>
        </CardHeader>
        <CardContent>
          {d.can_xu_ly.length === 0 ? (
            <EmptyState
              Icon={Inbox}
              title="Không còn bài nào chờ chấm"
              description="Bài mới nộp sẽ xuất hiện ở đây."
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
                            {/* Relative form answers "is this urgent?"; the exact timestamp
                                stays in the title for anyone who needs the real number. */}
                            <time dateTime={c.submitted_at} title={formatDateTime(c.submitted_at)}>
                              {formatRelative(c.submitted_at)}
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
                    {/* The chevron is the only signal that the whole row is clickable. The
                        old version had just a border, so a clickable row was
                        indistinguishable from a read-only one. */}
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
            <EmptyState
              Icon={GraduationCap}
              title="Chưa mở lớp nào"
              description="Tạo môn học và phiên bản nội dung trước, rồi mở lớp học phần từ phiên bản đã xuất bản."
              action={
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
    </PageShell>
  );
}

function StatCard({
  label,
  value,
  Icon,
  href,
  emphasized = false,
}: {
  label: string;
  value: number;
  Icon: React.ComponentType<{ className?: string }>;
  href?: string;
  emphasized?: boolean;
}) {
  const card = (
    <Card
      className={`h-full gap-0 py-4 transition-colors ${href ? "hover:border-primary" : ""}`}
    >
      <CardHeader className="px-4 pb-0">
        <CardTitle className="text-muted-foreground flex items-center gap-2 text-sm font-normal">
          <Icon className="size-4 shrink-0" />
          <span className="text-pretty">{label}</span>
        </CardTitle>
      </CardHeader>
      <CardContent className="px-4 pt-1">
        {/* `tabular-nums` keeps the digits aligned when the cards sit side by side; without
            it the three stat cards get different digit widths and look misaligned. */}
        <span
          className={`text-3xl font-semibold tabular-nums ${emphasized ? "text-warn" : ""}`}
        >
          {value}
        </span>
      </CardContent>
    </Card>
  );

  return href ? (
    <Link href={href} className="block">
      {card}
    </Link>
  ) : (
    card
  );
}
