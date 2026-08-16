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

async function proxy(req: NextRequest, ctx: Ctx) {
  const { path } = await ctx.params;
  const jar = await cookies();

  const url = `${API_BASE}/api/${path.join("/")}${req.nextUrl.search}`;
  const body = req.method === "GET" || req.method === "HEAD" ? undefined : await req.arrayBuffer();

  const call = (accessToken: string | undefined) =>
    fetch(url, {
      method: req.method,
      headers: {
        ...(req.headers.get("content-type")
          ? { "content-type": req.headers.get("content-type")! }
          : {}),
        ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
      },
      body,
      // The token lives in a server-side httpOnly cookie; do not forward cookies to .NET.
      cache: "no-store",
    });

  let res = await call(jar.get(COOKIE_ACCESS)?.value);

  // If the access token expired, refresh EXACTLY ONCE and retry. Looping without a limit
  // here turns one broken refresh token into an infinite loop hammering the backend.
  if (res.status === 401) {
    const refresh = jar.get(COOKIE_REFRESH)?.value;
    if (refresh) {
      const refreshed = await fetch(`${API_BASE}/api/auth/refresh`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ refresh_token: refresh }),
      });

      if (refreshed.ok) {
        const pair: TokenPair = await refreshed.json();
        jar.set(COOKIE_ACCESS, pair.access_token, cookieOptions(pair.expires_in));
        jar.set(COOKIE_REFRESH, pair.refresh_token, cookieOptions(60 * 60 * 24 * 14));
        res = await call(pair.access_token);
      } else {
        jar.delete(COOKIE_ACCESS);
        jar.delete(COOKIE_REFRESH);
      }
    }
  }

  const payload = await res.arrayBuffer();
  return new NextResponse(payload, {
    status: res.status,
    headers: {
      "content-type": res.headers.get("content-type") ?? "application/json",
    },
  });
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
