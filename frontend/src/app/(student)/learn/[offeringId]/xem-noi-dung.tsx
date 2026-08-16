"use client";

import { ExternalLink, FileQuestionMark, Presentation } from "lucide-react";
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
    const u = new URL(url);
    // So khớp CHÍNH XÁC hostname (không phải `includes`): `youtube.com.evil.tld` khớp ngay
    // với phép so chuỗi con. Và bắt buộc https — dữ liệu cũ có thể được nhập trước khi luật
    // này tồn tại, còn `javascript:` gắn vào src thì chạy ngay khi render.
    return u.protocol === "https:" && NGUON_VIDEO_CHO_PHEP.includes(u.hostname);
  } catch {
    return false;
  }
}

/** Khung thông báo gọn một dòng — KHÔNG để lại một khoảng trống mênh mông trong trang học. */
function ChuThich({
  Icon,
  children,
}: {
  Icon: React.ComponentType<{ className?: string }>;
  children: React.ReactNode;
}) {
  return (
    <p className="text-muted-foreground border-border flex items-start gap-2 rounded-lg border border-dashed px-3 py-2.5 text-sm text-pretty">
      <Icon className="mt-0.5 size-4 shrink-0" />
      <span>{children}</span>
    </p>
  );
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
        <ChuThich Icon={FileQuestionMark}>
          Video của bài này không đến từ nguồn được phép nhúng.
        </ChuThich>
      );
    }

    return (
      <div className="border-border aspect-video w-full overflow-hidden rounded-xl border">
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
    return <ChuThich Icon={FileQuestionMark}>Bài này chưa có tệp nội dung.</ChuThich>;
  }

  // Đi qua BFF: file được phục vụ kèm kiểm quyền phía máy chủ, token không rời khỏi cookie.
  const src = `/api/files/${fileUrl.split("/").map(encodeURIComponent).join("/")}`;

  if (contentType === "pdf") {
    return (
      <div className="flex flex-col gap-2">
        {/* <object> để trình duyệt render PDF bằng bộ xem sẵn có — xem ngay, không phải tải về,
            và không kéo theo pdf.js cùng cấu hình worker. Nội dung bên trong là phương án dự
            phòng cho trình duyệt không có bộ xem PDF. */}
        <object
          data={src}
          type="application/pdf"
          className="border-border h-[60vh] min-h-72 w-full rounded-xl border"
        >
          <p className="p-4 text-sm">Trình duyệt của bạn không xem được PDF trực tiếp.</p>
        </object>

        {/* Link mở tab mới LUÔN hiện, không chỉ khi <object> thất bại: trình duyệt trên điện
            thoại thường bỏ qua <object> mà không báo gì, và khi đó khung trên chỉ là một ô
            trống. Đây là đường thoát duy nhất chạy được ở mọi nơi. */}
        <a
          className="text-muted-foreground hover:text-foreground inline-flex min-h-11 w-fit items-center gap-1.5 text-sm underline transition-colors"
          href={src}
          target="_blank"
          rel="noreferrer"
        >
          <ExternalLink className="size-4 shrink-0" aria-hidden />
          Mở tệp PDF trong tab mới
        </a>
      </div>
    );
  }

  if (contentType === "ppt") {
    // Không thư viện JS nào render PPTX đủ tốt; chuyển đổi phía server bằng LibreOffice là
    // phụ thuộc hệ thống nặng và đã được quyết là CẮT (xem báo cáo Phase 2).
    return (
      <div className="border-border flex flex-col gap-3 rounded-xl border p-4">
        <p className="flex items-start gap-2 text-sm text-pretty">
          <Presentation className="text-muted-foreground mt-0.5 size-4 shrink-0" aria-hidden />
          Bài này là tệp trình chiếu. Trình duyệt không xem trực tiếp được định dạng PPTX.
        </p>
        <a
          className="bg-primary text-primary-foreground hover:bg-primary/80 inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-lg px-4 text-sm font-medium transition-colors sm:w-fit"
          href={src}
          target="_blank"
          rel="noreferrer"
        >
          Tải tệp về để xem
        </a>
      </div>
    );
  }

  return (
    <a
      className="hover:bg-accent border-border inline-flex min-h-11 w-fit items-center gap-2 rounded-lg border px-3 text-sm font-medium transition-colors"
      href={src}
      target="_blank"
      rel="noreferrer"
    >
      <ExternalLink className="size-4 shrink-0" aria-hidden />
      Mở tệp nội dung
    </a>
  );
}
