"use client";

import { LogOut } from "lucide-react";
import { usePathname, useRouter } from "next/navigation";
import { useState } from "react";
import { Brand } from "./brand";
import { ThemeToggle } from "./theme-toggle";

/**
 * The "immersive" route: the learning screen for one specific class (`/learn/{offeringId}`).
 *
 * That page is a full-frame screen with its own bar carrying the class code and the progress
 * percentage — things a shared bar cannot know. Stacking another bar on top of it eats two
 * lines of a phone screen, at the most expensive spot in the whole student interface.
 *
 * Match EXACTLY one segment after `/learn/`: `/learn/{id}/assignments` is still an ordinary
 * page and still needs this bar.
 */
function isImmersivePage(pathname: string): boolean {
  return /^\/learn\/[^/]+$/.test(pathname);
}

export function StudentTopbar({ name, initial }: { name: string; initial: string }) {
  const pathname = usePathname();
  const router = useRouter();
  const [signingOut, setSigningOut] = useState(false);

  if (isImmersivePage(pathname)) return null;

  async function signOut() {
    setSigningOut(true);
    await fetch("/api/auth/logout", { method: "POST" });
    router.replace("/login");
    router.refresh();
  }

  return (
    <header className="bg-card/90 border-border sticky top-0 z-30 border-b backdrop-blur">
      {/* Width and padding match `PageShell` EXACTLY (max-w-5xl + p-4/sm:p-6/lg:p-8). Being
          off by a single step makes the logo sit indented relative to the title right below
          it, and the two read as two separate frames stacked on top of each other. Change the
          width in `PageShell` and you have to change it here too. */}
      <div className="mx-auto flex h-14 max-w-5xl items-center justify-between gap-3 px-4 sm:px-6 lg:px-8">
        <Brand href="/learn" />

        <div className="flex items-center gap-2">
          <span
            aria-hidden
            className="bg-primary/10 text-primary grid size-8 place-items-center rounded-full text-xs font-semibold"
          >
            {initial}
          </span>
          <span className="hidden truncate text-sm sm:inline">{name}</span>
          <ThemeToggle />
          <button
            type="button"
            onClick={signOut}
            disabled={signingOut}
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
