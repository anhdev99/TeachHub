import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import { Providers } from "./providers";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
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
