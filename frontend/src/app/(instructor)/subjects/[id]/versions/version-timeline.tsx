"use client";

import { Archive, FilePen, GitBranch, Layers, Lock } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { EmptyState } from "@/components/shell/page-shell";
import { formatDate } from "@/lib/format";
import { apiFetch } from "@/lib/api/fetcher";
import { VERSION_STATUS_LABELS, type SubjectVersionDto } from "@/lib/api/types";

/**
 * Ba trạng thái phải phân biệt được TỪ XA — đây là chỗ giảng viên kiểm tra trước khi sửa
 * nội dung, và sửa nhầm bản đang chạy nghĩa là đổi nội dung dưới chân một lớp đã có bài nộp.
 *
 * Màu lấy từ token ngữ nghĩa chứ không phải `variant` mặc định của Badge: `default` là màu
 * thương hiệu (xanh mực), nó không mang nghĩa "đang dùng được" — dùng nó ở đây thì đổi màu
 * thương hiệu là đổi luôn nghĩa của trạng thái.
 */
const KIEU_TRANG_THAI = {
  published: {
    badge: "bg-ok-soft text-ok border-ok/25 border",
    cham: "bg-ok",
    Icon: Lock,
  },
  draft: {
    badge: "bg-warn-soft text-warn border-warn/25 border",
    cham: "bg-warn",
    Icon: FilePen,
  },
  archived: {
    badge: "bg-muted text-muted-foreground border-border border",
    cham: "bg-border",
    Icon: Archive,
  },
} as const;

export function VersionTimeline({
  subjectId,
  versions,
}: {
  subjectId: string;
  versions: SubjectVersionDto[];
}) {
  const router = useRouter();
  const [nguon, setNguon] = useState<SubjectVersionDto | null>(null);
  const [nhan, setNhan] = useState("");
  const [changelog, setChangelog] = useState("");
  const [loi, setLoi] = useState<string | null>(null);
  const [dangGui, setDangGui] = useState(false);

  async function taoBanMoi() {
    if (!nguon) return;
    setLoi(null);
    setDangGui(true);
    try {
      const moi = await apiFetch<SubjectVersionDto>(`/api/subjects/${subjectId}/versions`, {
        method: "POST",
        body: JSON.stringify({
          cloned_from_version_id: nguon.id,
          version_label: nhan.trim(),
        }),
      });

      if (changelog.trim()) {
        await apiFetch(`/api/subject-versions/${moi.id}`, {
          method: "PUT",
          body: JSON.stringify({ changelog: changelog.trim() }),
        });
      }

      setNguon(null);
      setNhan("");
      setChangelog("");
      // Chuyển thẳng sang trang soạn thảo bản draft vừa tạo — đó là việc tiếp theo
      // người dùng chắc chắn muốn làm.
      router.push(`/subjects/${subjectId}/versions/${moi.id}`);
    } catch (e) {
      setLoi(e instanceof Error ? e.message : "Không tạo được phiên bản.");
    } finally {
      setDangGui(false);
    }
  }

  return (
    <>
      {versions.length === 0 ? (
        <Card>
          <CardContent>
            <EmptyState
              Icon={Layers}
              title="Chưa có phiên bản nào"
              description="Phiên bản là một bản chụp nội dung môn học. Lớp học phần luôn ghim vào một phiên bản đã xuất bản."
            />
          </CardContent>
        </Card>
      ) : (
        <ol className="border-border flex flex-col gap-3 border-l-2 pl-6">
          {versions.map((v) => {
            const kieu = KIEU_TRANG_THAI[v.status];
            const laNhap = v.status === "draft";

            return (
              <li key={v.id} className="relative">
                {/* Chấm mốc mang màu trạng thái: quét dọc cột mốc là thấy ngay bản nào còn
                    sửa được mà không phải đọc từng nhãn. */}
                <span
                  aria-hidden
                  className={`${kieu.cham} border-background absolute top-5 -left-[1.9rem] size-3.5 rounded-full border-2`}
                />

                <Card>
                  <CardContent className="flex flex-col gap-3">
                    <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
                      <kieu.Icon className="text-muted-foreground size-4 shrink-0" aria-hidden />
                      <span className="font-medium">{v.version_label}</span>
                      <Badge className={`${kieu.badge} shrink-0`}>
                        {VERSION_STATUS_LABELS[v.status]}
                      </Badge>
                      {v.published_at && (
                        <span className="text-muted-foreground text-xs">
                          Xuất bản{" "}
                          <time dateTime={v.published_at}>{formatDate(v.published_at)}</time>
                        </span>
                      )}
                    </div>

                    {v.changelog && (
                      <p className="text-muted-foreground text-sm text-pretty">{v.changelog}</p>
                    )}

                    <div className="flex flex-wrap gap-2">
                      <Button asChild size="sm" variant={laNhap ? "default" : "outline"}>
                        <Link href={`/subjects/${subjectId}/versions/${v.id}`}>
                          {laNhap ? "Soạn nội dung" : "Xem chỉ đọc"}
                        </Link>
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => setNguon(v)}>
                        <GitBranch className="size-4" aria-hidden />
                        Tạo phiên bản mới từ đây
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              </li>
            );
          })}
        </ol>
      )}

      <Dialog open={nguon !== null} onOpenChange={(o) => !o && setNguon(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Tạo phiên bản mới từ “{nguon?.version_label}”</DialogTitle>
          </DialogHeader>

          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="nhan">Nhãn phiên bản</Label>
              <Input
                id="nhan"
                value={nhan}
                onChange={(e) => setNhan(e.target.value)}
                placeholder="2027-2028"
              />
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="changelog">Ghi chú thay đổi (tuỳ chọn)</Label>
              <Textarea
                id="changelog"
                value={changelog}
                onChange={(e) => setChangelog(e.target.value)}
                placeholder="Cập nhật chương 3 theo giáo trình mới"
              />
            </div>

            <p className="text-muted-foreground text-xs">
              Toàn bộ chương, bài học và component sẽ được nhân bản. Phiên bản nguồn không đổi.
            </p>

            {loi && (
              <p className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3 py-2 text-sm">
                {loi}
              </p>
            )}
          </div>

          <DialogFooter>
            <Button onClick={taoBanMoi} disabled={dangGui || !nhan.trim()}>
              {dangGui ? "Đang nhân bản…" : "Tạo phiên bản"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
