import { NextRequest, NextResponse } from "next/server";
import { API_BASE, luuToken, type TokenPair } from "@/lib/api/server";

export async function POST(req: NextRequest) {
  const body = await req.json();

  const res = await fetch(`${API_BASE}/api/auth/login`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    // Chuyển nguyên vẹn problem+json của backend để form hiện đúng thông báo tiếng Việt.
    const loi = await res.text();
    return new NextResponse(loi, {
      status: res.status,
      headers: { "content-type": res.headers.get("content-type") ?? "application/json" },
    });
  }

  // Backend trả snake_case (SRS mục 7). Đọc sai khoá ở đây thì token là undefined,
  // cookie rỗng, và mọi request sau đó 401 mà không có lỗi nào rõ ràng.
  const pair: TokenPair = await res.json();
  await luuToken(pair);

  return NextResponse.json({ ok: true });
}
