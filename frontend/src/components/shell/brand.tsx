import Link from "next/link";

/**
 * The TeachHub wordmark. Deliberately not an image: a rounded square with a letter in it is
 * enough, and because it follows the primary color it comes out right in both light and dark
 * mode — an image file would need two versions and one of them would be forgotten.
 */
export function Brand({ href = "/", subtitle }: { href?: string; subtitle?: string }) {
  return (
    // `min-h-11`: the "T" tile is only 32px tall, and this is the way home from the student
    // top bar — a surface designed for fingers throughout. The bar is 56px tall, so 44px fits.
    <Link href={href} className="flex min-h-11 min-w-0 items-center gap-2.5">
      <span
        aria-hidden
        className="bg-primary text-primary-foreground grid size-8 shrink-0 place-items-center rounded-lg text-sm font-bold"
      >
        T
      </span>
      <span className="flex min-w-0 flex-col leading-tight">
        <span className="truncate text-sm font-semibold tracking-tight">TeachHub</span>
        {subtitle && (
          <span className="text-muted-foreground truncate text-xs">{subtitle}</span>
        )}
      </span>
    </Link>
  );
}
