import { ChevronLeft } from "lucide-react";
import Link from "next/link";
import type { ReactNode } from "react";

/**
 * The frame around a page's content.
 *
 * It exists so every screen shares one width, one set of margins, one vertical rhythm. Each
 * page used to declare `max-w-3xl` / `max-w-4xl` / `max-w-5xl` depending on who wrote it, so
 * moving between two screens shifted the content sideways — that feeling of looseness does
 * not come from the colors.
 *
 * `wide` is for the pages that genuinely need the horizontal room (gradebook, submission
 * comparison).
 */
export function PageShell({
  children,
  wide = false,
}: {
  children: ReactNode;
  wide?: boolean;
}) {
  return (
    <main
      className={`mx-auto flex w-full flex-col gap-6 p-4 sm:p-6 lg:p-8 ${
        wide ? "max-w-7xl" : "max-w-5xl"
      }`}
    >
      {children}
    </main>
  );
}

/**
 * Page header: back link, title, description, and an action area on the right.
 *
 * The action area sits on the same row as the title on wide screens but **wraps** on narrow
 * ones (`flex-wrap`) — forcing buttons onto the title's row on a phone squeezes the title
 * down to two words.
 */
export function PageHeader({
  title,
  description,
  backTo,
  actions,
}: {
  title: ReactNode;
  description?: ReactNode;
  backTo?: { href: string; label: string };
  actions?: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-3">
      {/* `py-3 -my-3` rather than just `min-h-11`: the tap area has to reach 44px for a
          finger, but letting it actually occupy 44px adds more than a line of height to the
          top of every page. The padding grows the touch area outward while the negative
          margin pulls the layout back where it was — visually it stays one small line of
          text. This is the PRIMARY back link on a phone; missing it means scrolling back up
          to find it again. */}
      {backTo && (
        <Link
          href={backTo.href}
          className="text-muted-foreground hover:text-foreground -my-3 -ml-1 inline-flex min-h-11 w-fit items-center gap-1 py-3 text-sm transition-colors"
        >
          <ChevronLeft className="size-4" aria-hidden />
          {backTo.label}
        </Link>
      )}

      <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-3">
        <div className="flex min-w-0 flex-col gap-1">
          <h1 className="text-2xl font-semibold tracking-tight text-balance">{title}</h1>
          {description && (
            <p className="text-muted-foreground text-sm text-pretty">{description}</p>
          )}
        </div>
        {actions && <div className="flex shrink-0 flex-wrap gap-2">{actions}</div>}
      </div>
    </div>
  );
}

/**
 * Empty state.
 *
 * The old version left a whole blank screen when there was no data, so "nothing here yet" and
 * "something broke" looked identical — the user could not tell whether to wait, to create
 * something, or to report a bug. Always say CLEARLY what is missing, and offer the next step
 * when there is one.
 */
export function EmptyState({
  Icon,
  title,
  description,
  action,
}: {
  Icon?: React.ComponentType<{ className?: string }>;
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <div className="border-border flex flex-col items-center gap-3 rounded-xl border border-dashed px-6 py-12 text-center">
      {Icon && (
        <span className="bg-muted text-muted-foreground grid size-11 place-items-center rounded-full">
          <Icon className="size-5" />
        </span>
      )}
      <div className="flex flex-col gap-1">
        <p className="font-medium">{title}</p>
        {description && (
          <p className="text-muted-foreground max-w-sm text-sm text-pretty">{description}</p>
        )}
      </div>
      {action}
    </div>
  );
}

/**
 * Horizontal scroll container for wide content (gradebook, submission lists).
 *
 * Rule §9 of the design doc: **this block** scrolls, not the whole page. Wrapping a table in
 * a plain `div` makes the entire page scroll sideways on a phone, and at that point nothing
 * on it is readable anymore.
 */
export function HorizontalScroll({ children }: { children: ReactNode }) {
  return <div className="-mx-1 overflow-x-auto px-1">{children}</div>;
}
