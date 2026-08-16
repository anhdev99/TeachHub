import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import { Providers } from "./providers";

// ⚠️ "vietnamese" là BẮT BUỘC, không phải tuỳ chọn cho đẹp.
//
// Bộ "latin" KHÔNG chứa các ký tự có dấu của tiếng Việt (ế, ộ, ữ, ằ, ị, đ…). Thiếu nó thì
// trình duyệt vẫn hiện chữ — nhưng hiện bằng font DỰ PHÒNG của hệ thống cho riêng những ký tự
// đó, nên chữ lệch nét ngay giữa một từ ("Bảng điều khiển" ghép từ hai font khác nhau). Không
// có lỗi nào báo, và đây là thứ làm cả sản phẩm trông cẩu thả mà rất khó chỉ ra nguyên nhân.
//
// "latin-ext" giữ lại vì vẫn cần cho ký tự Âu khác lọt vào (tên riêng, thuật ngữ).
const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin", "latin-ext", "vietnamese"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin", "latin-ext", "vietnamese"],
});

export const metadata: Metadata = {
  title: "TeachHub",
  description: "Hệ thống quản lý dạy học cá nhân",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    // lang="vi" chứ không phải "en": ảnh hưởng cách trình duyệt ngắt dòng, kiểm chính tả
    // và cách trình đọc màn hình phát âm nội dung tiếng Việt.
    <html
      lang="vi"
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
