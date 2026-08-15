"use client";

import { useEffect, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { NHAN_LOAI_COMPONENT, type MyComponentDto } from "@/lib/api/types";

/**
 * Nội dung nhúng do giảng viên soạn vẫn đi qua bộ lọc trước khi vào DOM. Không phải vì
 * nghi ngờ giảng viên, mà vì `Html` là dữ liệu đi qua DB: bất kỳ đường ghi nào sau này
 * (import, sửa tay, endpoint soạn bài) cũng đổ vào đúng chỗ này. Lọc ở điểm render là
 * chốt chặn duy nhất không phụ thuộc vào việc nhớ lọc ở mọi đường ghi.
 */
function locHtml(raw: string): string {
  const doc = new DOMParser().parseFromString(raw, "text/html");

  doc.querySelectorAll("script, style, iframe, object, embed, link, meta").forEach((n) =>
    n.remove(),
  );

  doc.querySelectorAll("*").forEach((el) => {
    for (const attr of [...el.attributes]) {
      const ten = attr.name.toLowerCase();
      const gia = attr.value.replace(/\s/g, "").toLowerCase();
      // on* là trình xử lý sự kiện; javascript:/data: là hai lối chạy mã còn lại trong href/src.
      if (ten.startsWith("on") || gia.startsWith("javascript:") || gia.startsWith("data:")) {
        el.removeAttribute(attr.name);
      }
    }
  });

  return doc.body.innerHTML;
}

function NoiDungHtml({ html }: { html: string }) {
  // DOMParser chỉ có ở trình duyệt, nên lọc sau khi mount. Trước đó hiện rỗng thay vì
  // hiện tạm bản chưa lọc — chớp một khung hình chưa lọc cũng đủ để script chạy.
  const [sach, setSach] = useState<string | null>(null);
  useEffect(() => setSach(locHtml(html)), [html]);

  if (sach === null) return null;
  return (
    <div
      className="prose-sm [&_a]:underline [&_p]:my-1 text-sm"
      // eslint-disable-next-line react/no-danger -- đã qua locHtml ngay phía trên
      dangerouslySetInnerHTML={{ __html: sach }}
    />
  );
}

export function PhanBaiHoc({
  phan,
  dangLuu,
  onHoanThanh,
}: {
  phan: MyComponentDto;
  dangLuu: boolean;
  onHoanThanh: (componentId: string) => void;
}) {
  return (
    <li className="border-border flex flex-col gap-2 rounded-lg border p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant="outline">{NHAN_LOAI_COMPONENT[phan.type]}</Badge>
        {phan.is_required && <Badge variant="secondary">Bắt buộc</Badge>}
        {phan.completed && <Badge>✓ Đã xong</Badge>}
      </div>

      {phan.html && <NoiDungHtml html={phan.html} />}

      {phan.reflection_prompt && (
        <p className="text-sm">
          <span className="text-muted-foreground">Câu hỏi suy ngẫm: </span>
          {phan.reflection_prompt}
        </p>
      )}

      {phan.starter_code_url && (
        <a
          className="text-sm underline"
          href={phan.starter_code_url}
          target="_blank"
          rel="noreferrer"
        >
          Mã nguồn khởi đầu{phan.language ? ` (${phan.language})` : ""}
        </a>
      )}

      {(phan.inline_question_count ?? 0) > 0 && (
        <p className="text-muted-foreground text-sm">
          {phan.inline_question_count} câu hỏi trong phần này.
        </p>
      )}

      {phan.type === "assignment" && (
        <p className="text-muted-foreground text-sm">
          Phần bài tập — màn hình nộp bài thuộc Phase 3, tạm thời tự đánh dấu khi làm xong.
        </p>
      )}

      {!phan.completed && (
        <div>
          <Button
            size="sm"
            variant="outline"
            disabled={dangLuu}
            onClick={() => onHoanThanh(phan.id)}
          >
            {dangLuu ? "Đang lưu…" : "Đánh dấu đã xong"}
          </Button>
        </div>
      )}
    </li>
  );
}
