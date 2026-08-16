import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { DauTrang, Trang } from "@/components/shell/trang";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { RubricDto } from "@/lib/api/types";
import { SoanRubric } from "./soan-rubric";

export default async function RubricsPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/rubrics`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (!res.ok) redirect("/login");

  const rubrics: RubricDto[] = await res.json();

  return (
    <Trang>
      <DauTrang
        tieuDe="Bộ tiêu chí chấm"
        moTa="Bộ tiêu chí thuộc về bạn chứ không thuộc về một môn — dùng lại được cho mọi bài của mọi lớp."
      />

      <SoanRubric banDau={rubrics} />
    </Trang>
  );
}
