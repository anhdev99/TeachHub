"use client";

import {
  BookOpen,
  ClipboardList,
  GraduationCap,
  LayoutDashboard,
  ListChecks,
  LogOut,
  Menu,
  X,
} from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Thuonghieu } from "./thuong-hieu";

const MUC = [
  { href: "/dashboard", nhan: "Tổng quan", Icon: LayoutDashboard },
  { href: "/offerings", nhan: "Lớp học phần", Icon: GraduationCap },
  { href: "/subjects", nhan: "Môn học", Icon: BookOpen },
  { href: "/questions", nhan: "Ngân hàng câu hỏi", Icon: ListChecks },
  { href: "/rubrics", nhan: "Bộ tiêu chí chấm", Icon: ClipboardList },
] as const;

/**
 * So khớp mục đang chọn theo TIỀN TỐ, không phải so bằng.
 *
 * `/offerings/{id}/gradebook` vẫn phải làm sáng mục "Lớp học phần" — so bằng thì người dùng
 * đi sâu vào một lớp là mất luôn dấu vết mình đang ở nhánh nào, và thanh điều hướng thành đồ
 * trang trí. Chặn ranh giới bằng dấu `/` để `/subjects` không nuốt một route `/subjectsX`
 * nào đó về sau.
 */
function dangChon(pathname: string, href: string): boolean {
  return pathname === href || pathname.startsWith(`${href}/`);
}

function DanhSachMuc({ onDieuHuong }: { onDieuHuong?: () => void }) {
  const pathname = usePathname();

  return (
    <nav className="flex flex-col gap-0.5">
      {MUC.map(({ href, nhan, Icon }) => {
        const chon = dangChon(pathname, href);
        return (
          <Link
            key={href}
            href={href}
            onClick={onDieuHuong}
            aria-current={chon ? "page" : undefined}
            className={`flex min-h-11 items-center gap-2.5 rounded-lg px-3 text-sm transition-colors ${
              chon
                ? "bg-sidebar-accent text-sidebar-accent-foreground font-medium"
                : "text-sidebar-foreground hover:bg-sidebar-accent/50"
            }`}
          >
            <Icon className="size-4 shrink-0" aria-hidden />
            <span className="truncate">{nhan}</span>
          </Link>
        );
      })}
    </nav>
  );
}

function ChanSidebar({ ten, email, chu }: { ten: string; email: string; chu: string }) {
  const router = useRouter();
  const [dangThoat, setDangThoat] = useState(false);

  async function thoat() {
    setDangThoat(true);
    await fetch("/api/auth/logout", { method: "POST" });
    // `refresh()` để server component đọc lại cookie đã bị xoá; thiếu nó thì bản render cũ
    // vẫn nằm trong bộ nhớ đệm của router và người dùng thấy mình vẫn đang đăng nhập.
    router.replace("/login");
    router.refresh();
  }

  return (
    <div className="border-sidebar-border flex flex-col gap-2 border-t pt-3">
      <div className="flex items-center gap-2.5 px-1 min-w-0">
        <span
          aria-hidden
          className="bg-primary/10 text-primary grid size-8 shrink-0 place-items-center rounded-full text-xs font-semibold"
        >
          {chu}
        </span>
        <span className="flex min-w-0 flex-col leading-tight">
          <span className="truncate text-sm font-medium">{ten}</span>
          <span className="text-muted-foreground truncate text-xs">{email}</span>
        </span>
      </div>

      <button
        type="button"
        onClick={thoat}
        disabled={dangThoat}
        className="text-muted-foreground hover:bg-sidebar-accent/60 hover:text-foreground flex min-h-11 items-center gap-2.5 rounded-lg px-3 text-sm transition-colors disabled:opacity-60"
      >
        <LogOut className="size-4 shrink-0" aria-hidden />
        {dangThoat ? "Đang thoát…" : "Đăng xuất"}
      </button>
    </div>
  );
}

export function DieuHuongGv({
  ten,
  email,
  chu,
}: {
  ten: string;
  email: string;
  chu: string;
}) {
  const [mo, setMo] = useState(false);
  const pathname = usePathname();

  // Đổi trang thì đóng ngăn kéo. Không có dòng này thì trên điện thoại, bấm một mục xong
  // ngăn kéo vẫn che kín trang vừa mở, và người dùng tưởng bấm không ăn.
  useEffect(() => setMo(false), [pathname]);

  // Khoá cuộn nền khi ngăn kéo mở, nếu không thì vuốt trong ngăn kéo lại cuộn trang phía sau.
  useEffect(() => {
    if (!mo) return;
    const cu = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = cu;
    };
  }, [mo]);

  // Phím Esc đóng ngăn kéo — cùng kỳ vọng với mọi lớp phủ khác.
  useEffect(() => {
    if (!mo) return;
    const h = (e: KeyboardEvent) => e.key === "Escape" && setMo(false);
    window.addEventListener("keydown", h);
    return () => window.removeEventListener("keydown", h);
  }, [mo]);

  return (
    <>
      {/* ── Thanh trên, chỉ ở màn hình hẹp ───────────────────────────────────────────── */}
      <header className="bg-sidebar border-sidebar-border sticky top-0 z-30 flex h-14 items-center gap-2 border-b px-3 lg:hidden">
        <button
          type="button"
          onClick={() => setMo(true)}
          aria-label="Mở menu"
          aria-expanded={mo}
          className="hover:bg-sidebar-accent grid size-11 shrink-0 place-items-center rounded-lg"
        >
          <Menu className="size-5" aria-hidden />
        </button>
        <Thuonghieu href="/dashboard" />
      </header>

      {/* ── Lớp phủ của ngăn kéo ─────────────────────────────────────────────────────── */}
      {mo && (
        <button
          type="button"
          aria-label="Đóng menu"
          onClick={() => setMo(false)}
          className="fixed inset-0 z-40 bg-black/40 lg:hidden"
        />
      )}

      {/* ── Sidebar: cố định từ lg trở lên, ngăn kéo bên dưới ────────────────────────── */}
      <aside
        className={`bg-sidebar border-sidebar-border fixed inset-y-0 left-0 z-50 flex w-[17rem] flex-col gap-4 border-r p-3 transition-transform duration-200 lg:translate-x-0 ${
          mo ? "translate-x-0" : "-translate-x-full"
        }`}
      >
        <div className="flex items-center justify-between gap-2 px-1 pt-1">
          <Thuonghieu href="/dashboard" phu="Giảng viên" />
          <button
            type="button"
            onClick={() => setMo(false)}
            aria-label="Đóng menu"
            className="hover:bg-sidebar-accent grid size-9 shrink-0 place-items-center rounded-lg lg:hidden"
          >
            <X className="size-4" aria-hidden />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto">
          <DanhSachMuc onDieuHuong={() => setMo(false)} />
        </div>

        <ChanSidebar ten={ten} email={email} chu={chu} />
      </aside>
    </>
  );
}
