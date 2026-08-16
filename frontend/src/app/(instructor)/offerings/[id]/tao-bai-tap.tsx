"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Plus } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { apiFetch } from "@/lib/api/fetcher";
import type { AssignmentDto } from "@/lib/api/types";

// Khai đúng khóa snake_case của hợp đồng API và dùng luôn làm tên field — không có tầng
// ánh xạ camelCase ở giữa. Map tay thì server bỏ qua khóa lạ, trả 200, và bài tập được tạo
// với giá trị mặc định mà không ai biết.
const schema = z.object({
  title: z.string().min(1, "Chưa nhập tên bài tập."),
  description: z.string().optional(),
  due_at: z.string().optional(),
  allow_late_submission: z.boolean(),
  max_score: z.coerce.number().min(0, "Điểm tối đa từ 0 đến 10.").max(10, "Điểm tối đa từ 0 đến 10."),
  use_suggested_slots: z.boolean(),
});

type FormValues = z.input<typeof schema>;

export function TaoBaiTap({ subjectVersionId }: { subjectVersionId: string }) {
  const router = useRouter();
  const [mo, setMo] = useState(false);

  const {
    register, handleSubmit, setValue, watch,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      allow_late_submission: false,
      use_suggested_slots: true,
      max_score: 10,
    },
  });

  async function onSubmit(v: FormValues) {
    try {
      const bai = await apiFetch<AssignmentDto>("/api/assignments", {
        method: "POST",
        body: JSON.stringify({
          subject_version_id: subjectVersionId,
          title: v.title,
          description: v.description || null,
          // datetime-local trả giờ địa phương không có offset; Date rồi toISOString đưa
          // về UTC đúng như cột timestamptz mong đợi.
          due_at: v.due_at ? new Date(v.due_at).toISOString() : null,
          allow_late_submission: v.allow_late_submission,
          late_penalty_percent_per_day: 0,
          max_score: Number(v.max_score),
          use_suggested_slots: v.use_suggested_slots,
        }),
      });

      setMo(false);
      toast.success(`Đã tạo bài tập “${bai.title}”.`);
      router.push(`/assignments/${bai.id}/edit`);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Không tạo được bài tập.");
    }
  }

  return (
    <Dialog open={mo} onOpenChange={setMo}>
      <DialogTrigger asChild>
        <Button>
          <Plus className="size-4" aria-hidden />
          Tạo bài tập
        </Button>
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Bài tập mới</DialogTitle>
          {/* Nói rõ bài tập thuộc về PHIÊN BẢN chứ không thuộc về lớp: đây là chỗ hay
              hiểu nhầm, và hiểu nhầm nó nghĩa là sửa bài tập của cả những lớp khác. */}
          <DialogDescription>
            Bài tập gắn vào phiên bản nội dung mà lớp này đang ghim — mọi lớp dùng cùng
            phiên bản sẽ thấy nó.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4" noValidate>
          <div className="flex flex-col gap-2">
            <Label htmlFor="title">Tên bài tập</Label>
            <Input id="title" {...register("title")} />
            {errors.title && <p className="text-danger text-sm">{errors.title.message}</p>}
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="description">Mô tả</Label>
            <Textarea id="description" rows={3} {...register("description")} />
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="due_at">Hạn nộp</Label>
              <Input id="due_at" type="datetime-local" {...register("due_at")} />
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="max_score">Điểm tối đa</Label>
              <Input id="max_score" type="number" step="0.25" min={0} max={10}
                     {...register("max_score")} />
              {errors.max_score && (
                <p className="text-danger text-sm">{errors.max_score.message}</p>
              )}
            </div>
          </div>

          {/* min-h-11: cả dòng là vùng bấm, không chỉ ô vuông 16px của Checkbox — trên
              điện thoại đó là khác biệt giữa "tích được" và "tích ba lần mới trúng". */}
          <label className="flex min-h-11 items-center gap-2.5 text-sm">
            <Checkbox
              checked={watch("allow_late_submission")}
              onCheckedChange={(c) => setValue("allow_late_submission", c === true)}
            />
            Cho phép nộp trễ
          </label>

          <label className="flex min-h-11 items-center gap-2.5 text-sm">
            <Checkbox
              checked={watch("use_suggested_slots")}
              onCheckedChange={(c) => setValue("use_suggested_slots", c === true)}
            />
            Dùng mẫu gợi ý (Source code · Hình ảnh · Video)
          </label>

          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Đang tạo…" : "Tạo bài tập"}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  );
}
