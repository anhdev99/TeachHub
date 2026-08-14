"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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
import { apiFetch } from "@/lib/api/fetcher";
import { NHAN_TRANG_THAI, type SubjectVersionDto } from "@/lib/api/types";

function MauBadge(status: SubjectVersionDto["status"]) {
  // Xanh = đang dùng, vàng = còn sửa được, xám = lịch sử. Ba trạng thái phải phân biệt
  // được từ xa: đây là chỗ giảng viên kiểm tra trước khi sửa nội dung.
  switch (status) {
    case "published":
      return "default" as const;
    case "draft":
      return "secondary" as const;
    default:
      return "outline" as const;
  }
}

export function DongThoiGian({
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
      <ol className="border-border flex flex-col gap-4 border-l-2 pl-6">
        {versions.map((v) => (
          <li key={v.id} className="relative">
            <span className="bg-border absolute -left-[1.85rem] top-3 size-3 rounded-full" />

            <div className="border-border flex flex-col gap-3 rounded-lg border p-4">
              <div className="flex flex-wrap items-center gap-3">
                <span className="font-medium">{v.version_label}</span>
                <Badge variant={MauBadge(v.status)}>{NHAN_TRANG_THAI[v.status]}</Badge>
                {v.published_at && (
                  <span className="text-muted-foreground text-xs">
                    Xuất bản {new Date(v.published_at).toLocaleDateString("vi-VN")}
                  </span>
                )}
              </div>

              {v.changelog && <p className="text-muted-foreground text-sm">{v.changelog}</p>}

              <div className="flex flex-wrap gap-2">
                <Button asChild size="sm" variant="outline">
                  <Link href={`/subjects/${subjectId}/versions/${v.id}`}>
                    {v.status === "draft" ? "Soạn nội dung" : "Xem chỉ đọc"}
                  </Link>
                </Button>
                <Button size="sm" variant="ghost" onClick={() => setNguon(v)}>
                  Tạo phiên bản mới từ đây
                </Button>
              </div>
            </div>
          </li>
        ))}
      </ol>

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

            {loi && <p className="text-destructive text-sm">{loi}</p>}
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
