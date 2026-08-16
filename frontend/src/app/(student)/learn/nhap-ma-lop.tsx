"use client";

import { KeyRound } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { apiFetch } from "@/lib/api/fetcher";

export function NhapMaLop() {
  const router = useRouter();
  const [mo, setMo] = useState(false);
  const [ma, setMa] = useState("");
  const [loi, setLoi] = useState<string | null>(null);
  const [dangGui, setDangGui] = useState(false);

  const duMa = ma.trim().length === 6;

  async function vaoLop() {
    setLoi(null);
    setDangGui(true);
    try {
      await apiFetch("/api/offerings/join", {
        method: "POST",
        body: JSON.stringify({ join_code: ma.trim().toUpperCase() }),
      });
      setMo(false);
      setMa("");
      router.refresh();
    } catch (e) {
      setLoi(e instanceof Error ? e.message : "Không vào được lớp.");
    } finally {
      setDangGui(false);
    }
  }

  return (
    <Dialog open={mo} onOpenChange={setMo}>
      <DialogTrigger asChild>
        <Button className="min-h-11">
          <KeyRound aria-hidden />
          Nhập mã lớp
        </Button>
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Vào lớp bằng mã</DialogTitle>
          <DialogDescription>
            Mã gồm 6 ký tự, giảng viên đọc trên lớp. Bạn phải có tên trong danh sách lớp thì mã
            mới dùng được.
          </DialogDescription>
        </DialogHeader>

        {/* <form> để phím Enter trên bàn phím điện thoại cũng gửi được: bắt sinh viên gõ xong
            rồi thu bàn phím lại để tìm nút là thêm một bước không cần thiết. */}
        <form
          className="flex flex-col gap-3"
          onSubmit={(e) => {
            e.preventDefault();
            if (duMa && !dangGui) void vaoLop();
          }}
        >
          <Label htmlFor="ma">Mã lớp</Label>
          <Input
            id="ma"
            value={ma}
            onChange={(e) => setMa(e.target.value.toUpperCase())}
            placeholder="ABC234"
            maxLength={6}
            autoFocus
            autoCapitalize="characters"
            autoComplete="off"
            spellCheck={false}
            aria-describedby={loi ? "loi-ma" : undefined}
            className="h-14 text-center font-mono text-xl tracking-[0.4em] uppercase"
          />

          {loi && (
            // Nền + viền chứ không phải một dòng chữ đỏ trần: sau khi bấm "Vào lớp" mắt người
            // dùng đang ở nút, một dòng mảnh phía trên rất dễ bị bỏ qua và họ bấm lại lần nữa.
            <p
              id="loi-ma"
              role="alert"
              className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3 py-2 text-sm"
            >
              {loi}
            </p>
          )}

          <DialogFooter>
            <Button type="submit" disabled={dangGui || !duMa} className="min-h-11 w-full sm:w-auto">
              {dangGui ? "Đang vào lớp…" : "Vào lớp"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
