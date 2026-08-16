import { redirect } from "next/navigation";
import { DieuHuongGv } from "@/components/shell/dieu-huong-gv";
import { chuDau, layNguoiDung } from "@/lib/api/me";

/**
 * Khung chung cho mọi màn hình của giảng viên.
 *
 * Trước khi có file này, mỗi trang là một `<main>` trần: từ Bảng điều khiển KHÔNG có đường
 * nào đi tới Môn học hay Ngân hàng câu hỏi — phải tự gõ URL. Đó không phải chuyện thẩm mỹ mà
 * là thiếu hẳn một tầng: bản thiết kế đã quy định route group của mỗi vai phải có layout
 * riêng, và nó chưa từng được dựng.
 *
 * Đặt ở layout của route group nên **mọi trang thêm về sau tự có điều hướng** mà không phải
 * nhớ gì. Nếu để mỗi trang tự vẽ sidebar thì trang thứ mười sẽ quên, và nó sẽ là trang trông
 * như bị hỏng.
 */
export default async function InstructorLayout({ children }: LayoutProps<"/">) {
  const me = await layNguoiDung();
  if (!me) redirect("/login");

  // Sinh viên lạc vào nhánh giảng viên thì đưa về trang học, không hiện một khung điều hướng
  // toàn mục bấm vào đâu cũng 404.
  if (!me.roles?.includes("Instructor")) redirect("/learn");

  return (
    <div className="lg:pl-[17rem]">
      <DieuHuongGv
        ten={me.full_name ?? "Giảng viên"}
        email={me.email ?? ""}
        chu={chuDau(me.full_name)}
      />
      {children}
    </div>
  );
}
