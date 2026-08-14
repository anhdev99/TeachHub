"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
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

  return (
    <main className="mx-auto flex min-h-svh max-w-4xl flex-col gap-6 p-6">
      <header className="flex flex-col gap-2">
        <Link
          href={`/subjects/${subject.id}/versions`}
          className="text-muted-foreground hover:text-foreground text-sm"
        >
          ← {subject.code} · {subject.name}
        </Link>

        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-semibold">{version.version_label}</h1>
          <Badge variant={suaDuoc ? "secondary" : "outline"}>
            {NHAN_TRANG_THAI[version.status]}
          </Badge>
        </div>
      </header>

      {!suaDuoc && (
        <div className="border-border bg-muted flex flex-wrap items-center justify-between gap-3 rounded-lg border p-4">
          <p className="text-sm">
            Phiên bản này đã xuất bản — chỉ xem. Nội dung của các lớp đang dạy phụ thuộc vào nó.
          </p>
          <Button asChild size="sm" variant="outline">
            <Link href={`/subjects/${subject.id}/versions`}>Tạo phiên bản mới từ đây</Link>
          </Button>
        </div>
      )}

      {loi && <p className="text-destructive text-sm">{loi}</p>}

      {chuongQuery.isPending ? (
        <p className="text-muted-foreground text-sm">Đang tải chương…</p>
      ) : chuongQuery.isError ? (
        <p className="text-destructive text-sm">Không tải được danh sách chương.</p>
      ) : (
        <div className="flex flex-col gap-4">
          {chuongQuery.data.length === 0 && (
            <p className="text-muted-foreground text-sm">
              Chưa có chương nào. Thêm chương đầu tiên bên dưới.
            </p>
          )}

          {chuongQuery.data.map((c) => (
            <KhoiChuong key={c.id} chuong={c} suaDuoc={suaDuoc} versionId={version.id} />
          ))}
        </div>
      )}

      {suaDuoc && (
        <div className="flex flex-col gap-4">
          <div className="flex gap-2">
            <Input
              value={tenChuongMoi}
              onChange={(e) => setTenChuongMoi(e.target.value)}
              placeholder="Tên chương mới"
              onKeyDown={(e) => {
                if (e.key === "Enter" && tenChuongMoi.trim()) themChuong.mutate(tenChuongMoi.trim());
              }}
            />
            <Button
              onClick={() => themChuong.mutate(tenChuongMoi.trim())}
              disabled={!tenChuongMoi.trim() || themChuong.isPending}
            >
              Thêm chương
            </Button>
          </div>

          <div className="border-border flex items-center justify-between gap-3 border-t pt-4">
            <p className="text-muted-foreground text-sm">
              Xuất bản sẽ khoá phiên bản này lại và lưu trữ bản đang xuất bản trước đó.
            </p>
            <Button
              variant="default"
              onClick={() => xuatBan.mutate()}
              disabled={xuatBan.isPending}
            >
              {xuatBan.isPending ? "Đang xuất bản…" : "Xuất bản"}
            </Button>
          </div>
        </div>
      )}
    </main>
  );
}
