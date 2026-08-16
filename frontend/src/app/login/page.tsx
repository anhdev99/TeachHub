"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
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

const schema = z.object({
  email: z.email("Email không đúng định dạng."),
  password: z.string().min(1, "Chưa nhập mật khẩu."),
});

type FormValues = z.infer<typeof schema>;

/**
 * Tài khoản bấm-một-phát khi phát triển.
 *
 * ⚠️ CHỈ hiện khi `NODE_ENV === "development"`. Đây là mật khẩu viết thẳng trong mã nguồn —
 * ở production nó là một danh sách tài khoản mời mọi người vào thử. `process.env.NODE_ENV`
 * được Next thay bằng hằng số lúc build, nên ở bản production cả khối này bị loại khỏi bundle
 * chứ không phải chỉ ẩn bằng CSS: người xem mã nguồn trang cũng không thấy gì.
 *
 * Chỉ dùng cho dữ liệu dev. Thêm tài khoản thật vào đây là biến tiện lợi thành lỗ hổng.
 */
const TAI_KHOAN_DEV = [
  { nhan: "Giảng viên", email: "instructor@teachhub.local", password: "Teach@123" },
  { nhan: "Sinh viên", email: "dung@sv.edu.vn", password: "Sv@12345" },
] as const;

export default function LoginPage() {
  const router = useRouter();
  const [loiChung, setLoiChung] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  async function onSubmit(values: FormValues) {
    setLoiChung(null);

    // Gọi BFF của Next chứ không gọi thẳng .NET: token phải được đặt vào cookie
    // httpOnly ở phía máy chủ, trình duyệt không bao giờ chạm vào nó (ADR-007).
    const res = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(values),
    });

    if (!res.ok) {
      const problem = await res.json().catch(() => null);
      setLoiChung(problem?.detail ?? "Đăng nhập không thành công.");
      return;
    }

    const { redirect_to } = (await res.json()) as { redirect_to?: string };
    router.push(redirect_to ?? "/dashboard");
    router.refresh();
  }

  return (
    // Hai cột từ lg: bên trái là bảng hiệu, bên phải là form. Bản cũ đặt một hộp nhỏ giữa
    // trang trắng mênh mông — thứ đầu tiên người dùng thấy, và nó không nói được sản phẩm này
    // là gì. Dưới lg thì chỉ còn cột form, không nhét bảng hiệu vào màn hình điện thoại.
    <main className="grid min-h-svh lg:grid-cols-2">
      <section className="bg-primary text-primary-foreground relative hidden flex-col justify-between overflow-hidden p-10 lg:flex">
        <div className="flex items-center gap-2.5">
          <span className="bg-primary-foreground/15 grid size-9 place-items-center rounded-lg text-base font-bold">
            T
          </span>
          <span className="text-lg font-semibold tracking-tight">TeachHub</span>
        </div>

        <div className="flex max-w-md flex-col gap-4">
          <p className="text-3xl leading-snug font-semibold tracking-tight text-balance">
            Soạn bài, giao bài, chấm điểm — trong một chỗ.
          </p>
          <p className="text-primary-foreground/75 text-pretty">
            Nội dung có phiên bản nên lớp đang chạy không bị đổi giữa chừng. Sổ điểm tự tính
            theo trọng số bạn đặt, và mọi lần sửa điểm đều còn dấu vết.
          </p>
        </div>

        {/* Trang trí: hai vòng tròn mờ. `aria-hidden` vì chúng không mang thông tin nào. */}
        <div
          aria-hidden
          className="bg-primary-foreground/5 pointer-events-none absolute -right-24 -bottom-32 size-96 rounded-full"
        />
        <div
          aria-hidden
          className="bg-primary-foreground/5 pointer-events-none absolute -top-20 -right-10 size-56 rounded-full"
        />

        <p className="text-primary-foreground/60 text-xs">
          Hệ thống quản lý dạy học cá nhân
        </p>
      </section>

      <section className="flex items-center justify-center p-4 sm:p-8">
      <Card className="w-full max-w-sm">
        <CardHeader>
          {/* Bảng hiệu thu nhỏ, chỉ hiện khi cột trái bị ẩn — nếu không thì logo xuất hiện
              hai lần trên cùng một màn hình. */}
          <span className="bg-primary text-primary-foreground mb-2 grid size-9 place-items-center rounded-lg text-sm font-bold lg:hidden">
            T
          </span>
          <CardTitle className="text-xl">Đăng nhập TeachHub</CardTitle>
          <CardDescription>Dành cho giảng viên và sinh viên.</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4" noValidate>
            <div className="flex flex-col gap-2">
              <Label htmlFor="email">Email</Label>
              <Input id="email" type="email" autoComplete="username" {...register("email")} />
              {errors.email && (
                <p className="text-destructive text-sm">{errors.email.message}</p>
              )}
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="password">Mật khẩu</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                {...register("password")}
              />
              {errors.password && (
                <p className="text-destructive text-sm">{errors.password.message}</p>
              )}
            </div>

            {loiChung && (
              // Lỗi chung có nền và viền chứ không phải một dòng chữ đỏ trần: sau khi bấm
              // "Đăng nhập" mắt người dùng đang ở nút, một dòng chữ mảnh phía trên rất dễ bị
              // bỏ qua và họ bấm lại lần nữa.
              <p
                role="alert"
                className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3 py-2 text-sm"
              >
                {loiChung}
              </p>
            )}

            <Button type="submit" disabled={isSubmitting} className="min-h-11">
              {isSubmitting ? "Đang đăng nhập…" : "Đăng nhập"}
            </Button>

            {process.env.NODE_ENV === "development" && (
              <div className="border-border flex flex-col gap-2 border-t pt-3">
                <p className="text-muted-foreground text-xs">
                  Chỉ có ở máy phát triển — bấm để vào thẳng:
                </p>
                <div className="flex flex-wrap gap-2">
                  {TAI_KHOAN_DEV.map((tk) => (
                    <Button
                      key={tk.email}
                      type="button"
                      variant="outline"
                      size="sm"
                      disabled={isSubmitting}
                      // Điền vào form RỒI submit, không gọi thẳng `onSubmit`: như vậy ô email
                      // và mật khẩu hiện đúng thứ vừa dùng, nên khi cần thử một tài khoản khác
                      // thì sửa tại chỗ được thay vì gõ lại từ đầu.
                      onClick={() => {
                        setValue("email", tk.email);
                        setValue("password", tk.password);
                        void handleSubmit(onSubmit)();
                      }}
                    >
                      {tk.nhan}
                    </Button>
                  ))}
                </div>
              </div>
            )}
          </form>
        </CardContent>
      </Card>
      </section>
    </main>
  );
}
