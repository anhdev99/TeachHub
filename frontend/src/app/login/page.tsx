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
 * One-tap accounts for development.
 *
 * ⚠️ Shown ONLY when `NODE_ENV === "development"`. These are passwords written directly into
 * the source — in production this would be a list of accounts inviting anyone to walk in.
 * Next replaces `process.env.NODE_ENV` with a constant at build time, so in a production
 * build this whole block is dropped from the bundle rather than merely hidden with CSS:
 * someone reading the page source sees nothing either.
 *
 * Dev data only. Adding a real account here turns a convenience into a vulnerability.
 */
const DEV_ACCOUNTS = [
  { label: "Giảng viên", email: "instructor@teachhub.local", password: "Teach@123" },
  { label: "Sinh viên", email: "dung@sv.edu.vn", password: "Sv@12345" },
] as const;

export default function LoginPage() {
  const router = useRouter();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  async function onSubmit(values: FormValues) {
    setFormError(null);

    // Call Next's BFF rather than .NET directly: the token has to be written into an
    // httpOnly cookie on the server, and the browser never touches it (ADR-007).
    const res = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(values),
    });

    if (!res.ok) {
      const problem = await res.json().catch(() => null);
      setFormError(problem?.detail ?? "Đăng nhập không thành công.");
      return;
    }

    const { redirect_to } = (await res.json()) as { redirect_to?: string };
    router.push(redirect_to ?? "/dashboard");
    router.refresh();
  }

  return (
    // Two columns from lg up: branding on the left, the form on the right. The old version
    // put a small box in the middle of a vast white page — the first thing a user ever sees,
    // and it said nothing about what this product is. Below lg only the form column remains;
    // we do not cram the branding panel onto a phone screen.
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

        {/* Decoration: two faint circles. `aria-hidden` because they carry no information. */}
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
          {/* Compact branding, shown only when the left column is hidden — otherwise the
              logo appears twice on the same screen. */}
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

            {formError && (
              // The form-level error gets a background and a border instead of being a bare
              // red line: right after pressing "Đăng nhập" the user's eyes are on the button,
              // and a thin line of text above it is easy to miss — so they press again.
              <p
                role="alert"
                className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3 py-2 text-sm"
              >
                {formError}
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
                  {DEV_ACCOUNTS.map((account) => (
                    <Button
                      key={account.email}
                      type="button"
                      variant="outline"
                      size="sm"
                      disabled={isSubmitting}
                      // Fill the form and THEN submit, rather than calling `onSubmit`
                      // directly: that way the email and password fields show exactly what
                      // was used, so trying a different account means editing in place
                      // instead of typing everything again.
                      onClick={() => {
                        setValue("email", account.email);
                        setValue("password", account.password);
                        void handleSubmit(onSubmit)();
                      }}
                    >
                      {account.label}
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
