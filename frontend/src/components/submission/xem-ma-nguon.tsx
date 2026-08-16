"use client";

import { useEffect, useState } from "react";

/**
 * Một đoạn cần tô nền trong mã nguồn, tính bằng OFFSET KÝ TỰ trên mã gốc.
 *
 * Trùng khớp một-một với `MatchedRangeDto` do API so sánh bài nộp trả về. Không khai lại
 * kiểu đó ở đây làm gì — nơi dùng truyền thẳng `MatchedRangeDto[]` vào và TypeScript nhận,
 * vì hai trường `position`/`length` khớp cấu trúc. Khai riêng để component này không phải
 * kéo theo cả schema API chỉ vì hai con số.
 */
export type VungToSang = { position: number; length: number };

/**
 * Suy ngôn ngữ từ phần mở rộng. Không đoán được thì trả "text" — Shiki ném lỗi với ngôn ngữ
 * lạ, và một tệp .cfg trong bài nộp không được phép làm hỏng cả trang chấm.
 */
function ngonNgu(duongDan: string): string {
  const ext = duongDan.split(".").pop()?.toLowerCase() ?? "";
  const bang: Record<string, string> = {
    ts: "typescript", tsx: "tsx", js: "javascript", jsx: "jsx",
    json: "json", css: "css", scss: "scss", html: "html",
    md: "markdown", py: "python", java: "java", cs: "csharp",
    sql: "sql", sh: "bash", yml: "yaml", yaml: "yaml", xml: "xml",
    kt: "kotlin", swift: "swift", go: "go", rs: "rust", php: "php", c: "c", cpp: "cpp",
  };
  return bang[ext] ?? "text";
}

/**
 * Gộp các đoạn chồng lấn và kẹp về trong [0, doDai].
 *
 * BẮT BUỘC phải gộp: dấu vân tay Winnowing là các k-gram TRƯỢT nên hai đoạn liền kề gần như
 * luôn phủ lên nhau. Shiki NÉM LỖI khi hai decoration giao nhau, nên bỏ bước này là cả khối
 * mã biến mất và thay bằng thông báo lỗi — đúng lúc giảng viên cần đọc nó nhất.
 *
 * Kẹp biên cũng không thừa: offset được tính lúc băm bài nộp, còn nội dung hiển thị là thứ
 * đọc lại từ kho lúc này. Lệch một byte (mã hoá, ký tự cuối tệp) mà không kẹp thì Shiki cũng
 * ném lỗi vì decoration vượt ngoài chuỗi.
 */
function gopVung(vung: readonly VungToSang[], doDai: number) {
  const doan = vung
    .map((v) => ({ start: Math.max(0, v.position), end: Math.min(doDai, v.position + v.length) }))
    .filter((d) => d.end > d.start)
    .sort((a, b) => a.start - b.start);

  const ra: { start: number; end: number }[] = [];
  for (const d of doan) {
    const cuoi = ra.at(-1);
    if (cuoi && d.start <= cuoi.end) cuoi.end = Math.max(cuoi.end, d.end);
    else ra.push({ ...d });
  }
  return ra;
}

/**
 * Xem nội dung một tệp bên trong bài nộp, có tô màu cú pháp và (tuỳ chọn) tô nền các đoạn
 * trùng với một bài nộp khác.
 *
 * Shiki nhận vào CHUỖI TEXT và sinh ra HTML đã thoát ký tự — nó không bao giờ thực thi nội
 * dung đầu vào. Đây là ngoại lệ DUY NHẤT được phép dùng dangerouslySetInnerHTML trong dự án
 * này: mọi cách render nội dung sinh viên nộp khác đều là lỗ XSS chạy trong phiên của giảng
 * viên — tức phiên có quyền sửa điểm cả lớp.
 *
 * Import động để bộ tô màu (nặng) không nằm trong bundle của trang nộp bài, nơi phần lớn
 * sinh viên không bao giờ mở tệp ra xem.
 */
