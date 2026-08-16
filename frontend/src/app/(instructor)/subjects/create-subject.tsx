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

export function CreateSubject() {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function save() {
    setError(null);
    setSubmitting(true);
    try {
      await apiFetch("/api/subjects", {
        method: "POST",
        body: JSON.stringify({ code: code.trim().toUpperCase(), name: name.trim() }),
      });
      setOpen(false);
      setCode("");
      setName("");
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không tạo được môn học.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
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

          {/* The `detail` field of ProblemDetails is already a Vietnamese sentence written for
              the user — show it verbatim, never collapse it to "Something went wrong". */}
          {error && (
            <p className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3 py-2 text-sm">
              {error}
            </p>
          )}
        </div>

        <DialogFooter>
          <Button onClick={save} disabled={submitting || !code.trim() || !name.trim()}>
            {submitting ? "Đang tạo…" : "Tạo"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
