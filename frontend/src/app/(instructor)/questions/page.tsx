import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { DauTrang, Trang } from "@/components/shell/trang";
import { API_BASE, COOKIE_ACCESS } from "@/lib/api/server";
import type { QuizQuestionDto } from "@/lib/api/types";
import { NganHangCauHoi } from "./ngan-hang-cau-hoi";

export default async function QuestionsPage() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) redirect("/login");

  const res = await fetch(`${API_BASE}/api/questions`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (!res.ok) redirect("/login");

  const cau: QuizQuestionDto[] = await res.json();

  return (
    <Trang>
      <DauTrang
        tieuDe="Ngân hàng câu hỏi"
        moTa="Câu hỏi thuộc về bạn chứ không thuộc một môn — dùng lại được cho mọi bài trắc nghiệm của mọi lớp."
      />

      <NganHangCauHoi banDau={cau} />
    </Trang>
  );
}
