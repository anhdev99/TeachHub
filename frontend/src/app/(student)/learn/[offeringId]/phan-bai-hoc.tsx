"use client";

import { useMutation } from "@tanstack/react-query";
import {
  Check,
  CircleCheck,
  CircleX,
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
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { apiFetch } from "@/lib/api/fetcher";
import {
  NHAN_LOAI_COMPONENT,
  type CheckInlineQuestionsResultDto,
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
      // Ngoại lệ DUY NHẤT trong tệp này. `locHtml` ngay phía trên đã gỡ script/iframe/object
      // và tước mọi thuộc tính on* cùng mọi javascript:/data:.
      //
      // Chỉ thị dưới đây phải nằm SÁT dòng nó bảo vệ — `eslint-disable-next-line` chỉ áp cho
      // đúng một dòng kế tiếp, chèn thêm chú thích vào giữa là nó lặng lẽ mất tác dụng.
      // eslint-disable-next-line react/no-danger -- đã lọc qua locHtml, xem giải thích trên
      dangerouslySetInnerHTML={{ __html: sach }}
    />
  );
}

/**
 * Trắc nghiệm nhanh nhúng ngay trong bài học.
 *
 * ⚠️ CHẤM Ở MÁY CHỦ, không bao giờ ở client. `InlineQuestionDto` chỉ mang
 * `{index, text, options, multi_select}` — backend CỐ Ý không gửi `correct_indexes` xuống,
 * cùng lý do với `TakeOptionDto` của Phase 5. Đáp án đúng và lời giải chỉ xuất hiện trong
 * PHẢN HỒI của lời gọi chấm, tức là sau khi sinh viên đã trả lời. Gửi trước rồi ẩn trên giao
 * diện thì cả lớp mở DevTools ra đọc.
 *
 * `multi_select` do máy chủ suy từ số đáp án đúng, và đường chấm dùng CHÍNH giá trị đó — nên
 * ô chọn hiện ra ở đây với luật chấm ở kia không thể lệch nhau. Thiếu cờ này thì câu nhiều
 * đáp án bị render thành radio: sinh viên không chọn đủ được đáp án, bị chấm sai, và không có
 * gì chỉ ra vì sao.
 *
 * Đây là câu hỏi TỰ KIỂM TRA, không vào sổ điểm — nhưng trả lời đúng hết thì máy chủ đánh dấu
 * phần học hoàn thành, nên nó vẫn đẩy được tiến độ mở khoá bài sau.
 */
