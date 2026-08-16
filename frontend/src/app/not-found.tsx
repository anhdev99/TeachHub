import Link from "next/link";

/**
 * The application-wide 404 page.
 *
 * Why this file is needed: the backend deliberately returns **404 and never 403** for a
 * resource that does not belong to the caller (ADR-012) — a 403 confirms that the id exists,
 * which leaks precisely what we just hid. So 404 here is not a rare case, it is the **main
 * path** of every ownership check: 14 places in `src/app` call `notFound()`. Without this
 * file, all of them render Next's default page — "This page could not be found." — in
 * English, in the middle of a Vietnamese product.
 *
 * The wording here must be "không tìm thấy" and ABSOLUTELY NOT "bạn không có quyền": saying
 * "you don't have permission" states out loud that the resource exists, which is exactly what
 * returning 404 instead of 403 was meant to conceal.
 */
export default function NotFound() {
  return (
    <main className="mx-auto flex min-h-svh max-w-md flex-col items-center justify-center gap-4 p-6 text-center">
      <p className="text-muted-foreground text-5xl font-semibold">404</p>

      <h1 className="text-xl font-semibold">Không tìm thấy nội dung này</h1>

      <p className="text-muted-foreground text-sm">
        Nội dung có thể đã bị xoá, hoặc đường dẫn không đúng.
      </p>

      <Link
        href="/"
        className="border-border hover:bg-accent inline-flex min-h-11 items-center rounded-md border px-4 text-sm"
      >
        Về trang chủ
      </Link>
    </main>
  );
}
