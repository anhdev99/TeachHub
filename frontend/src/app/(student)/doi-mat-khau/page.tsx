"use client";

import { Eye, EyeOff, ShieldCheck } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageShell } from "@/components/shell/page-shell";
import { apiFetch } from "@/lib/api/fetcher";
import { cn } from "@/lib/utils";

/**
 * Trang này PHẢI tồn tại và phải gọi được khi đang bị middleware chặn — nếu không, sinh viên
 * đăng nhập lần đầu bị chặn ở mọi endpoint kể cả đường để tự gỡ chặn.
 */
export default function DoiMatKhauPage() {
  const router = useRouter();
  const [cu, setCu] = useState("");
  const [moi, setMoi] = useState("");
  const [nhacLai, setNhacLai] = useState("");
  const [hien, setHien] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);
  const [dangGui, setDangGui] = useState(false);

  const duDai = moi.length >= 8;
  const khop = nhacLai.length > 0 && moi === nhacLai;
  const lechNhau = nhacLai.length > 0 && moi !== nhacLai;

  async function doi(e: React.FormEvent) {
    e.preventDefault();
    setLoi(null);

    if (moi !== nhacLai) {
      setLoi("Hai lần nhập mật khẩu mới không khớp.");
      return;
    }

    setDangGui(true);
    try {
      await apiFetch("/api/auth/change-password", {
        method: "POST",
        body: JSON.stringify({ current_password: cu, new_password: moi }),
      });
      router.push("/learn");
      router.refresh();
    } catch (err) {
      setLoi(err instanceof Error ? err.message : "Không đổi được mật khẩu.");
    } finally {
      setDangGui(false);
    }
  }

  return (
    <PageShell>
      <Card className="mx-auto w-full max-w-md">
        <CardHeader>
          <span className="bg-primary/10 text-primary mb-1 grid size-9 place-items-center rounded-lg">
            <ShieldCheck className="size-5" aria-hidden />
          </span>
          <CardTitle className="text-xl">Đổi mật khẩu lần đầu</CardTitle>
          <CardDescription className="text-pretty">
            Mật khẩu hiện tại do hệ thống sinh ngẫu nhiên khi giảng viên thêm bạn vào lớp. Đặt
            mật khẩu riêng rồi bạn mới vào được trang học.
          </CardDescription>
        </CardHeader>

        <CardContent>
          <form onSubmit={doi} className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="cu">Mật khẩu hiện tại</Label>
              <Input
                id="cu"
                type={hien ? "text" : "password"}
                autoComplete="current-password"
                className="h-11"
                value={cu}
                onChange={(e) => setCu(e.target.value)}
              />
            </div>

            <div className="flex flex-col gap-2">
              <div className="flex items-center justify-between gap-2">
                <Label htmlFor="moi">Mật khẩu mới</Label>
                {/* Nút hiện/ẩn cho CẢ BA ô: gõ mật khẩu dài trên bàn phím điện thoại mà không
                    nhìn thấy gì là lý do người dùng đặt mật khẩu ngắn nhất có thể. */}
                <button
                  type="button"
                  onClick={() => setHien((v) => !v)}
                  // `-my-3 py-3` mở rộng vùng chạm lên 44px mà không đẩy hàng nhãn cao thêm:
                  // một nút chữ cao 16px bên cạnh ô nhập là thứ trên điện thoại bấm ba lần
                  // mới trúng.
                  className="text-muted-foreground hover:text-foreground -my-3 -mx-1 inline-flex items-center gap-1.5 px-1 py-3 text-xs transition-colors"
                >
                  {hien ? <EyeOff className="size-3.5" aria-hidden /> : <Eye className="size-3.5" aria-hidden />}
                  {hien ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                </button>
              </div>
              <Input
                id="moi"
                type={hien ? "text" : "password"}
                autoComplete="new-password"
                className="h-11"
                value={moi}
                onChange={(e) => setMoi(e.target.value)}
                aria-describedby="dk-moi"
              />
              <p
                id="dk-moi"
                className={cn(
                  "text-xs",
                  moi.length === 0
                    ? "text-muted-foreground"
                    : duDai
                      ? "text-ok"
                      : "text-warn",
                )}
              >
                Ít nhất 8 ký tự.
              </p>
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="nhac">Nhập lại mật khẩu mới</Label>
              <Input
                id="nhac"
                type={hien ? "text" : "password"}
                autoComplete="new-password"
                className="h-11"
                value={nhacLai}
                onChange={(e) => setNhacLai(e.target.value)}
                aria-invalid={lechNhau || undefined}
                aria-describedby={lechNhau ? "dk-nhac" : undefined}
              />
              {/* Báo lệch NGAY khi gõ, không đợi bấm nút: bắt người dùng gõ xong cả form rồi
                  mới nói "hai lần nhập không khớp" là bắt họ gõ lại cả hai ô. */}
              {lechNhau && (
                <p id="dk-nhac" className="text-warn text-xs">
                  Hai lần nhập chưa khớp nhau.
                </p>
              )}
              {khop && duDai && <p className="text-ok text-xs">Hai lần nhập đã khớp.</p>}
            </div>

            {loi && (
              <p
                role="alert"
                className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3 py-2 text-sm text-pretty"
              >
                {loi}
              </p>
            )}

            <Button
              type="submit"
              disabled={dangGui || !cu || !duDai || !khop}
              className="min-h-11"
            >
              {dangGui ? "Đang đổi…" : "Đổi mật khẩu"}
            </Button>
          </form>
        </CardContent>
      </Card>
    </PageShell>
  );
}
