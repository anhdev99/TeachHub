import { ChevronLeft } from "lucide-react";
import Link from "next/link";
import type { ReactNode } from "react";

/**
 * Khung một trang nội dung.
 *
 * Tồn tại để mọi màn hình có cùng bề rộng, cùng lề, cùng khoảng cách dọc. Trước đây mỗi trang
 * tự khai `max-w-3xl` / `max-w-4xl` / `max-w-5xl` tuỳ người viết, nên chuyển giữa hai màn
 * hình là nội dung nhảy ngang một đoạn — cảm giác lỏng lẻo đó không nằm ở màu sắc.
 *
 * `rong` cho những trang thật sự cần bề ngang (sổ điểm, so sánh bài nộp).
 */
export function Trang({
  children,
  rong = false,
}: {
  children: ReactNode;
  rong?: boolean;
}) {
  return (
    <main
      className={`mx-auto flex w-full flex-col gap-6 p-4 sm:p-6 lg:p-8 ${
        rong ? "max-w-7xl" : "max-w-5xl"
      }`}
    >
      {children}
    </main>
  );
}

/**
 * Đầu trang: đường quay lại, tiêu đề, mô tả, và vùng hành động bên phải.
 *
 * Vùng hành động nằm cùng hàng với tiêu đề ở màn rộng nhưng **xuống dòng** ở màn hẹp
 * (`flex-wrap`) — nhét nút vào cùng hàng trên điện thoại thì tiêu đề bị ép còn hai chữ.
 */
export function DauTrang({
  tieuDe,
  moTa,
  quayLai,
  hanhDong,
}: {
  tieuDe: ReactNode;
  moTa?: ReactNode;
  quayLai?: { href: string; nhan: string };
  hanhDong?: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-3">
      {/* `py-3 -my-3` chứ không chỉ `min-h-11`: vùng bấm phải đạt 44px cho ngón tay, nhưng
          nếu để nó chiếm 44px thật thì đầu mỗi trang phình thêm hơn một dòng. Phần đệm nới
          vùng chạm ra, phần lề âm kéo bố cục về chỗ cũ — nhìn vẫn là một dòng chữ nhỏ.
          Đây là đường quay lại CHÍNH trên điện thoại, bấm trượt là phải cuộn lên tìm lại. */}
      {quayLai && (
        <Link
          href={quayLai.href}
          className="text-muted-foreground hover:text-foreground -my-3 -ml-1 inline-flex min-h-11 w-fit items-center gap-1 py-3 text-sm transition-colors"
        >
          <ChevronLeft className="size-4" aria-hidden />
          {quayLai.nhan}
        </Link>
      )}

      <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-3">
        <div className="flex min-w-0 flex-col gap-1">
          <h1 className="text-2xl font-semibold tracking-tight text-balance">{tieuDe}</h1>
          {moTa && <p className="text-muted-foreground text-sm text-pretty">{moTa}</p>}
        </div>
        {hanhDong && <div className="flex shrink-0 flex-wrap gap-2">{hanhDong}</div>}
      </div>
    </div>
  );
}

/**
 * Trạng thái rỗng.
 *
 * Bản cũ để trắng cả một màn hình khi không có dữ liệu, nên "chưa có gì" và "hỏng rồi" trông
 * y hệt nhau — người dùng không biết nên chờ, nên tạo mới, hay nên báo lỗi. Luôn nói RÕ đang
 * thiếu gì và đưa sẵn bước kế tiếp nếu có.
 */
export function TrongRong({
  Icon,
  tieuDe,
  moTa,
  hanhDong,
}: {
  Icon?: React.ComponentType<{ className?: string }>;
  tieuDe: string;
  moTa?: string;
  hanhDong?: ReactNode;
}) {
  return (
    <div className="border-border flex flex-col items-center gap-3 rounded-xl border border-dashed px-6 py-12 text-center">
      {Icon && (
        <span className="bg-muted text-muted-foreground grid size-11 place-items-center rounded-full">
          <Icon className="size-5" />
        </span>
      )}
      <div className="flex flex-col gap-1">
        <p className="font-medium">{tieuDe}</p>
        {moTa && <p className="text-muted-foreground max-w-sm text-sm text-pretty">{moTa}</p>}
      </div>
      {hanhDong}
    </div>
  );
}

/**
 * Khối cuộn ngang cho nội dung rộng (sổ điểm, danh sách nộp bài).
 *
 * Quy tắc §9 của bản thiết kế: **khối này** cuộn, không phải cả trang. Bọc bảng bằng một
 * `div` thường là cả trang cuộn ngang trên điện thoại, và khi đó không còn đọc được gì.
 */
export function CuonNgang({ children }: { children: ReactNode }) {
  return <div className="-mx-1 overflow-x-auto px-1">{children}</div>;
}
