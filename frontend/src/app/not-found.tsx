import Link from "next/link";

/**
 * Trang 404 dùng chung cho toàn ứng dụng.
 *
 * Vì sao cần file này: backend cố ý trả **404 chứ không bao giờ 403** cho tài nguyên không
 * thuộc người gọi (ADR-012) — 403 xác nhận id có tồn tại, tức là rò rỉ đúng thứ vừa che.
 * Nên 404 ở đây không phải ca hiếm, mà là **đường đi chính** của mọi phép kiểm quyền sở hữu:
 * 14 chỗ trong `src/app` gọi `notFound()`. Không có file này thì tất cả đều hiện trang mặc
 * định của Next — "This page could not be found." — tiếng Anh, giữa một sản phẩm tiếng Việt.
 *
 * Chữ dùng ở đây phải là "không tìm thấy", TUYỆT ĐỐI KHÔNG phải "bạn không có quyền":
 * viết "không có quyền" là tự tay nói ra rằng tài nguyên đó có thật, đúng thứ mà việc trả 404
 * thay cho 403 sinh ra để giấu.
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
