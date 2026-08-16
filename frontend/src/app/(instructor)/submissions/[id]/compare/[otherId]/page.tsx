import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { DauTrang, Trang } from "@/components/shell/trang";
import { CanhBaoTrungLap } from "@/components/submission/canh-bao-trung-lap";
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

  const duongVe =
    assignment && offering
      ? `/assignments/${assignment}/similarity?offering=${offering}`
      : offering
        ? `/offerings/${offering}/grading`
        : "/dashboard";

  // 400 là ca thật, không phải lỗi lập trình: hai bài nộp không cùng một bài tập thì không so
  // được. Hiện nguyên câu tiếng Việt backend trả về thay vì đẩy người dùng về trang đăng nhập.
  if (res.status === 400) {
    const problem: ProblemDetails = await res.json().catch(() => ({}) as ProblemDetails);
    return (
      <Trang>
        <DauTrang
          tieuDe="Không so sánh được"
          quayLai={{ href: duongVe, nhan: "Báo cáo trùng lặp" }}
        />
        {/* Đây là lỗi thật của thao tác nên dùng `danger`; khung hổ phách phía dưới dành
            riêng cho chuyện "trùng lặp không phải kết tội" — trộn hai màu là mất cả hai nghĩa. */}
        <p className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3.5 py-3 text-sm text-pretty">
          {problem.detail ?? "Hai bài nộp này không so sánh được."}
        </p>
      </Trang>
    );
  }

  if (!res.ok) redirect("/login");

  const bc: SubmissionComparisonDto = await res.json();

  return (
    <Trang rong>
      <DauTrang
        tieuDe="So sánh hai bài nộp"
        moTa="Đoạn tô nền hổ phách là phần mã hai bên dùng chung."
        quayLai={{ href: duongVe, nhan: "Báo cáo trùng lặp" }}
      />

      <CanhBaoTrungLap />

      <SoSanhBaiNop
        bc={bc}
        tenA={a?.trim() || "Bài nộp A"}
        tenB={b?.trim() || "Bài nộp B"}
      />
    </Trang>
  );
}
