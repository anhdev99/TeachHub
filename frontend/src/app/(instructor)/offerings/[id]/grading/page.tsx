import { ChevronRight, Copy, Inbox, PenLine } from "lucide-react";
import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader, PageShell, EmptyState } from "@/components/shell/page-shell";
import { formatScore, formatRelative, formatDateTime } from "@/lib/format";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import { SUBMISSION_STATUS_LABELS, type ToGradeItemDto } from "@/lib/api/types";

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
  const daXuLy = ds.filter((s) => s.status !== "submitted");

  return (
    <PageShell>
      <PageHeader
        backTo={{ href: `/offerings/${id}`, label: "Lớp học phần" }}
        title="Bài nộp"
        description={`${chuaCham.length} bài chưa chấm trên tổng ${ds.length} bài đã nộp.`}
      />

      {ds.length === 0 ? (
        <Card>
          <CardContent>
            <EmptyState
              Icon={Inbox}
              title="Chưa có bài nộp nào"
              description="Bài sinh viên nộp sẽ xuất hiện ở đây, bài chưa chấm nằm trên cùng."
            />
          </CardContent>
        </Card>
      ) : (
        <>
          {/* Tách hàng chờ ra một khối riêng, đặt TRÊN: đây là danh sách việc, không phải
              nhật ký. Trộn chung thì bài cần chấm lẫn giữa bài đã xong. */}
          {chuaCham.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <PenLine className="text-muted-foreground size-4" aria-hidden />
                  Chờ chấm
                  <Badge className="bg-warn-soft text-warn border-warn/25 border font-normal">
                    {chuaCham.length}
                  </Badge>
                </CardTitle>
              </CardHeader>
              <CardContent>
                <DanhSach ds={chuaCham} offeringId={id} />
              </CardContent>
            </Card>
          )}

          {daXuLy.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Đã xử lý</CardTitle>
              </CardHeader>
              <CardContent>
                <DanhSach ds={daXuLy} offeringId={id} />
              </CardContent>
            </Card>
          )}
        </>
      )}
    </PageShell>
  );
}

function NhanTrangThai({ status }: { status: ToGradeItemDto["status"] }) {
  const nhan = SUBMISSION_STATUS_LABELS[status];

  if (status === "graded" || status === "returned") {
    return <Badge className="bg-ok-soft text-ok border-ok/25 shrink-0 border">{nhan}</Badge>;
  }
  if (status === "submitted" || status === "grading") {
    return (
      <Badge className="bg-warn-soft text-warn border-warn/25 shrink-0 border">{nhan}</Badge>
    );
  }
  return (
    <Badge variant="secondary" className="shrink-0 font-normal">
      {nhan}
    </Badge>
  );
}

function DanhSach({ ds, offeringId }: { ds: ToGradeItemDto[]; offeringId: string }) {
  return (
    <ul className="flex flex-col gap-1.5">
      {ds.map((s) => (
        <li
          key={s.submission_id}
          className="hover:bg-accent group flex items-center rounded-lg pr-1 transition-colors"
        >
          <Link
            href={`/grading/${s.submission_id}`}
            className="flex min-w-0 flex-1 items-center gap-3 rounded-lg px-3 py-2.5"
          >
            <span className="flex min-w-0 flex-1 flex-col gap-0.5">
              <span className="truncate font-medium">{s.title}</span>
              <span className="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-0.5 text-sm">
                {s.group_name && (
                  <>
                    <span className="text-foreground truncate font-medium">{s.group_name}</span>
                    <span aria-hidden>·</span>
                  </>
                )}
                <span className="truncate">{s.student_names}</span>
                {s.submitted_at && (
                  <>
                    <span aria-hidden>·</span>
                    {/* Tương đối để trả lời "có gấp không"; mốc chính xác giữ trong title. */}
                    <time dateTime={s.submitted_at} title={formatDateTime(s.submitted_at)}>
                      {formatRelative(s.submitted_at)}
                    </time>
                  </>
                )}
              </span>
            </span>

            {s.is_late && (
              <Badge className="bg-danger-soft text-danger border-danger/25 shrink-0 border">
                Trễ hạn
              </Badge>
            )}
            {/* `!= null` chứ không `!== null`: API bỏ hẳn khoá khỏi JSON khi giá trị là
                null, nên trường này về tới đây là `undefined`, và phép so sánh chặt sẽ cho
                qua rồi in ra dấu "—" ở đúng những bài chưa có điểm. */}
            {s.score != null && (
              <span className="shrink-0 text-sm font-medium tabular-nums">{formatScore(s.score)}</span>
            )}
            <NhanTrangThai status={s.status} />
            <ChevronRight
              className="text-muted-foreground group-hover:text-foreground size-4 shrink-0 transition-colors"
              aria-hidden
            />
          </Link>

          {/* Đối chiếu trùng lặp là một đích KHÁC, nên nó phải là một liên kết riêng nằm
              ngoài vùng bấm chính — nhét vào trong thì bấm nhầm là rơi sang màn hình khác. */}
          {s.assignment_id && (
            <Link
              href={`/assignments/${s.assignment_id}/similarity?offering=${offeringId}`}
              title="Đối chiếu trùng lặp"
              aria-label={`Đối chiếu trùng lặp cho ${s.title}`}
              className="text-muted-foreground hover:text-foreground hover:bg-background grid size-9 shrink-0 place-items-center rounded-md transition-colors"
            >
              <Copy className="size-4" aria-hidden />
            </Link>
          )}
        </li>
      ))}
    </ul>
  );
}
