"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
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
        <Button>Nhập mã lớp</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Vào lớp bằng mã</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          <Label htmlFor="ma">Mã lớp</Label>
          <Input
            id="ma"
            value={ma}
            onChange={(e) => setMa(e.target.value.toUpperCase())}
            placeholder="ABC234"
            maxLength={6}
            className="font-mono tracking-[0.3em] uppercase"
          />
          <p className="text-muted-foreground text-xs">
            Mã gồm 6 ký tự, giảng viên đọc trên lớp. Bạn phải có tên trong danh sách lớp.
          </p>
          {loi && <p className="text-destructive text-sm">{loi}</p>}
        </div>

        <DialogFooter>
          <Button onClick={vaoLop} disabled={dangGui || ma.trim().length < 6}>
            {dangGui ? "Đang vào lớp…" : "Vào lớp"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
