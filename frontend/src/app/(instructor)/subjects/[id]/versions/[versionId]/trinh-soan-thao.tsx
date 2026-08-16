"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { GitBranch, Layers, Loader2, Lock, Plus } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { DauTrang, Trang, TrongRong } from "@/components/shell/trang";
import { apiFetch } from "@/lib/api/fetcher";
import {
  NHAN_TRANG_THAI,
  type ChapterDto,
  type SubjectDto,
  type SubjectVersionDto,
} from "@/lib/api/types";
import { KhoiChuong } from "./khoi-chuong";

export function TrinhSoanThao({
  subject,
  version,
}: {
  subject: SubjectDto;
  version: SubjectVersionDto;
}) {
  const router = useRouter();
  const qc = useQueryClient();
  const [tenChuongMoi, setTenChuongMoi] = useState("");
  const [loi, setLoi] = useState<string | null>(null);

  // Chỉ bản nháp mới sửa được — đây là chỗ ngăn giảng viên vô tình đổi nội dung
  // của lớp đang chạy. Backend cũng chặn bằng [RequireDraftVersion]; UI khoá trước
  // để người dùng không phải bấm rồi mới nhận lỗi.
  const suaDuoc = version.status === "draft";

  const chuongQuery = useQuery({
    queryKey: ["chapters", version.id],
    queryFn: () => apiFetch<ChapterDto[]>(`/api/subject-versions/${version.id}/chapters`),
  });

  const themChuong = useMutation({
    mutationFn: (title: string) =>
      apiFetch<ChapterDto>(`/api/subject-versions/${version.id}/chapters`, {
        method: "POST",
        body: JSON.stringify({ title }),
      }),
    onSuccess: () => {
      setTenChuongMoi("");
      setLoi(null);
      qc.invalidateQueries({ queryKey: ["chapters", version.id] });
    },
    onError: (e: Error) => setLoi(e.message),
  });

  const xuatBan = useMutation({
    mutationFn: () =>
      apiFetch(`/api/subject-versions/${version.id}/publish`, { method: "POST" }),
    onSuccess: () => router.refresh(),
    onError: (e: Error) => setLoi(e.message),
  });

  const chuong = chuongQuery.data ?? [];

  return (
    <Trang>
      <DauTrang
        quayLai={{
          href: `/subjects/${subject.id}/versions`,
          nhan: `${subject.code} · ${subject.name}`,
        }}
        tieuDe={
          <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
            {version.version_label}
            {/* Trạng thái nằm NGAY CẠNH tiêu đề chứ không ở một góc khác của trang: câu
                hỏi "bản này còn sửa được không" phải trả lời được cùng lúc với "đang mở
                bản nào". */}
            <Badge
              className={
                suaDuoc
                  ? "bg-warn-soft text-warn border-warn/25 border"
                  : "bg-ok-soft text-ok border-ok/25 border"
              }
            >
              {NHAN_TRANG_THAI[version.status]}
            </Badge>
          </span>
        }
        moTa={
          suaDuoc
            ? "Bản nháp — thêm chương, bài học và kéo-thả để sắp thứ tự."
            : "Phiên bản đã khoá. Muốn sửa thì tạo một bản nháp mới từ đây."
        }
        hanhDong={
          !suaDuoc && (
            <Button asChild variant="outline">
              <Link href={`/subjects/${subject.id}/versions`}>
                <GitBranch className="size-4" aria-hidden />
                Tạo phiên bản mới từ đây
              </Link>
            </Button>
          )
        }
      />

      {!suaDuoc && (
        // Dải cảnh báo chiếm hết bề ngang, màu hổ phách, có ổ khoá: ba tín hiệu cùng nói một
        // điều. Chỉ đổi màu nhãn trạng thái thì người quen tay vẫn bấm vào ô nhập rồi mới
        // hiểu vì sao không gõ được.
        <div className="bg-warn-soft border-warn/25 text-warn flex items-start gap-3 rounded-xl border px-4 py-3">
          <Lock className="mt-0.5 size-4 shrink-0" aria-hidden />
          <p className="text-sm text-pretty">
            <span className="font-medium">Phiên bản này chỉ xem.</span> Nội dung của các lớp
            đang dạy ghim vào đây, nên mọi ô nhập và nút sửa đều bị khoá — kể cả kéo-thả thứ
            tự bài học.
          </p>
        </div>
      )}

      {loi && (
        <p className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3 py-2 text-sm">
          {loi}
        </p>
      )}

      {chuongQuery.isPending ? (
        <p className="text-muted-foreground flex items-center gap-2 text-sm">
          <Loader2 className="size-4 animate-spin" aria-hidden />
          Đang tải chương…
        </p>
      ) : chuongQuery.isError ? (
        <p className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3 py-2 text-sm">
          Không tải được danh sách chương. Tải lại trang để thử lại.
        </p>
      ) : chuong.length === 0 ? (
        <Card>
          <CardContent>
            <TrongRong
              Icon={Layers}
              tieuDe="Phiên bản này chưa có chương nào"
              moTa={
                suaDuoc
                  ? "Chương là khung ngoài cùng: chương chứa bài học, bài học chứa các phần. Thêm chương đầu tiên ở ô bên dưới."
                  : "Bản đã xuất bản này trống. Tạo một phiên bản mới nếu muốn bổ sung nội dung."
              }
            />
          </CardContent>
        </Card>
      ) : (
        <div className="flex flex-col gap-4">
          {chuong.map((c) => (
            <KhoiChuong key={c.id} chuong={c} suaDuoc={suaDuoc} versionId={version.id} />
          ))}
        </div>
      )}

      {/* Ô thêm chương LUÔN hiển thị, chỉ `disabled` khi bản đã khoá. Ẩn hẳn thì màn hình
          chỉ-xem trông như một màn hình khác, và người dùng đi tìm nút đã "biến mất". */}
      <div className="flex flex-col gap-2">
        <div className="flex flex-wrap gap-2">
          <Input
            className="min-w-48 flex-1"
            value={tenChuongMoi}
            disabled={!suaDuoc}
            aria-label="Tên chương mới"
            onChange={(e) => setTenChuongMoi(e.target.value)}
            placeholder={suaDuoc ? "Tên chương mới" : "Đã khoá — phiên bản chỉ xem"}
            onKeyDown={(e) => {
              if (e.key === "Enter" && tenChuongMoi.trim()) themChuong.mutate(tenChuongMoi.trim());
            }}
          />
          <Button
            onClick={() => themChuong.mutate(tenChuongMoi.trim())}
            disabled={!suaDuoc || !tenChuongMoi.trim() || themChuong.isPending}
          >
            <Plus className="size-4" aria-hidden />
            Thêm chương
          </Button>
        </div>
      </div>

      {suaDuoc && (
        <div className="border-border flex flex-wrap items-center justify-between gap-3 border-t pt-4">
          <p className="text-muted-foreground max-w-md text-sm text-pretty">
            Xuất bản sẽ <strong className="font-medium">khoá phiên bản này lại</strong> và lưu
            trữ bản đang xuất bản trước đó. Sau đó chỉ sửa được bằng cách tạo phiên bản mới.
          </p>
          <Button onClick={() => xuatBan.mutate()} disabled={xuatBan.isPending}>
            {xuatBan.isPending ? "Đang xuất bản…" : "Xuất bản"}
          </Button>
        </div>
      )}
    </Trang>
  );
}
