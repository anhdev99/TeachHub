import { cookies } from "next/headers";
import { NextRequest, NextResponse } from "next/server";
import {
  API_BASE,
  COOKIE_ACCESS,
  COOKIE_REFRESH,
  cookieOptions,
  type TokenPair,
} from "@/lib/api/server";

type Ctx = { params: Promise<{ path: string[] }> };

async function chuyenTiep(req: NextRequest, ctx: Ctx) {
  const { path } = await ctx.params;
  const jar = await cookies();

  const url = `${API_BASE}/api/${path.join("/")}${req.nextUrl.search}`;
  const body = req.method === "GET" || req.method === "HEAD" ? undefined : await req.arrayBuffer();

  const goi = (accessToken: string | undefined) =>
    fetch(url, {
      method: req.method,
      headers: {
        ...(req.headers.get("content-type")
          ? { "content-type": req.headers.get("content-type")! }
          : {}),
        ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
      },
      body,
      // Token nằm trong cookie httpOnly ở phía máy chủ; không chuyển tiếp cookie sang .NET.
      cache: "no-store",
    });

  let res = await goi(jar.get(COOKIE_ACCESS)?.value);

  // Access token hết hạn thì làm mới ĐÚNG MỘT LẦN rồi thử lại. Lặp không giới hạn ở đây
  // biến một refresh token hỏng thành vòng lặp vô tận đập vào backend.
  if (res.status === 401) {
    const refresh = jar.get(COOKIE_REFRESH)?.value;
    if (refresh) {
      const lamMoi = await fetch(`${API_BASE}/api/auth/refresh`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ refresh_token: refresh }),
      });

      if (lamMoi.ok) {
        const pair: TokenPair = await lamMoi.json();
        jar.set(COOKIE_ACCESS, pair.access_token, cookieOptions(pair.expires_in));
        jar.set(COOKIE_REFRESH, pair.refresh_token, cookieOptions(60 * 60 * 24 * 14));
        res = await goi(pair.access_token);
      } else {
        jar.delete(COOKIE_ACCESS);
        jar.delete(COOKIE_REFRESH);
      }
    }
  }

  const noiDung = await res.arrayBuffer();
  return new NextResponse(noiDung, {
    status: res.status,
    headers: {
      "content-type": res.headers.get("content-type") ?? "application/json",
    },
  });
}

export const GET = chuyenTiep;
export const POST = chuyenTiep;
export const PUT = chuyenTiep;
export const PATCH = chuyenTiep;
export const DELETE = chuyenTiep;
