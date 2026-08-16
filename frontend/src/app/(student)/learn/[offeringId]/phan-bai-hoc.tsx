"use client";

import { useMutation } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { apiFetch } from "@/lib/api/fetcher";
import {
  NHAN_LOAI_COMPONENT,
  type InlineQuestionDto,
  type MyComponentDto,
  type TakeAttemptDto,
} from "@/lib/api/types";

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

/**
 * Trắc nghiệm nhanh nhúng ngay trong bài học.
 *
 * ⚠️ KHÔNG CHẤM Ở CLIENT, và đây không phải là chuyện lười: payload `InlineQuestionDto` chỉ có
 * `{index, text, options}` — backend CỐ Ý tước đáp án đúng đi, đúng lý do với `TakeOptionDto`
 * của Phase 5. Muốn chấm ở client thì phải có đáp án trong payload, mà gửi đáp án xuống rồi
 * ẩn trên giao diện nghĩa là cả lớp mở DevTools ra đọc. Nên phần chấm CHỜ endpoint chấm câu
 * nhúng ở máy chủ; tới lúc đó chỗ này chỉ là ô tự kiểm tra.
 *
 * Vì chưa có endpoint nên lựa chọn của sinh viên KHÔNG được gửi đi và KHÔNG được lưu. Phải nói
 * thẳng điều đó trên màn hình: hiện một bài trắc nghiệm trông như thật rồi lặng lẽ vứt câu trả
 * lời đi là để sinh viên tin mình đã nộp, và trách nhau khi điểm không có.
 *
 * KHI ENDPOINT CHẤM CÓ RỒI, hai thứ phải nối vào đây (đã đối chiếu với domain, xác nhận
 * 2026-08-15):
 *
 *  1. `InlineQuestionDto` hiện KHÔNG có cờ phân biệt chọn-một / chọn-nhiều, trong khi
 *     `Domain/Content/ComponentConfig.cs` lưu `CorrectIndexes` là DANH SÁCH — câu nhiều đáp án
 *     là có thật. Chỗ này đang render radio (chọn một). Chừng nào chưa chấm thì chưa sai, nhưng
 *     nối endpoint vào mà vẫn thiếu cờ đó là chấm sai câu nhiều đáp án một cách LẶNG LẼ: sinh
 *     viên không chọn đủ được đáp án, và không có lỗi nào chỉ ra vì sao mất điểm.
 *  2. Domain có `Explanation` cho mỗi câu nhưng DTO gửi xuống cố ý KHÔNG kèm — đúng, vì lời
 *     giải mà gửi trước thì mở DevTools là biết đáp án. Nó phải đi về trong PHẢN HỒI của lời
 *     gọi chấm rồi mới hiện. Bỏ quên bước đó là tính năng mất đúng phần dạy học của nó: sinh
 *     viên biết mình sai mà không biết vì sao.
 */
function TracNghiemNhanh({ cauHoi }: { cauHoi: InlineQuestionDto[] }) {
  // Khoá theo `index` THẬT của câu hỏi do server đánh số, không theo vị trí trong mảng.
  const [chon, setChon] = useState<Record<number, number>>({});

  return (
    <div className="flex flex-col gap-3">
      <p className="text-muted-foreground text-sm">
        Câu hỏi tự kiểm tra. Lựa chọn của bạn <strong>chưa được chấm và chưa được lưu</strong> —
        hãy tự đối chiếu với nội dung bài học.
      </p>

      <ol className="flex flex-col gap-3">
        {cauHoi.map((c) => (
          <li key={c.index} className="flex flex-col gap-1.5">
            {/* Chuỗi do giảng viên soạn đi qua text node của React nên được thoát tự động.
                Không dùng dangerouslySetInnerHTML ở đây. */}
            <p className="text-sm font-medium">
              {c.index + 1}. {c.text}
            </p>

            {c.options.length === 0 ? (
              <p className="text-muted-foreground text-sm">Câu hỏi tự luận — hãy tự trả lời.</p>
            ) : (
              <div className="flex flex-col gap-1">
                {c.options.map((o, i) => (
                  // Vùng bấm ≥ 44px cho ngón tay (SRS mục 9: SV hay xem trên điện thoại).
                  <label
                    key={`${c.index}-${o}`}
                    className="flex min-h-11 cursor-pointer items-center gap-2 text-sm"
                  >
                    <input
                      type="radio"
                      // Radio nhóm theo câu hỏi; thiếu `name` riêng thì cả bài chỉ chọn được
                      // đúng một phương án cho tất cả các câu.
                      name={`cau-${c.index}`}
                      checked={chon[c.index] === i}
                      onChange={() => setChon((cu) => ({ ...cu, [c.index]: i }))}
                    />
                    {o}
                  </label>
                ))}
              </div>
            )}
          </li>
        ))}
      </ol>
    </div>
  );
}

