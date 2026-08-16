import { CalendarClock, ChevronRight, FileText } from "lucide-react";
import Link from "next/link";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { DauTrang, Trang, TrongRong } from "@/components/shell/trang";
import { diem, khoangCach, ngayGio } from "@/lib/dinh-dang";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import { NHAN_TRANG_THAI_NOP, type MyAssignmentDto } from "@/lib/api/types";

/** 48 giờ: mốc mà "sắp tới hạn" bắt đầu đáng để tô màu. */
const SAP_HAN_MS = 48 * 60 * 60 * 1000;

export default async function MyAssignmentsPage({
  params,
}: {
  params: Promise<{ offeringId: string }>;
}) {
  const { offeringId } = await params;
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/offerings/${offeringId}/assignments`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (res.status === 404) notFound();
  if (!res.ok) redirect("/login");

  const baiTap: MyAssignmentDto[] = await res.json();
  // Server component render một lần cho mỗi request (`cache: "no-store"`), nên đọc đồng hồ ở
  // đây là an toàn và cho kết quả mới. Tính "quá hạn" ở client thì phải đọc lại đồng hồ trong
  // trình duyệt và lệch với bản máy chủ đã render — nhãn đổi màu ngay trước mắt người dùng.
  // eslint-disable-next-line react-hooks/purity -- không phải render lặp lại, không có hydrate lệch
  const bayGio = Date.now();

  return (
    <Trang>
      <DauTrang
        tieuDe="Bài tập"
        moTa="Bài tập của lớp này và trạng thái nộp của bạn."
        quayLai={{ href: `/learn/${offeringId}`, nhan: "Trang học" }}
      />

      {baiTap.length === 0 ? (
        <TrongRong
          Icon={FileText}
          tieuDe="Lớp này chưa có bài tập nào"
          moTa="Bài tập sẽ hiện ở đây ngay khi giảng viên giao. Bạn không cần làm gì lúc này."
        />
      ) : (
        <Card>
          <CardContent className="px-2">
            <ul className="flex flex-col gap-1">
              {baiTap.map((a) => {
                const hetHan = a.due_at !== null && new Date(a.due_at).getTime() < bayGio;
                const daNop = a.status !== null && a.status !== undefined && a.status !== "draft";
                const sapHan =
                  !hetHan &&
                  !daNop &&
                  a.due_at !== null &&
                  new Date(a.due_at).getTime() - bayGio < SAP_HAN_MS;

                return (
                  <li key={a.id}>
                    <Link
                      href={`/assignments/${a.id}/submit?offering=${offeringId}`}
                      className="hover:bg-accent group flex min-h-11 items-center gap-3 rounded-lg px-3 py-2.5 transition-colors"
                    >
                      <span className="flex min-w-0 flex-1 flex-col gap-1">
                        <span className="truncate font-medium">{a.title}</span>

                        {/* Nhãn trạng thái nằm ở HÀNG PHỤ chứ không ép vào cuối hàng tiêu đề:
                            ở 390px, ba nhãn cuối hàng bóp tên bài tập còn hai chữ, mà tên bài
                            mới là thứ sinh viên dùng để nhận ra bài nào. */}
                        <span className="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
                          <NhanTrangThai a={a} />

                          {a.is_late && (
                            <Badge className="bg-danger-soft text-danger border-danger/25 border">
                              Nộp trễ
                            </Badge>
                          )}

                          {hetHan && !daNop && (
                            <Badge
                              className={
                                a.allow_late_submission
                                  ? "bg-warn-soft text-warn border-warn/25 border"
                                  : "bg-danger-soft text-danger border-danger/25 border"
                              }
                            >
                              {a.allow_late_submission ? "Quá hạn · còn nhận nộp trễ" : "Đã đóng"}
                            </Badge>
                          )}

                          {sapHan && a.due_at && (
                            <Badge className="bg-warn-soft text-warn border-warn/25 border">
                              Hết hạn {khoangCach(a.due_at)}
                            </Badge>
                          )}

                          {a.due_at ? (
                            <span className="inline-flex items-center gap-1">
                              <CalendarClock className="size-3.5 shrink-0" aria-hidden />
                              {/* Mốc chính xác hiện thẳng ra: đây là con số sinh viên chép vào
                                  lịch, hiện tương đối thôi thì phải bấm vào mới biết ngày nào. */}
                              <time dateTime={a.due_at} title={ngayGio(a.due_at)}>
                                Hạn {ngayGio(a.due_at)}
                              </time>
                            </span>
                          ) : (
                            <span>Không có hạn nộp</span>
                          )}

                          {a.score != null && (
                            <span className="text-foreground font-medium tabular-nums">
                              {diem(a.score)}/{diem(a.max_score)} điểm
                            </span>
                          )}
                        </span>
                      </span>

                      <ChevronRight
                        className="text-muted-foreground group-hover:text-foreground size-4 shrink-0 transition-colors"
                        aria-hidden
                      />
                    </Link>
                  </li>
                );
              })}
            </ul>
          </CardContent>
        </Card>
      )}
    </Trang>
  );
}

function NhanTrangThai({ a }: { a: MyAssignmentDto }) {
  if (a.status == null) {
    return <Badge variant="outline">Chưa nộp</Badge>;
  }

  const nhan = NHAN_TRANG_THAI_NOP[a.status];

  // Đã chấm / đã trả bài là việc ĐÃ XONG → xanh lá. Đã nộp / đang chấm là "đang chạy, không
  // cần làm gì" → xám. Bản nháp là việc CÒN DANG DỞ của chính sinh viên → hổ phách, vì đây
  // đúng là thứ hay bị quên: tải tệp lên rồi tưởng đã nộp.
  if (a.status === "graded" || a.status === "returned") {
    return <Badge className="bg-ok-soft text-ok border-ok/25 border">{nhan}</Badge>;
  }
  if (a.status === "draft") {
    return (
      <Badge className="bg-warn-soft text-warn border-warn/25 border">Bản nháp — chưa nộp</Badge>
    );
  }
  return <Badge variant="secondary">{nhan}</Badge>;
}
