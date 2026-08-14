import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";

type MeDto = {
  id: string;
  email: string;
  full_name: string;
  roles: string[];
};

export default async function DashboardPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  // Server component gọi thẳng .NET API kèm token đọc từ cookie httpOnly — không đi vòng
  // qua proxy vì đã ở phía máy chủ rồi.
  const res = await fetch(`${API_BASE}/api/auth/me`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (!res.ok) redirect("/login");

  const me: MeDto = await res.json();

  return (
    <main className="mx-auto flex min-h-svh max-w-3xl flex-col gap-6 p-6">
      <h1 className="text-2xl font-semibold">Bảng điều khiển</h1>

      <Card>
        <CardHeader>
          <CardTitle>{me.full_name}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-1 text-sm">
          <p>{me.email}</p>
          <p className="text-muted-foreground">Vai trò: {me.roles.join(", ")}</p>
        </CardContent>
      </Card>
    </main>
  );
}
