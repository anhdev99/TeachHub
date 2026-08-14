"use client";

import type { LessonContentType } from "@/lib/api/types";

/**
 * Chỉ cho nhúng iframe từ danh sách nguồn này. Cho nhúng tuỳ ý nghĩa là bất kỳ ai sửa được
 * nội dung bài học đều có thể nhúng trang lừa đảo vào trang học của sinh viên.
 */
const NGUON_VIDEO_CHO_PHEP = [
  "youtube.com",
  "www.youtube.com",
  "youtube-nocookie.com",
  "www.youtube-nocookie.com",
  "youtu.be",
  "player.vimeo.com",
  "vimeo.com",
];

function nguonHopLe(url: string): boolean {
  try {
    return NGUON_VIDEO_CHO_PHEP.includes(new URL(url).hostname);
  } catch {
    return false;
  }
}

export function XemNoiDung({
  contentType,
  fileUrl,
}: {
  contentType: LessonContentType;
  fileUrl: string | null;
}) {
  if (contentType === "video_embed") {
    if (!fileUrl || !nguonHopLe(fileUrl)) {
      return (
        <p className="text-muted-foreground text-sm">
          Video của bài này không đến từ nguồn được phép nhúng.
        </p>
      );
    }

    return (
      <div className="aspect-video w-full overflow-hidden rounded-lg border">
        <iframe
          src={fileUrl}
          title="Video bài giảng"
          className="size-full"
          allow="accelerometer; clipboard-write; encrypted-media; picture-in-picture"
          sandbox="allow-scripts allow-same-origin allow-presentation"
          referrerPolicy="strict-origin-when-cross-origin"
        />
      </div>
    );
  }

  if (!fileUrl) {
    return (
      <p className="text-muted-foreground text-sm">Bài này chưa có tệp nội dung.</p>
    );
  }

  // Đi qua BFF: file được phục vụ kèm kiểm quyền phía máy chủ, token không rời khỏi cookie.
  const src = `/api/files/${fileUrl.split("/").map(encodeURIComponent).join("/")}`;

  if (contentType === "pdf") {
    return (
      // <object> để trình duyệt render PDF bằng bộ xem sẵn có — xem ngay, không phải tải về,
      // và không kéo theo pdf.js cùng cấu hình worker. Nội dung bên trong là phương án dự phòng
      // cho trình duyệt không có bộ xem PDF.
      <object data={src} type="application/pdf" className="h-[70vh] w-full rounded-lg border">
        <p className="p-4 text-sm">
          Trình duyệt của bạn không xem được PDF trực tiếp.{" "}
          <a className="underline" href={src} target="_blank" rel="noreferrer">
            Mở tệp trong tab mới
          </a>
        </p>
      </object>
    );
  }

  if (contentType === "ppt") {
    // Không thư viện JS nào render PPTX đủ tốt; chuyển đổi phía server bằng LibreOffice là
    // phụ thuộc hệ thống nặng và đã được quyết là CẮT (xem báo cáo Phase 2).
    return (
      <div className="border-border flex flex-col gap-2 rounded-lg border p-4">
        <p className="text-sm">
          Bài này là tệp trình chiếu. Trình duyệt không xem trực tiếp được định dạng PPTX.
        </p>
        <a className="text-sm underline" href={src} target="_blank" rel="noreferrer">
          Tải tệp về để xem
        </a>
      </div>
    );
  }

  return (
    <a className="text-sm underline" href={src} target="_blank" rel="noreferrer">
      Mở tệp nội dung
    </a>
  );
}
