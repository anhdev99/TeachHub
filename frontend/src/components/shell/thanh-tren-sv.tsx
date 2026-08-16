"use client";

import { LogOut } from "lucide-react";
import { usePathname, useRouter } from "next/navigation";
import { useState } from "react";
import { Thuonghieu } from "./thuong-hieu";

/**
 * Route "đắm chìm": trang học một lớp cụ thể (`/learn/{offeringId}`).
 *
 * Trang đó là màn hình toàn khung, tự có thanh riêng mang mã lớp và phần trăm tiến độ — thứ
 * mà một thanh dùng chung không thể biết. Chồng thêm thanh nữa lên trên là ăn mất hai dòng
 * trên màn hình điện thoại, đúng chỗ đắt nhất của giao diện sinh viên.
 *
 * Khớp CHÍNH XÁC một đoạn sau `/learn/`: `/learn/{id}/assignments` vẫn là trang thường và
 * vẫn cần thanh này.
 */
function laTrangDamChim(pathname: string): boolean {
  return /^\/learn\/[^/]+$/.test(pathname);
}

export function ThanhTrenSv({ ten, chu }: { ten: string; chu: string }) {
  const pathname = usePathname();
  const router = useRouter();
  const [dangThoat, setDangThoat] = useState(false);

  if (laTrangDamChim(pathname)) return null;

  async function thoat() {
    setDangThoat(true);
    await fetch("/api/auth/logout", { method: "POST" });
    router.replace("/login");
    router.refresh();
  }

  return (
    <header className="bg-card/90 border-border sticky top-0 z-30 border-b backdrop-blur">
      <div className="mx-auto flex h-14 max-w-3xl items-center justify-between gap-3 px-4">
        <Thuonghieu href="/learn" />

        <div className="flex items-center gap-2">
          <span
            aria-hidden
            className="bg-primary/10 text-primary grid size-8 place-items-center rounded-full text-xs font-semibold"
          >
            {chu}
          </span>
          <span className="hidden truncate text-sm sm:inline">{ten}</span>
          <button
            type="button"
            onClick={thoat}
            disabled={dangThoat}
            aria-label="Đăng xuất"
            title="Đăng xuất"
            className="text-muted-foreground hover:bg-accent hover:text-foreground grid size-11 place-items-center rounded-lg transition-colors disabled:opacity-60"
          >
            <LogOut className="size-4" aria-hidden />
          </button>
        </div>
      </div>
    </header>
  );
}
