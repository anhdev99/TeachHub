import { SearchX } from "lucide-react";
import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { DauTrang, Trang, TrongRong } from "@/components/shell/trang";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { MyProgressDto } from "@/lib/api/types";
import { TrangHoc } from "./trang-hoc";

/** Màn hình 6.3 — trang học của sinh viên. */
export default async function LearnOfferingPage({
  params,
}: {
  params: Promise<{ offeringId: string }>;
}) {
  const { offeringId } = await params;

  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/offerings/${offeringId}/my-progress`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (res.status === 403) {
    const problem = await res.json().catch(() => null);
    if (problem?.code === "must_change_password") redirect("/doi-mat-khau");
  }

  if (!res.ok) {
    // Thanh trên dùng chung TỰ ẨN ở đúng route này (trang học là màn hình toàn khung), nên
    // trạng thái lỗi phải tự mang đường quay lại — nếu không thì đây là một ngõ cụt.
    //
    // Backend trả 404 cho cả "không tồn tại" lẫn "không phải của bạn" (cố ý, để không xác
    // nhận id có thật). Vì vậy KHÔNG được viết "bạn không có quyền": câu đó tiết lộ đúng thứ
    // backend đang giấu.
    return (
      <Trang>
        <DauTrang tieuDe="Trang học" quayLai={{ href: "/learn", nhan: "Lớp của tôi" }} />
        <TrongRong
          Icon={SearchX}
          tieuDe="Không tìm thấy lớp học"
          moTa="Lớp này không tồn tại hoặc bạn không có tên trong danh sách lớp. Hỏi lại giảng viên mã lớp rồi thử vào bằng mã."
          hanhDong={
            <Link
              href="/learn"
              className="bg-primary text-primary-foreground inline-flex min-h-11 items-center rounded-lg px-4 text-sm font-medium"
            >
              Về Lớp của tôi
            </Link>
          }
        />
      </Trang>
    );
  }

  const progress: MyProgressDto = await res.json();
  return <TrangHoc offeringId={offeringId} banDau={progress} />;
}
