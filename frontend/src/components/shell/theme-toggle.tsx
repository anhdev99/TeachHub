"use client";

import { Monitor, Moon, Sun } from "lucide-react";
import { useTheme } from "next-themes";
import { useSyncExternalStore } from "react";

const CYCLE = [
  { value: "system", label: "Theo hệ thống", Icon: Monitor },
  { value: "light", label: "Sáng", Icon: Sun },
  { value: "dark", label: "Tối", Icon: Moon },
] as const;

/**
 * Theme switch: one button cycling system → light → dark.
 *
 * A single button instead of a three-option menu: this is something a user touches a handful
 * of times in their life, and it does not deserve a menu's worth of space. The current label
 * lives in `title` and `aria-label`, so it is still readable by a screen reader.
 */
export function ThemeToggle({ className = "" }: { className?: string }) {
  const { theme, setTheme } = useTheme();

  // The theme is only knowable in the browser (localStorage + OS setting). Rendering the icon
  // before hydration finishes means the server and the browser draw two different icons — a
  // real hydration error, unlike the <html> tag which is explicitly exempted. Keep the size
  // reserved so the layout does not jump.
  //
  // `useSyncExternalStore` rather than `useState` + `useEffect(() => setMounted(true))`: that
  // approach costs an extra render pass and violates `react-hooks/set-state-in-effect`. Here
  // React takes `false` while building HTML on the server and `true` in the browser, exactly
  // once, with no effect at all. The subscribe function is a no-op because this value never
  // changes again.
  const mounted = useSyncExternalStore(
    () => () => {},
    () => true,
    () => false,
  );

  const current = CYCLE.find((c) => c.value === theme) ?? CYCLE[0];
  const next = CYCLE[(CYCLE.indexOf(current) + 1) % CYCLE.length]!;
  const Icon = current.Icon;

  return (
    <button
      type="button"
      onClick={() => setTheme(next.value)}
      title={`Giao diện: ${current.label} — bấm để chuyển sang ${next.label.toLowerCase()}`}
      aria-label={`Giao diện: ${current.label}. Bấm để chuyển sang ${next.label.toLowerCase()}.`}
      className={`text-muted-foreground hover:bg-accent hover:text-foreground grid size-11 place-items-center rounded-lg transition-colors ${className}`}
    >
      {mounted ? <Icon className="size-4" aria-hidden /> : <span className="size-4" />}
    </button>
  );
}
