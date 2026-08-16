import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { DauTrang, Trang } from "@/components/shell/trang";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { components } from "@/lib/api/schema";
import { TienDoCaNhan } from "./tien-do-ca-nhan";

type MyGamificationDto = components["schemas"]["MyGamificationDto"];
type LeaderboardDto = components["schemas"]["LeaderboardDto"];
type TodayReviewDto = components["schemas"]["TodayReviewDto"];

export default async function ProgressPage({
  params,
}: {
  params: Promise<{ offeringId: string }>;
}) {
  const { offeringId } = await params;
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const headers = { authorization: `Bearer ${token}` };

  // Đồng bộ trước khi đọc: XP là số ĐƯỢC SUY RA từ hoạt động học có thật, không phải số
  // client gửi lên. Không đồng bộ thì sinh viên mở trang thấy con số cũ và tưởng hệ thống
  // không ghi nhận việc mình vừa làm.
  const me: MyGamificationDto | null = await fetch(
    `${API_BASE}/api/offerings/${offeringId}/my-gamification/sync`,
    { method: "POST", headers, cache: "no-store" },
  ).then((r) => (r.ok ? r.json() : null)).catch(() => null);

  if (me === null) notFound();

  const [bxh, onTap] = await Promise.all([
    fetch(`${API_BASE}/api/offerings/${offeringId}/leaderboard`, { headers, cache: "no-store" })
      .then((r) => (r.ok ? (r.json() as Promise<LeaderboardDto>) : null)).catch(() => null),
    fetch(`${API_BASE}/api/review/today`, { headers, cache: "no-store" })
      .then((r) => (r.ok ? (r.json() as Promise<TodayReviewDto>) : null)).catch(() => null),
  ]);

  return (
    <Trang>
      <DauTrang
        tieuDe="Tiến độ của tôi"
        moTa="Điểm kinh nghiệm, chuỗi ngày học và những câu cần ôn lại hôm nay."
        quayLai={{ href: `/learn/${offeringId}`, nhan: "Trang học" }}
      />

      <TienDoCaNhan me={me} bangXepHang={bxh} onTap={onTap} />
    </Trang>
  );
}
