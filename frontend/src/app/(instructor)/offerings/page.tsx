import { ChevronRight, GraduationCap } from "lucide-react";
import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { EmptyState, PageHeader, PageShell } from "@/components/shell/page-shell";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { OfferingDto } from "@/lib/api/types";

/**
 * The offering's status badge. `active` is the everyday state, so it does NOT get a loud
 * color — tinting every row leaves the color unable to distinguish anything, and the one that
 * actually deserves attention ("đã đóng") drowns.
 */
function StatusBadge({ status }: { status: OfferingDto["status"] }) {
  if (status === "active") {
    return (
      <Badge className="bg-ok-soft text-ok border-ok/25 shrink-0 border">Đang chạy</Badge>
    );
  }
  if (status === "closed") {
    return (
      <Badge variant="secondary" className="shrink-0">
        Đã đóng
      </Badge>
    );
  }
  return (
    <Badge className="bg-warn-soft text-warn border-warn/25 shrink-0 border">Bản nháp</Badge>
  );
}

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
    <PageShell>
      <PageHeader
        title="Lớp học phần"
        description="Mỗi lớp ghim vào một phiên bản nội dung đã xuất bản của môn học."
      />

      <Card>
        <CardContent>
          {offerings.length === 0 ? (
            <EmptyState
              Icon={GraduationCap}
              title="Chưa mở lớp nào"
              description="Lớp học phần mở từ một phiên bản môn học đã xuất bản. Soạn nội dung và xuất bản trước."
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
              {offerings.map((o) => (
                <li key={o.id}>
                  <Link
                    href={`/offerings/${o.id}`}
                    className="hover:bg-accent group flex items-center gap-3 rounded-lg px-3 py-2.5 transition-colors"
                  >
                    <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span className="truncate font-medium">{o.subject_name}</span>
                      <span className="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-0.5 text-sm">
                        {/* The subject code and class code are IDENTIFIERS, not prose —
                            font-mono keeps them aligned column-wise across rows. */}
                        <span className="font-mono text-xs">{o.subject_code}</span>
                        <span aria-hidden>·</span>
                        <span className="font-mono text-xs">{o.class_code}</span>
                        <span aria-hidden>·</span>
                        <span>{o.semester}</span>
                        <span aria-hidden>·</span>
                        <span>{o.enrollment_count} sinh viên</span>
                      </span>
                    </span>

                    <StatusBadge status={o.status} />
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
