"use client";

import { Monitor, Moon, Sun } from "lucide-react";
import { useTheme } from "next-themes";
import { useSyncExternalStore } from "react";

const VONG = [
  { gia: "system", nhan: "Theo hệ thống", Icon: Monitor },
  { gia: "light", nhan: "Sáng", Icon: Sun },
  { gia: "dark", nhan: "Tối", Icon: Moon },
] as const;

/**
 * Công tắc chủ đề: một nút xoay vòng hệ thống → sáng → tối.
 *
 * Một nút thay vì menu ba lựa chọn: đây là thứ người dùng đụng tới vài lần trong đời, không
 * đáng chiếm chỗ của một menu. Nhãn hiện tại nằm ở `title` và `aria-label` nên vẫn đọc được
 * bằng trình đọc màn hình.
 */
export function DoiChuDe({ className = "" }: { className?: string }) {
  const { theme, setTheme } = useTheme();

  // Chủ đề chỉ biết được ở trình duyệt (localStorage + cài đặt máy). Render icon trước khi
  // hydrate xong là máy chủ và trình duyệt vẽ ra hai icon khác nhau — lỗi hydration thật,
  // khác với thẻ <html> vốn đã được miễn trừ. Giữ đúng kích thước để bố cục không nhảy.
  //
  // `useSyncExternalStore` chứ không phải `useState` + `useEffect(() => setDaGan(true))`:
  // cách kia gây một vòng render thừa và vi phạm `react-hooks/set-state-in-effect`. Ở đây
  // React lấy `false` khi dựng HTML trên máy chủ và `true` ở trình duyệt, đúng một lần,
  // không cần effect nào. Hàm subscribe trả về no-op vì giá trị này không bao giờ đổi nữa.
  const daGan = useSyncExternalStore(
    () => () => {},
    () => true,
    () => false,
  );

  const hienTai = VONG.find((v) => v.gia === theme) ?? VONG[0];
  const keTiep = VONG[(VONG.indexOf(hienTai) + 1) % VONG.length]!;
  const Icon = hienTai.Icon;

  return (
    <button
      type="button"
      onClick={() => setTheme(keTiep.gia)}
      title={`Giao diện: ${hienTai.nhan} — bấm để chuyển sang ${keTiep.nhan.toLowerCase()}`}
      aria-label={`Giao diện: ${hienTai.nhan}. Bấm để chuyển sang ${keTiep.nhan.toLowerCase()}.`}
      className={`text-muted-foreground hover:bg-accent hover:text-foreground grid size-11 place-items-center rounded-lg transition-colors ${className}`}
    >
      {daGan ? <Icon className="size-4" aria-hidden /> : <span className="size-4" />}
    </button>
  );
}
