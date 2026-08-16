"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ThemeProvider } from "next-themes";
import { useState } from "react";

export function Providers({ children }: { children: React.ReactNode }) {
  // Tạo QueryClient trong state chứ không phải module scope: ở server, module scope bị
  // chia sẻ giữa các request và dữ liệu của giảng viên này sẽ rò sang giảng viên khác.
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false },
        },
      }),
  );

  return (
    // `attribute="class"` phải khớp với `@custom-variant dark (&:is(.dark *))` trong
    // globals.css — next-themes mặc định ghi `data-theme`, mà biến thể `dark` của dự án lại
    // đọc class, nên để mặc định thì bảng màu tối có đủ nhưng không bao giờ được kích hoạt.
    //
    // `defaultTheme="system"` để theo cài đặt máy: giảng viên chấm bài đêm khuya không phải
    // đi tìm công tắc, còn ai muốn khác hệ thống thì vẫn tự đổi được.
    <ThemeProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      disableTransitionOnChange
    >
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    </ThemeProvider>
  );
}