export function PhanBaiHoc({
  phan,
  offeringId,
  dangLuu,
  onHoanThanh,
}: {
  phan: MyComponentDto;
  offeringId: string;
  dangLuu: boolean;
  onHoanThanh: (componentId: string) => void;
}) {
  const router = useRouter();

  // Rút ra biến cục bộ để TypeScript thu hẹp được kiểu bên trong closure của onClick —
  // `phan.ref_id` là thuộc tính nên phép thu hẹp không sống sót qua ranh giới hàm, và cách
  // duy nhất còn lại là dấu `!`, tức là tự tắt đúng cái kiểm tra mình cần.
  const refId = phan.ref_id;

  const batDauQuiz = useMutation({
    mutationFn: (quizId: string) =>
      apiFetch<TakeAttemptDto>(`/api/quizzes/${quizId}/attempts`, { method: "POST" }),
    onSuccess: (luot) => router.push(`/quizzes/${luot.id}?offering=${offeringId}`),
    // Hết lượt làm, quiz chưa có câu hỏi, ngoài thời gian mở… backend trả ProblemDetails kèm
    // câu tiếng Việt đọc được. Hiện nguyên câu đó thay vì "Lỗi 400".
    onError: (e: Error) => toast.error(e.message),
  });

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

      {/* Trắc nghiệm nhanh: câu hỏi nhúng ngay trong bài, không rời khỏi trang học. */}
      {phan.type === "checkpoint_quiz" && phan.inline_questions.length > 0 && (
        <TracNghiemNhanh cauHoi={phan.inline_questions} />
      )}

      {/* Đếm được câu hỏi nhưng không tải được nội dung là ca thật (dữ liệu cũ soạn trước khi
          có inline_questions). Nói rõ hơn là im lặng hiện một phần trống. */}
      {phan.type === "checkpoint_quiz" &&
        phan.inline_questions.length === 0 &&
        phan.inline_question_count > 0 && (
          <p className="text-muted-foreground text-sm">
            Phần này có {phan.inline_question_count} câu hỏi nhưng chưa tải được nội dung câu hỏi.
          </p>
        )}

      {/* Bài trắc nghiệm thật: `ref_id` trỏ tới bản ghi quiz. Không có ref_id thì giảng viên
          chưa gắn quiz nào vào phần này — hiện đúng lý do thay vì một nút bấm không đi tới đâu. */}
      {phan.type === "quiz" &&
        (refId ? (
          <div>
            <Button
              size="sm"
              disabled={batDauQuiz.isPending}
              onClick={() => batDauQuiz.mutate(refId)}
            >
              {batDauQuiz.isPending ? "Đang mở…" : "Làm bài trắc nghiệm"}
            </Button>
          </div>
        ) : (
          <p className="text-muted-foreground text-sm">
            Phần trắc nghiệm này chưa được gắn với bài trắc nghiệm nào.
          </p>
        ))}

      {phan.type === "assignment" &&
        (refId ? (
          <Link
            className="text-sm underline"
            href={`/assignments/${refId}/submit?offering=${offeringId}`}
          >
            Mở bài tập để nộp
          </Link>
        ) : (
          <p className="text-muted-foreground text-sm">
            Phần bài tập này chưa được gắn với bài tập nào.
          </p>
        ))}

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
