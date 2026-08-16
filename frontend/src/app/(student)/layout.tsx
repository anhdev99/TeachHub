import { redirect } from "next/navigation";
import { ThanhTrenSv } from "@/components/shell/thanh-tren-sv";
import { chuDau, layNguoiDung } from "@/lib/api/me";

/**
 * Khung chung cho vai sinh viên. Tối giản có chủ đích: SRS mục 9 nói sinh viên chủ yếu xem
 * trên điện thoại, nên mọi pixel chiều dọc dành cho điều hướng là pixel lấy đi của nội dung.
 * Không có sidebar — sinh viên chỉ có vài đường đi, và tất cả đều xuất phát từ trang lớp.
 *
 * KHÔNG chặn theo vai ở đây. Giảng viên mở link bài học của lớp mình là việc hợp lệ và hay
 * xảy ra (xem sinh viên đang thấy gì); phần API vẫn tự kiểm quyền trên từng endpoint, nên
 * chặn ở đây chỉ tạo ra một ngõ cụt không có lý do.
 */
export default async function StudentLayout({ children }: LayoutProps<"/">) {
  const me = await layNguoiDung();
  if (!me) redirect("/login");

  return (
    <div className="flex min-h-svh flex-col">
      <ThanhTrenSv ten={me.full_name ?? "Sinh viên"} chu={chuDau(me.full_name)} />
      {children}
    </div>
  );
}
