import { cookies } from "next/headers";
import { API_BASE, COOKIE_ACCESS } from "./server";

export type NguoiDung = {
  full_name?: string;
  email?: string;
  roles?: string[];
};

/**
 * Đọc người dùng hiện tại ở phía máy chủ.
 *
 * Tách ra khỏi từng trang vì layout của mỗi vai cần nó để dựng thanh điều hướng, và nếu mỗi
 * trang tự gọi thì một trang quên kiểm token là một trang render với dữ liệu rỗng thay vì
 * chuyển về đăng nhập. Trả `null` khi chưa đăng nhập để nơi gọi tự quyết định chuyển hướng —
 * `redirect()` gọi từ trong hàm dùng chung sẽ khiến nơi gọi khó suy ra luồng điều khiển.
 */
export async function layNguoiDung(): Promise<NguoiDung | null> {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) return null;

  const res = await fetch(`${API_BASE}/api/auth/me`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (!res.ok) return null;
  return (await res.json()) as NguoiDung;
}

/** Chữ cái đầu để dựng avatar khi chưa có ảnh. */
export function chuDau(ten?: string): string {
  const t = (ten ?? "").trim();
  if (!t) return "?";
  const phan = t.split(/\s+/);
  // Tiếng Việt viết họ trước tên, mà người ta nhớ nhau bằng TÊN — nên lấy chữ cái của từ
  // cuối cùng làm chính: "Nguyễn Văn An" ra "A", không phải "N" (trùng với nửa lớp họ Nguyễn).
  return phan[phan.length - 1]!.charAt(0).toUpperCase();
}
