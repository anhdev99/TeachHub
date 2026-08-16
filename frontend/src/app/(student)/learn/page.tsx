import { ChevronRight, GraduationCap } from "lucide-react";
import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { Card, CardContent } from "@/components/ui/card";
import { DauTrang, Trang, TrongRong } from "@/components/shell/trang";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { MyOfferingDto } from "@/lib/api/types";
import { NhapMaLop } from "./nhap-ma-lop";

export default async function LearnPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/me/offerings`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  // 403 kèm must_change_password: middleware chặn cho tới khi đổi mật khẩu lần đầu.
  if (res.status === 403) {
    const problem = await res.json().catch(() => null);
    if (problem?.code === "must_change_password") redirect("/doi-mat-khau");
  }
  if (!res.ok) redirect("/login");

  const offerings: MyOfferingDto[] = await res.json();

  return (
    <Trang>
      <DauTrang
        tieuDe="Lớp của tôi"
        moTa="Chọn một lớp để mở trang học."
        // Khi chưa có lớp nào thì nút này chuyển xuống trạng thái rỗng: đặt cả hai chỗ là
        // hai nút giống hệt nhau trên cùng một màn hình, và người dùng phải đoán xem chúng
        // có làm cùng một việc không.
        hanhDong={offerings.length > 0 ? <NhapMaLop /> : undefined}
      />

      {offerings.length === 0 ? (
        <TrongRong
          Icon={GraduationCap}
          tieuDe="Bạn chưa vào lớp nào"
          moTa="Giảng viên đọc mã lớp gồm 6 ký tự trên lớp. Nhập mã đó để vào lớp đầu tiên của bạn."
          hanhDong={<NhapMaLop />}
        />
      ) : (
        <Card>
          <CardContent className="px-2">
            <ul className="flex flex-col gap-1">
              {offerings.map((o) => (
                <li key={o.id}>
                  <Link
                    href={`/learn/${o.id}`}
                    className="hover:bg-accent group flex min-h-11 items-center gap-3 rounded-lg px-3 py-2.5 transition-colors"
                  >
                    <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span className="truncate font-medium">{o.subject_name}</span>
                      <span className="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-0.5 text-sm">
                        {/* Mã môn và mã lớp là ĐỊNH DANH, không phải văn xuôi — chữ đều
                            giúp đối chiếu với bảng của giảng viên nhanh hơn hẳn. */}
                        <span className="font-mono text-xs">{o.subject_code}</span>
                        <span aria-hidden>·</span>
                        <span className="font-mono text-xs">{o.class_code}</span>
                        <span aria-hidden>·</span>
                        <span>{o.semester}</span>
                      </span>
                    </span>

                    <ChevronRight
                      className="text-muted-foreground group-hover:text-foreground size-4 shrink-0 transition-colors"
                      aria-hidden
                    />
                  </Link>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </Trang>
  );
}
