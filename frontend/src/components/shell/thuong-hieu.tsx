import Link from "next/link";

/**
 * Dấu hiệu nhận biết TeachHub. Không dùng ảnh: một hình vuông bo góc với chữ cái là đủ, và
 * nó theo màu chủ đạo nên tự đúng ở cả chế độ sáng lẫn tối — file ảnh thì phải làm hai bản
 * và sẽ có một bản bị quên.
 */
export function Thuonghieu({ href = "/", phu }: { href?: string; phu?: string }) {
  return (
    <Link href={href} className="flex items-center gap-2.5 min-w-0">
      <span
        aria-hidden
        className="bg-primary text-primary-foreground grid size-8 shrink-0 place-items-center rounded-lg text-sm font-bold"
      >
        T
      </span>
      <span className="flex min-w-0 flex-col leading-tight">
        <span className="truncate text-sm font-semibold tracking-tight">TeachHub</span>
        {phu && <span className="text-muted-foreground truncate text-xs">{phu}</span>}
      </span>
    </Link>
  );
}
