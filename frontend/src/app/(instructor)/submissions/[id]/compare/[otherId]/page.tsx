import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { components } from "@/lib/api/schema";
import { SoSanhBaiNop } from "./so-sanh-bai-nop";

type SubmissionComparisonDto = components["schemas"]["SubmissionComparisonDto"];
type ProblemDetails = components["schemas"]["ProblemDetails"];

/**
 * Màn hình so sánh cạnh nhau hai bài nộp (Phase 6).
 *
 * Tên sinh viên đi qua query string vì API so sánh chỉ trả về id — trang báo cáo trùng lặp
 * đã có sẵn tên nên chuyển tiếp rẻ hơn hai lượt gọi nữa. Tên là chuỗi do người khác nhập,
 * nhưng nó chỉ đi vào text node của React nên được thoát tự động; không có đường nào để nó
 * thành HTML.
 */
export default async function ComparePage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string; otherId: string }>;
  searchParams: Promise<{ offering?: string; assignment?: string; a?: string; b?: string }>;
}) {
  const { id, otherId } = await params;
  const { offering, assignment, a, b } = await searchParams;

  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/submissions/${id}/compare/${otherId}`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  // 404 nghĩa là KHÔNG TÌM THẤY HOẶC KHÔNG THUỘC VỀ MÌNH — backend cố ý không trả 403 để
  // không xác nhận id có tồn tại. Viết "bạn không có quyền" ở đây là tự tay tiết lộ đúng thứ
  // backend vừa giấu.
  if (res.status === 404) notFound();

  // 400 là ca thật, không phải lỗi lập trình: hai bài nộp không cùng một bài tập thì không so
  // được. Hiện nguyên câu tiếng Việt backend trả về thay vì đẩy người dùng về trang đăng nhập.
  if (res.status === 400) {
    const problem: ProblemDetails = await res.json().catch(() => ({}) as ProblemDetails);
    return (
      <main className="mx-auto flex min-h-svh max-w-5xl flex-col gap-4 p-4 sm:p-6">
        <h1 className="text-2xl font-semibold">Không so sánh được</h1>
        <p className="text-destructive text-sm">
          {problem.detail ?? "Hai bài nộp này không so sánh được."}
        </p>
      </main>
    );
  }

  if (!res.ok) redirect("/login");

  const bc: SubmissionComparisonDto = await res.json();

  const duongVe =
    assignment && offering
      ? `/assignments/${assignment}/similarity?offering=${offering}`
      : offering
        ? `/offerings/${offering}/grading`
        : "/dashboard";

  return (
    <main className="mx-auto flex min-h-svh max-w-5xl flex-col gap-6 p-4 sm:p-6">
      <div className="flex flex-col gap-1">
        <Link href={duongVe} className="text-muted-foreground hover:text-foreground text-sm">
          ← Báo cáo trùng lặp
        </Link>
        <h1 className="text-2xl font-semibold">So sánh hai bài nộp</h1>
      </div>

      {/* Rủi ro #3 của SRS: hệ thống KHÔNG BAO GIỜ tự trừ điểm. Con số ở đây là gợi ý để đọc,
          không phải kết luận — bài nhập môn dùng chung starter code trùng 70–90% một cách
          hoàn toàn chính đáng. Bỏ dòng cảnh báo này là mời người dùng coi phần trăm như bằng
          chứng kỷ luật. */}
      <div className="border-border rounded-lg border border-l-4 border-l-amber-500 p-3 text-sm">
        Đây là <strong>gợi ý để đọc kỹ</strong>, không phải kết luận. Hệ thống{" "}
        <strong>không bao giờ tự trừ điểm</strong> và không tự kết luận sao chép. Mã khởi đầu
        do chính bạn phát, thư viện chung hay đoạn mẫu trong bài giảng đều làm hai bài trùng
        cao một cách chính đáng — hãy đọc mã trước khi quyết định.
      </div>

      <SoSanhBaiNop
        bc={bc}
        tenA={a?.trim() || "Bài nộp A"}
        tenB={b?.trim() || "Bài nộp B"}
      />
    </main>
  );
}
