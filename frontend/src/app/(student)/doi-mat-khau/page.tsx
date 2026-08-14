"use client";

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
import { apiFetch } from "@/lib/api/fetcher";

/**
 * Trang này PHẢI tồn tại và phải gọi được khi đang bị middleware chặn — nếu không, sinh viên
 * đăng nhập lần đầu bị chặn ở mọi endpoint kể cả đường để tự gỡ chặn.
 */
export default function DoiMatKhauPage() {
  const router = useRouter();
  const [cu, setCu] = useState("");
  const [moi, setMoi] = useState("");
  const [nhacLai, setNhacLai] = useState("");
  const [loi, setLoi] = useState<string | null>(null);
  const [dangGui, setDangGui] = useState(false);

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
    <main className="flex min-h-svh items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Đổi mật khẩu lần đầu</CardTitle>
          <CardDescription>
            Bạn cần đặt mật khẩu riêng trước khi dùng hệ thống.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={doi} className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="cu">Mật khẩu hiện tại</Label>
              <Input id="cu" type="password" value={cu} onChange={(e) => setCu(e.target.value)} />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="moi">Mật khẩu mới</Label>
              <Input id="moi" type="password" value={moi} onChange={(e) => setMoi(e.target.value)} />
              <p className="text-muted-foreground text-xs">Ít nhất 8 ký tự.</p>
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="nhac">Nhập lại mật khẩu mới</Label>
              <Input
                id="nhac"
                type="password"
                value={nhacLai}
                onChange={(e) => setNhacLai(e.target.value)}
              />
            </div>

            {loi && <p className="text-destructive text-sm">{loi}</p>}

            <Button type="submit" disabled={dangGui || !cu || moi.length < 8}>
              {dangGui ? "Đang đổi…" : "Đổi mật khẩu"}
            </Button>
          </form>
        </CardContent>
      </Card>
    </main>
  );
}