export function XemMaNguon({
  submissionId,
  fileId,
  path,
  tenTep,
  vungToSang,
}: {
  submissionId: string;
  fileId: string;
  /** Đường dẫn bên trong tệp nén. Bỏ trống khi sinh viên nộp thẳng một tệp mã nguồn. */
  path?: string | null;
  /** Tên tệp, chỉ dùng để đoán ngôn ngữ khi không có `path`. */
  tenTep?: string;
  vungToSang?: readonly VungToSang[];
}) {
  const [html, setHtml] = useState<string | null>(null);
  const [loi, setLoi] = useState<string | null>(null);

  // Mảng là tham chiếu mới sau mỗi lần render của component cha, nên KHÔNG đưa thẳng
  // `vungToSang` vào deps: effect sẽ chạy lại vô hạn và tải lại tệp không ngừng. So bằng
  // nội dung đã tuần tự hoá mới là thứ thực sự quyết định kết quả hiển thị.
  const khoaVung = JSON.stringify(vungToSang ?? []);

  useEffect(() => {
    let huy = false;

    (async () => {
      setHtml(null);
      setLoi(null);

      try {
        const q = path ? `?path=${encodeURIComponent(path)}` : "";
        const res = await fetch(`/api/submissions/${submissionId}/files/${fileId}/entry${q}`);

        if (!res.ok) {
          const problem = await res.json().catch(() => null);
          // 404 = không tìm thấy HOẶC không thuộc về mình (backend cố ý không trả 403).
          // Vì thế không bao giờ viết "bạn không có quyền" ở đây.
          throw new Error(problem?.detail ?? `Lỗi ${res.status}`);
        }

        // Backend trả text/plain + Content-Disposition: attachment. fetch đọc được thân
        // bình thường; header đó chỉ chặn trình duyệt tự render tệp trong một tab.
        const text = await res.text();
        const { codeToHtml } = await import("shiki");

        const doan = gopVung(
          (JSON.parse(khoaVung) as VungToSang[]),
          text.length,
        );

        // defaultColor: "light" gắn màu sáng THẲNG vào style, còn màu tối đi kèm dưới dạng
        // biến --shiki-dark. Để `false` cho cả hai thì không màu nào được áp và khối mã hiện
        // ra đen trắng — đúng thứ đã thấy khi chạy thử. Quy tắc đọc biến tối nằm ở globals.css.
        const ra = await codeToHtml(text, {
          lang: ngonNgu(path ?? tenTep ?? ""),
          themes: { light: "github-light", dark: "github-dark" },
          defaultColor: "light",
          // Shiki tự cắt decoration theo từng dòng và tự thoát ký tự — đoạn tô sáng vẫn chỉ
          // là text, không mở thêm đường nào cho nội dung sinh viên vào DOM dưới dạng HTML.
          decorations: doan.map((d) => ({
            start: d.start,
            end: d.end,
            properties: { class: "th-trung" },
          })),
        });

        if (!huy) setHtml(ra);
      } catch (e) {
        if (!huy) setLoi(e instanceof Error ? e.message : "Không đọc được tệp.");
      }
    })();

    return () => {
      huy = true;
    };
  }, [submissionId, fileId, path, tenTep, khoaVung]);

  // Câu chữ lấy nguyên từ `ProblemDetails.detail` của backend — nó đã được viết bằng tiếng
  // Việt cho người đọc. Bọc trong khung `danger` để phân biệt với chính nội dung tệp.
  if (loi)
    return (
      <p className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3 py-2 text-sm text-pretty">
        {loi}
      </p>
    );

  if (html === null)
    return (
      <p className="border-border text-muted-foreground rounded-lg border border-dashed px-3 py-2 text-sm">
        Đang tải tệp…
      </p>
    );

  return (
    <div
      className="[&_pre]:overflow-x-auto [&_pre]:rounded-lg [&_pre]:border [&_pre]:p-3 [&_pre]:text-xs"
      // eslint-disable-next-line react/no-danger -- Shiki sinh HTML đã thoát từ chuỗi text thô
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}
