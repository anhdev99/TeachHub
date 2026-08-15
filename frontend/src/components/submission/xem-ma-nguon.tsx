"use client";

import { useEffect, useState } from "react";

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
 * Xem nội dung một tệp bên trong bài nộp, có tô màu cú pháp.
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
}: {
  submissionId: string;
  fileId: string;
  path: string;
}) {
  const [html, setHtml] = useState<string | null>(null);
  const [loi, setLoi] = useState<string | null>(null);

  useEffect(() => {
    let huy = false;

    (async () => {
      setHtml(null);
      setLoi(null);

      try {
        const res = await fetch(
          `/api/submissions/${submissionId}/files/${fileId}/entry?path=${encodeURIComponent(path)}`,
        );

        if (!res.ok) {
          const problem = await res.json().catch(() => null);
          throw new Error(problem?.detail ?? `Lỗi ${res.status}`);
        }

        // Backend trả text/plain + Content-Disposition: attachment. fetch đọc được thân
        // bình thường; header đó chỉ chặn trình duyệt tự render tệp trong một tab.
        const text = await res.text();
        const { codeToHtml } = await import("shiki");

        // defaultColor: "light" gắn màu sáng THẲNG vào style, còn màu tối đi kèm dưới dạng
        // biến --shiki-dark. Để `false` cho cả hai thì không màu nào được áp và khối mã hiện
        // ra đen trắng — đúng thứ đã thấy khi chạy thử. Quy tắc đọc biến tối nằm ở globals.css.
        const ra = await codeToHtml(text, {
          lang: ngonNgu(path),
          themes: { light: "github-light", dark: "github-dark" },
          defaultColor: "light",
        });

        if (!huy) setHtml(ra);
      } catch (e) {
        if (!huy) setLoi(e instanceof Error ? e.message : "Không đọc được tệp.");
      }
    })();

    return () => {
      huy = true;
    };
  }, [submissionId, fileId, path]);

  if (loi) return <p className="text-destructive text-sm">{loi}</p>;
  if (html === null) return <p className="text-muted-foreground text-sm">Đang tải tệp…</p>;

  return (
    <div
      className="[&_pre]:overflow-x-auto [&_pre]:rounded-lg [&_pre]:border [&_pre]:p-3 [&_pre]:text-xs"
      // eslint-disable-next-line react/no-danger -- Shiki sinh HTML đã thoát từ chuỗi text thô
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}