function TracNghiemNhanh({
  componentId,
  cauHoi,
  onHoanThanh,
}: {
  componentId: string;
  cauHoi: InlineQuestionDto[];
  onHoanThanh: () => void;
}) {
  // Khoá theo `index` THẬT của câu hỏi do server đánh số, không theo vị trí trong mảng.
  // Giá trị là MẢNG kể cả với câu chọn-một: một hình dạng duy nhất cho cả hai loại, thay vì
  // hai kiểu dữ liệu phải nhớ chuyển qua lại ở mỗi chỗ dùng.
  const [chon, setChon] = useState<Record<number, number[]>>({});
  const goc = useId();

  const cham = useMutation({
    mutationFn: () =>
      apiFetch<CheckInlineQuestionsResultDto>(`/api/lesson-components/${componentId}/check`, {
        method: "POST",
        body: JSON.stringify({
          answers: cauHoi.map((c) => ({
            index: c.index,
            selected_indexes: chon[c.index] ?? [],
          })),
        }),
      }),
    onSuccess: (kq) => {
      // Trả lời đúng hết thì máy chủ tự đánh dấu phần học xong. Báo lên trang cha để thanh
      // tiến độ và trạng thái khoá của bài sau cập nhật theo, thay vì phải tải lại trang.
      if (kq.completed) onHoanThanh();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const kq = cham.data;
  // Tra kết quả theo `index` thật, không theo vị trí — máy chủ không hứa trả về đúng thứ tự.
  const theoIndex = new Map((kq?.results ?? []).map((r) => [r.index, r]));

  function doiChon(index: number, i: number, multi: boolean) {
    setChon((cu) => {
      const dang = cu[index] ?? [];
      if (!multi) return { ...cu, [index]: [i] };
      return {
        ...cu,
        [index]: dang.includes(i) ? dang.filter((x) => x !== i) : [...dang, i],
      };
    });
  }

  const chuaTraLoi = cauHoi.filter((c) => c.options.length > 0 && !(chon[c.index]?.length > 0));

  return (
    <div className="flex flex-col gap-3">
      {kq ? (
        <p
          className={cn(
            "rounded-lg border px-3 py-2 text-sm text-pretty",
            kq.all_correct
              ? "bg-ok-soft text-ok border-ok/25"
              : "bg-warn-soft text-warn border-warn/25",
          )}
        >
          Đúng {kq.correct_count}/{kq.total} câu.{" "}
          {kq.all_correct
            ? "Phần này đã được đánh dấu hoàn thành."
            : "Xem lời giải bên dưới rồi thử lại nhé."}
        </p>
      ) : (
        <p className="text-muted-foreground text-sm text-pretty">
          Câu hỏi tự kiểm tra — không tính vào điểm. Trả lời đúng hết thì phần này được đánh
          dấu hoàn thành.
        </p>
      )}

      <ol className="flex flex-col gap-4">
        {cauHoi.map((c) => {
          const r = theoIndex.get(c.index);
          const dapAnDung = r?.correct_indexes ?? [];

          return (
            <li key={c.index} className="flex flex-col gap-2">
              {/* Chuỗi do giảng viên soạn đi qua text node của React nên được thoát tự động.
                  Không dùng dangerouslySetInnerHTML ở đây. */}
              <p className="flex items-start gap-2 text-sm font-medium text-pretty">
                <span className="flex-1">
                  {c.index + 1}. {c.text}
                </span>
                {r &&
                  (r.is_correct ? (
                    <CircleCheck className="text-ok mt-0.5 size-4 shrink-0" aria-label="Đúng" />
                  ) : (
                    <CircleX className="text-danger mt-0.5 size-4 shrink-0" aria-label="Sai" />
                  ))}
              </p>

              {c.multi_select && !kq && (
                <p className="text-muted-foreground text-xs">Chọn tất cả đáp án đúng.</p>
              )}

              {c.options.length === 0 ? (
                <p className="text-muted-foreground text-sm">Câu hỏi tự luận — hãy tự trả lời.</p>
              ) : (
                <div className="flex flex-col gap-1.5">
                  {c.options.map((o, i) => {
                    const id = `${goc}-${c.index}-${i}`;
                    const daChon = (chon[c.index] ?? []).includes(i);
                    const laDapAn = dapAnDung.includes(i);

                    return (
                      // Cả ô là vùng bấm ≥ 44px, không phải mỗi chấm tròn 16px: trên điện
                      // thoại bấm trúng một chấm 16px là chuyện may rủi.
                      <Label
                        key={id}
                        htmlFor={id}
                        className={cn(
                          "flex min-h-11 items-center gap-2.5 rounded-lg border px-3 py-2 text-sm font-normal transition-colors",
                          kq ? "cursor-default" : "cursor-pointer",
                          // Sau khi chấm, đáp án đúng luôn được tô — kể cả khi sinh viên
                          // không chọn nó. Chỉ tô ô đã chọn thì người sai vẫn không biết
                          // đáp án đúng là ô nào.
                          laDapAn
                            ? "bg-ok-soft border-ok/30"
                            : kq && daChon
                              ? "bg-danger-soft border-danger/30"
                              : daChon
                                ? "border-primary bg-accent"
                                : "border-border",
                          kq ? "" : "hover:bg-muted",
                        )}
                      >
                        <Checkbox
                          id={id}
                          checked={daChon}
                          disabled={!!kq || cham.isPending}
                          // `rounded-full` cho câu chọn-một: hình dạng ô là thứ đầu tiên
                          // người ta đọc để biết được chọn mấy đáp án.
                          className={c.multi_select ? "" : "rounded-full"}
                          onCheckedChange={() => doiChon(c.index, i, c.multi_select)}
                        />
                        <span className="flex-1 text-pretty">{o}</span>
                      </Label>
                    );
                  })}
                </div>
              )}

              {/* Lời giải chỉ có sau khi chấm — đây là phần dạy học thật của tính năng này.
                  Biết mình sai mà không biết vì sao thì bài tự kiểm tra chẳng dạy được gì. */}
              {r?.explanation && (
                <p className="text-muted-foreground border-border border-l-2 pl-3 text-sm text-pretty">
                  {r.explanation}
                </p>
              )}
            </li>
          );
        })}
      </ol>

      <div className="flex flex-wrap items-center gap-2">
        {kq ? (
          <Button
            size="sm"
            variant="outline"
            onClick={() => {
              cham.reset();
              setChon({});
            }}
          >
            Làm lại
          </Button>
        ) : (
          <Button size="sm" disabled={cham.isPending} onClick={() => cham.mutate()}>
            {cham.isPending ? "Đang chấm…" : "Kiểm tra"}
          </Button>
        )}

        {/* Nói trước còn bao nhiêu câu bỏ trống, thay vì để bấm xong mới biết. Không CHẶN
            bấm: đây là bài tự kiểm tra, bỏ trống vài câu vẫn là một lựa chọn hợp lệ. */}
        {!kq && chuaTraLoi.length > 0 && (
          <span className="text-muted-foreground text-xs">
            Còn {chuaTraLoi.length} câu chưa chọn.
          </span>
        )}
      </div>
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
        <TracNghiemNhanh
          componentId={phan.id}
          cauHoi={phan.inline_questions}
          // Đúng hết thì máy chủ đã đánh dấu hoàn thành. Gọi lại `onHoanThanh` để trang cha
          // nạp lại tiến độ — lệnh này idempotent nên đánh dấu lần nữa không sao, và nếu
          // không gọi thì thanh tiến độ với trạng thái khoá của bài sau vẫn hiện số cũ.
          onHoanThanh={() => onHoanThanh(phan.id)}
        />
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
