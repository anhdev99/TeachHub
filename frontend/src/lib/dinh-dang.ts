/**
 * Định dạng ngày giờ cho người Việt đọc.
 *
 * Bản cũ đổ thẳng `new Date(x).toLocaleString()` ra màn hình, cho chuỗi kiểu
 * `11:38:48 16/8/2026`: có giây (không ai cần), không có số 0 đứng đầu, và thứ tự giờ-trước-
 * ngày đọc ngược với thói quen. Trong một danh sách, những chuỗi đó dài ngắn khác nhau nên
 * cột thời gian răng cưa — chi tiết nhỏ nhưng lặp lại ở mọi màn hình.
 */

const NGAY_GIO = new Intl.DateTimeFormat("vi-VN", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hour12: false,
});

const NGAY = new Intl.DateTimeFormat("vi-VN", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
});

/** `16/08/2026 11:38` */
export function ngayGio(iso?: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  // Intl trả "16/08/2026, 11:38" — bỏ dấu phẩy cho gọn trong ô bảng.
  return NGAY_GIO.format(d).replace(",", "");
}

/** `16/08/2026` */
export function ngay(iso?: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "—" : NGAY.format(d);
}

/**
 * Khoảng cách tương đối: "2 giờ trước", "trong 3 ngày".
 *
 * Dùng cho những chỗ mà câu hỏi thật là "gần đây không / còn kịp không" chứ không phải "chính
 * xác lúc nào" — hàng chờ chấm, hạn nộp. Luôn đi kèm mốc tuyệt đối ở thuộc tính `title` để
 * người cần con số chính xác vẫn lấy được; chỉ hiện tương đối là mất thông tin.
 *
 * `Intl.RelativeTimeFormat` chứ không tự viết bảng chữ: tự viết thì thiếu ca số nhiều, ca
 * "hôm qua", và sẽ sai ở đúng những mốc hiếm gặp mà không ai thử.
 */
const TUONG_DOI = new Intl.RelativeTimeFormat("vi-VN", { numeric: "auto" });

const BAC: [Intl.RelativeTimeFormatUnit, number][] = [
  ["year", 1000 * 60 * 60 * 24 * 365],
  ["month", 1000 * 60 * 60 * 24 * 30],
  ["day", 1000 * 60 * 60 * 24],
  ["hour", 1000 * 60 * 60],
  ["minute", 1000 * 60],
];

export function khoangCach(iso?: string | null, moc: Date = new Date()): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";

  const lech = d.getTime() - moc.getTime();

  for (const [donVi, ms] of BAC) {
    if (Math.abs(lech) >= ms) {
      // Làm tròn về 0 (`trunc`) chứ không `round`: 1,9 giờ trước phải là "1 giờ trước",
      // không phải "2 giờ trước" — nói quá lên là sai theo hướng khó chịu nhất.
      return TUONG_DOI.format(Math.trunc(lech / ms), donVi);
    }
  }
  return "vừa xong";
}

/** Điểm số: bỏ số 0 thừa sau dấu phẩy, giữ dấu phẩy thập phân kiểu Việt. */
export function diem(x?: number | null): string {
  if (x === null || x === undefined) return "—";
  return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(x);
}
