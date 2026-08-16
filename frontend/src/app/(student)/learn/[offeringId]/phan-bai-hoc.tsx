"use client";

import { useMutation } from "@tanstack/react-query";
import {
  Check,
  ClipboardList,
  Code2,
  FileText,
  ListChecks,
  MessageSquareQuote,
  RotateCcw,
  Sparkles,
  StickyNote,
} from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useId, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { apiFetch } from "@/lib/api/fetcher";
import {
  NHAN_LOAI_COMPONENT,
  type InlineQuestionDto,
  type LessonComponentType,
  type MyComponentDto,
  type TakeAttemptDto,
} from "@/lib/api/types";
import { cn } from "@/lib/utils";

/** Icon cho từng loại phần — đọc loại bằng một cái liếc thay vì phải đọc nhãn chữ. */
const ICON_LOAI: Record<LessonComponentType, React.ComponentType<{ className?: string }>> = {
  recap: RotateCcw,
  hook: Sparkles,
  checkpoint_quiz: ListChecks,
  code_along: Code2,
  assignment: FileText,
  quiz: ClipboardList,
  reflection: MessageSquareQuote,
  custom_note: StickyNote,
};

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
  // eslint-disable-next-line react-hooks/set-state-in-effect -- xem lý do ngay dưới đây
  useEffect(() => setSach(locHtml(html)), [html]);
  // `DOMParser` là API của trình duyệt nên KHÔNG chạy được lúc render phía máy chủ. Đây đúng
  // là ca mà quy tắc lint chừa ra: đồng bộ với một hệ thống bên ngoài (DOM của trình duyệt).
  // Cách "gọn" hơn — render thẳng `html` rồi lọc sau — là render một khung hình chưa lọc, mà
  // một khung hình cũng đủ để script trong đó chạy.

  if (sach === null) return null;
  return (
    <div
      className="text-sm [&_a]:underline [&_li]:my-0.5 [&_ol]:my-1 [&_ol]:list-decimal [&_ol]:pl-5 [&_p]:my-1 [&_ul]:my-1 [&_ul]:list-disc [&_ul]:pl-5"
      // Ngoại lệ DUY NHẤT trong tệp này: chuỗi đã đi qua `locHtml` ngay phía trên.
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
  const goc = useId();

  return (
    <div className="flex flex-col gap-3">
      {/* Câu này là NỘI DUNG chứ không phải trang trí: nó là thứ duy nhất ngăn sinh viên tin
          rằng mình vừa nộp một bài có tính điểm. Nền hổ phách để không bị lướt qua. */}
      <p className="bg-warn-soft text-warn border-warn/25 rounded-lg border px-3 py-2 text-xs text-pretty">
        Câu hỏi tự kiểm tra. Lựa chọn của bạn <strong>chưa được chấm và chưa được lưu</strong> —
        hãy tự đối chiếu với nội dung bài học.
      </p>

      <ol className="flex flex-col gap-4">
        {cauHoi.map((c) => (
          <li key={c.index} className="flex flex-col gap-2">
            {/* Chuỗi do giảng viên soạn đi qua text node của React nên được thoát tự động.
                Không dùng dangerouslySetInnerHTML ở đây. */}
            <p className="text-sm font-medium text-pretty">
              {c.index + 1}. {c.text}
            </p>

            {c.options.length === 0 ? (
              <p className="text-muted-foreground text-sm">Câu hỏi tự luận — hãy tự trả lời.</p>
            ) : (
              <RadioGroup
                value={chon[c.index] === undefined ? "" : String(chon[c.index])}
                onValueChange={(v) => setChon((cu) => ({ ...cu, [c.index]: Number(v) }))}
                className="gap-1.5"
              >
                {c.options.map((o, i) => {
                  const id = `${goc}-${c.index}-${i}`;
                  const daChon = chon[c.index] === i;

                  return (
                    // Cả ô là vùng bấm ≥ 44px, không phải mỗi chấm tròn 16px: trên điện thoại
                    // bấm trúng một chấm 16px là chuyện may rủi.
                    <Label
                      key={id}
                      htmlFor={id}
                      className={cn(
                        "flex min-h-11 cursor-pointer items-center gap-2.5 rounded-lg border px-3 py-2 text-sm font-normal transition-colors",
                        daChon ? "border-primary bg-accent" : "border-border hover:bg-muted",
                      )}
                    >
                      <RadioGroupItem value={String(i)} id={id} />
                      <span className="flex-1 text-pretty">{o}</span>
                    </Label>
                  );
                })}
              </RadioGroup>
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
  const Icon = ICON_LOAI[phan.type];

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
    <li
      className={cn(
        "bg-card flex flex-col gap-3 rounded-xl border p-3 sm:p-4",
        // Phần đã xong lùi về sau bằng viền xanh nhạt chứ không đổi nền: đổi nền cả khối làm
        // nội dung bên trong khó đọc, mà nội dung vẫn cần đọc lại được sau khi xong.
        phan.completed ? "border-ok/30" : "border-border",
      )}
    >
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-muted-foreground inline-flex items-center gap-1.5 text-xs font-medium">
          <Icon className="size-3.5" />
          {NHAN_LOAI_COMPONENT[phan.type]}
        </span>

        {phan.is_required && !phan.completed && (
          <Badge className="bg-warn-soft text-warn border-warn/25 border">Bắt buộc</Badge>
        )}
        {phan.completed && (
          <Badge className="bg-ok-soft text-ok border-ok/25 border">
            <Check aria-hidden />
            Đã xong
          </Badge>
        )}
      </div>

      {phan.html && <NoiDungHtml html={phan.html} />}

      {phan.reflection_prompt && (
        <div className="flex flex-col gap-1">
          <span className="text-muted-foreground text-xs font-medium">Câu hỏi suy ngẫm</span>
          <blockquote className="border-primary/40 border-l-2 pl-3 text-sm text-pretty">
            {phan.reflection_prompt}
          </blockquote>
        </div>
      )}

      {phan.starter_code_url && (
        <a
          className="hover:bg-accent border-border inline-flex min-h-11 w-fit items-center gap-2 rounded-lg border px-3 text-sm font-medium transition-colors"
          href={phan.starter_code_url}
          target="_blank"
          rel="noreferrer"
        >
          <Code2 className="size-4 shrink-0" aria-hidden />
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
          <p className="text-muted-foreground text-sm text-pretty">
            Phần này có {phan.inline_question_count} câu hỏi nhưng chưa tải được nội dung câu hỏi.
          </p>
        )}

      {/* Bài trắc nghiệm thật: `ref_id` trỏ tới bản ghi quiz. Không có ref_id thì giảng viên
          chưa gắn quiz nào vào phần này — hiện đúng lý do thay vì một nút bấm không đi tới đâu. */}
      {phan.type === "quiz" &&
        (refId ? (
          <Button
            disabled={batDauQuiz.isPending}
            onClick={() => batDauQuiz.mutate(refId)}
            className="min-h-11 w-full sm:w-fit"
          >
            {batDauQuiz.isPending ? "Đang mở…" : "Làm bài trắc nghiệm"}
          </Button>
        ) : (
          <p className="text-muted-foreground text-sm text-pretty">
            Phần trắc nghiệm này chưa được gắn với bài trắc nghiệm nào.
          </p>
        ))}

      {phan.type === "assignment" &&
        (refId ? (
          <Link
            href={`/assignments/${refId}/submit?offering=${offeringId}`}
            className="bg-primary text-primary-foreground hover:bg-primary/80 inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-lg px-4 text-sm font-medium transition-colors sm:w-fit"
          >
            <FileText className="size-4 shrink-0" aria-hidden />
            Mở bài tập để nộp
          </Link>
        ) : (
          <p className="text-muted-foreground text-sm text-pretty">
            Phần bài tập này chưa được gắn với bài tập nào.
          </p>
        ))}

      {!phan.completed && (
        <Button
          variant="outline"
          disabled={dangLuu}
          onClick={() => onHoanThanh(phan.id)}
          className="min-h-11 w-full sm:w-fit"
        >
          {dangLuu ? "Đang lưu…" : "Đánh dấu đã xong"}
        </Button>
      )}
    </li>
  );
}
