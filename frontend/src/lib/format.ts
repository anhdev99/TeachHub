/**
 * Date and time formatting for Vietnamese readers.
 *
 * The old code dumped `new Date(x).toLocaleString()` straight onto the screen, producing
 * strings like `11:38:48 16/8/2026`: seconds nobody needs, no leading zeros, and a
 * time-before-date order that reads backwards from local habit. Inside a list those strings
 * come out at different lengths, so the time column looks ragged — a small detail, but one
 * that repeats on every screen.
 */

const DATE_TIME_FORMAT = new Intl.DateTimeFormat("vi-VN", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hour12: false,
});

const DATE_FORMAT = new Intl.DateTimeFormat("vi-VN", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
});

/** `16/08/2026 11:38` */
export function formatDateTime(iso?: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  // Intl returns "16/08/2026, 11:38" — drop the comma so it stays compact inside a table cell.
  return DATE_TIME_FORMAT.format(d).replace(",", "");
}

/** `16/08/2026` */
export function formatDate(iso?: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "—" : DATE_FORMAT.format(d);
}

/**
 * Relative distance in time: "2 giờ trước", "trong 3 ngày".
 *
 * For places where the real question is "was this recent / am I still in time?" rather than
 * "exactly when?" — the grading queue, submission deadlines. Always pair it with the absolute
 * timestamp in a `title` attribute so anyone who needs the precise number can still get it;
 * showing only the relative form throws information away.
 *
 * `Intl.RelativeTimeFormat` instead of a hand-written phrase table: hand-written tables miss
 * plural cases and the "yesterday" case, and they break at exactly the rare boundaries nobody
 * ever tests.
 */
const RELATIVE_FORMAT = new Intl.RelativeTimeFormat("vi-VN", { numeric: "auto" });

const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ["year", 1000 * 60 * 60 * 24 * 365],
  ["month", 1000 * 60 * 60 * 24 * 30],
  ["day", 1000 * 60 * 60 * 24],
  ["hour", 1000 * 60 * 60],
  ["minute", 1000 * 60],
];

export function formatRelative(iso?: string | null, from: Date = new Date()): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";

  const delta = d.getTime() - from.getTime();

  for (const [unit, ms] of UNITS) {
    if (Math.abs(delta) >= ms) {
      // Truncate toward zero (`trunc`), not `round`: 1.9 hours ago has to read "1 giờ trước",
      // not "2 giờ trước" — overstating it is wrong in the most irritating direction.
      return RELATIVE_FORMAT.format(Math.trunc(delta / ms), unit);
    }
  }
  return "vừa xong";
}

/** Score: drop trailing zeros, keep the Vietnamese decimal comma. */
export function formatScore(x?: number | null): string {
  if (x === null || x === undefined) return "—";
  return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(x);
}
