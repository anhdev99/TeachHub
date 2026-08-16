"use client"; // Ranh giới lỗi bắt buộc phải là Client Component (quy ước Next.js).

import { useEffect } from "react";

/**
 * Ranh giới lỗi dùng chung. Cùng lý do với `not-found.tsx`: thiếu file này thì mọi lỗi chưa
 * bắt được hiện trang mặc định tiếng Anh của Next.
 *
 * ⚠️ Next 16 đặt tên prop là `retry`, KHÔNG phải `reset` như bản 15 — đây là một trong những
 * thay đổi phá vỡ mà `frontend/AGENTS.md` cảnh báo. Viết `reset` theo trí nhớ thì TypeScript
 * không kêu (prop dư bị bỏ qua) nhưng nút "Thử lại" thành nút chết.
 * Nguồn: `node_modules/next/dist/docs/01-app/01-getting-started/10-error-handling.md`.
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

      {/* `digest` là mã Next sinh cho lỗi phía máy chủ; hiện nó ra để đối chiếu với log,
          thay vì bắt người dùng mô tả lại lỗi bằng lời. Nội dung lỗi thật KHÔNG hiện ở đây —
          nó có thể chứa chi tiết nội bộ. */}
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
