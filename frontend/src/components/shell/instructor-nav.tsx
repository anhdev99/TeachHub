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
import { Brand } from "./brand";
import { ThemeToggle } from "./theme-toggle";

const NAV_ITEMS = [
  { href: "/dashboard", label: "Tổng quan", Icon: LayoutDashboard },
  { href: "/offerings", label: "Lớp học phần", Icon: GraduationCap },
  { href: "/subjects", label: "Môn học", Icon: BookOpen },
  { href: "/questions", label: "Ngân hàng câu hỏi", Icon: ListChecks },
  { href: "/rubrics", label: "Bộ tiêu chí chấm", Icon: ClipboardList },
] as const;

/**
 * Match the selected item by PREFIX, not by equality.
 *
 * `/offerings/{id}/gradebook` still has to highlight "Lớp học phần" — with an equality check
 * the user loses all trace of which branch they are in the moment they go deep into a class,
 * and the nav bar becomes decoration. The boundary is guarded with a `/` so `/subjects` will
 * not swallow some future `/subjectsX` route.
 */
function isActive(pathname: string, href: string): boolean {
  return pathname === href || pathname.startsWith(`${href}/`);
}

function NavList({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();

  return (
    <nav className="flex flex-col gap-0.5">
      {NAV_ITEMS.map(({ href, label, Icon }) => {
        const active = isActive(pathname, href);
        return (
          <Link
            key={href}
            href={href}
            onClick={onNavigate}
            aria-current={active ? "page" : undefined}
            className={`flex min-h-11 items-center gap-2.5 rounded-lg px-3 text-sm transition-colors ${
              active
                ? "bg-sidebar-accent text-sidebar-accent-foreground font-medium"
                : "text-sidebar-foreground hover:bg-sidebar-accent/50"
            }`}
          >
            <Icon className="size-4 shrink-0" aria-hidden />
            <span className="truncate">{label}</span>
          </Link>
        );
      })}
    </nav>
  );
}

function SidebarFooter({
  name,
  email,
  initial,
}: {
  name: string;
  email: string;
  initial: string;
}) {
  const router = useRouter();
  const [signingOut, setSigningOut] = useState(false);

  async function signOut() {
    setSigningOut(true);
    await fetch("/api/auth/logout", { method: "POST" });
    // `refresh()` so server components re-read the now-deleted cookie; without it the old
    // render stays in the router cache and the user still looks signed in.
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
          {initial}
        </span>
        <span className="flex min-w-0 flex-col leading-tight">
          <span className="truncate text-sm font-medium">{name}</span>
          <span className="text-muted-foreground truncate text-xs">{email}</span>
        </span>
      </div>

      <div className="flex items-center gap-1">
        <button
          type="button"
          onClick={signOut}
          disabled={signingOut}
          className="text-muted-foreground hover:bg-sidebar-accent/60 hover:text-foreground flex min-h-11 flex-1 items-center gap-2.5 rounded-lg px-3 text-sm transition-colors disabled:opacity-60"
        >
          <LogOut className="size-4 shrink-0" aria-hidden />
          {signingOut ? "Đang thoát…" : "Đăng xuất"}
        </button>
        <ThemeToggle />
      </div>
    </div>
  );
}

export function InstructorNav({
  name,
  email,
  initial,
}: {
  name: string;
  email: string;
  initial: string;
}) {
  const [open, setOpen] = useState(false);
  const pathname = usePathname();

  // Close the drawer when the page changes. Without this line, tapping an item on a phone
  // leaves the drawer covering the page that just opened, and the user thinks the tap did
  // nothing. Still needed even though the items already call `setOpen(false)`: the browser's
  // Back button changes the page without going through any item.
  //
  // Adjust the state DURING RENDER rather than inside a `useEffect`. This is React's
  // recommended pattern for "reset state when a prop changes": React notices and re-renders
  // immediately before painting, so no frame ever escapes with the drawer still open. Doing
  // it in an effect changes the state after painting — one wasted render and a visible flash.
  const [lastPath, setLastPath] = useState(pathname);
  if (pathname !== lastPath) {
    setLastPath(pathname);
    setOpen(false);
  }

  // Lock background scrolling while the drawer is open, otherwise swiping inside the drawer
  // scrolls the page behind it.
  useEffect(() => {
    if (!open) return;
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = previous;
    };
  }, [open]);

  // Esc closes the drawer — the same expectation as every other overlay.
  useEffect(() => {
    if (!open) return;
    const onKeyDown = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [open]);

  return (
    <>
      {/* ── Top bar, narrow screens only ─────────────────────────────────────────────── */}
      <header className="bg-sidebar border-sidebar-border sticky top-0 z-30 flex h-14 items-center gap-2 border-b px-3 lg:hidden">
        <button
          type="button"
          onClick={() => setOpen(true)}
          aria-label="Mở menu"
          aria-expanded={open}
          className="hover:bg-sidebar-accent grid size-11 shrink-0 place-items-center rounded-lg"
        >
          <Menu className="size-5" aria-hidden />
        </button>
        <Brand href="/dashboard" />
      </header>

      {/* ── Drawer scrim ─────────────────────────────────────────────────────────────── */}
      {open && (
        <button
          type="button"
          aria-label="Đóng menu"
          onClick={() => setOpen(false)}
          className="fixed inset-0 z-40 bg-black/40 lg:hidden"
        />
      )}

      {/* ── Sidebar: pinned from lg up, a drawer below that ──────────────────────────── */}
      <aside
        className={`bg-sidebar border-sidebar-border fixed inset-y-0 left-0 z-50 flex w-[17rem] flex-col gap-4 border-r p-3 transition-transform duration-200 lg:translate-x-0 ${
          open ? "translate-x-0" : "-translate-x-full"
        }`}
      >
        <div className="flex items-center justify-between gap-2 px-1 pt-1">
          <Brand href="/dashboard" subtitle="Giảng viên" />
          <button
            type="button"
            onClick={() => setOpen(false)}
            aria-label="Đóng menu"
            className="hover:bg-sidebar-accent grid size-9 shrink-0 place-items-center rounded-lg lg:hidden"
          >
            <X className="size-4" aria-hidden />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto">
          <NavList onNavigate={() => setOpen(false)} />
        </div>

        <SidebarFooter name={name} email={email} initial={initial} />
      </aside>
    </>
  );
}
