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

export function TaoMonMoi() {
  const router = useRouter();
  const [mo, setMo] = useState(false);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [loi, setLoi] = useState<string | null>(null);
  const [dangGui, setDangGui] = useState(false);

  async function luu() {
    setLoi(null);
    setDangGui(true);
    try {
      await apiFetch("/api/subjects", {
        method: "POST",
        body: JSON.stringify({ code: code.trim().toUpperCase(), name: name.trim() }),
      });
      setMo(false);
      setCode("");
      setName("");
      router.refresh();
    } catch (e) {
      setLoi(e instanceof Error ? e.message : "Không tạo được môn học.");
    } finally {
      setDangGui(false);
    }
  }

  return (
    <Dialog open={mo} onOpenChange={setMo}>
      <DialogTrigger asChild>
        <Button>Tạo môn học</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Tạo môn học mới</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="code">Mã môn</Label>
            <Input
              id="code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              placeholder="IN4203"
            />
            <p className="text-muted-foreground text-xs">Chỉ gồm chữ in hoa và số.</p>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="name">Tên môn</Label>
            <Input
              id="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Lập trình di động"
            />
          </div>

          {loi && <p className="text-destructive text-sm">{loi}</p>}
        </div>

        <DialogFooter>
          <Button onClick={luu} disabled={dangGui || !code.trim() || !name.trim()}>
            {dangGui ? "Đang tạo…" : "Tạo"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
