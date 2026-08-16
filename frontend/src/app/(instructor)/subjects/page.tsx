import { BookOpen, ChevronRight } from "lucide-react";
import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { DauTrang, Trang, TrongRong } from "@/components/shell/trang";
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
    <Trang>
      <DauTrang
        tieuDe="Môn học"
        moTa="Mỗi môn giữ nhiều phiên bản nội dung; lớp học phần ghim vào một phiên bản đã xuất bản."
        hanhDong={<TaoMonMoi />}
      />

      <Card>
        <CardContent>
          {subjects.length === 0 ? (
            <TrongRong
              Icon={BookOpen}
              tieuDe="Chưa có môn học nào"
              moTa="Tạo môn đầu tiên, soạn nội dung trong một phiên bản nháp, rồi xuất bản để mở lớp."
            />
          ) : (
            <ul className="flex flex-col gap-1.5">
              {subjects.map((s) => (
                <li key={s.id}>
                  <Link
                    href={`/subjects/${s.id}/versions`}
                    className="hover:bg-accent group flex items-center gap-3 rounded-lg px-3 py-2.5 transition-colors"
                  >
                    <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span className="truncate font-medium">{s.name}</span>
                      <span className="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-0.5 text-sm">
                        <span className="font-mono text-xs">{s.code}</span>
                        {s.description && (
                          <>
                            <span aria-hidden>·</span>
                            <span className="truncate">{s.description}</span>
                          </>
                        )}
                      </span>
                    </span>

                    {/* Xanh = có bản đang dùng được để mở lớp; xám = mới chỉ có bản nháp.
                        Đây là thông tin quyết định "mở lớp được chưa", nên nó phải đọc
                        được từ danh sách chứ không phải bấm vào mới biết. */}
                    {s.current_version_id ? (
                      <Badge className="bg-ok-soft text-ok border-ok/25 shrink-0 border">
                        Đã xuất bản
                      </Badge>
                    ) : (
                      <Badge variant="secondary" className="shrink-0">
                        Chưa xuất bản
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
