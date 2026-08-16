"use client"; // An error boundary must be a Client Component (Next.js convention).

import { useEffect } from "react";

/**
 * The shared error boundary. Same reason as `not-found.tsx`: without this file, every
 * uncaught error shows Next's default English page.
 *
 * ⚠️ Next 16 names the prop `retry`, NOT `reset` as in 15 — one of the breaking changes
 * `frontend/AGENTS.md` warns about. Writing `reset` from memory does not upset TypeScript
 * (the extra prop is simply ignored) but it turns the "Thử lại" button into a dead button.
 * Source: `node_modules/next/dist/docs/01-app/01-getting-started/10-error-handling.md`.
 */
export default function ErrorPage({
  error,
  retry,
}: {
  error: Error & { digest?: string };
  retry: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <main className="mx-auto flex min-h-svh max-w-md flex-col items-center justify-center gap-4 p-6 text-center">
      <h1 className="text-xl font-semibold">Đã có lỗi xảy ra</h1>

      <p className="text-muted-foreground text-sm">
        Thao tác vừa rồi không hoàn tất. Bạn thử lại giúp tôi một lần nữa nhé.
      </p>

      {/* `digest` is the id Next generates for a server-side error; showing it lets us match
          the report against the logs instead of asking the user to describe the failure in
          words. The actual error message is NOT shown here — it may contain internal
          details. */}
      {error.digest && (
        <p className="text-muted-foreground font-mono text-xs">Mã lỗi: {error.digest}</p>
      )}

      <button
        type="button"
        onClick={() => retry()}
        className="border-border hover:bg-accent inline-flex min-h-11 items-center rounded-md border px-4 text-sm"
      >
        Thử lại
      </button>
    </main>
  );
}
