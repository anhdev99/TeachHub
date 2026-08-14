import { cookies } from "next/headers";

export const API_BASE = process.env.API_BASE_URL ?? "http://localhost:5080";

export const COOKIE_ACCESS = "th_at";
export const COOKIE_REFRESH = "th_rt";

const isProd = process.env.NODE_ENV === "production";

/**
 * httpOnly: JavaScript phía client không đọc được token. secure chỉ bật ở production
 * vì dev chạy trên http://localhost — bật cứng thì trình duyệt bỏ cookie và không ai
 * đăng nhập được ở máy phát triển.
 */
export function cookieOptions(maxAge: number) {
  return {
    httpOnly: true,
    secure: isProd,
    sameSite: "lax" as const,
    path: "/",
    maxAge,
  };
}

export type TokenPair = {
  access_token: string;
  refresh_token: string;
  expires_in: number;
};

export async function luuToken(pair: TokenPair) {
  const jar = await cookies();
  jar.set(COOKIE_ACCESS, pair.access_token, cookieOptions(pair.expires_in));
  jar.set(COOKIE_REFRESH, pair.refresh_token, cookieOptions(60 * 60 * 24 * 14));
}

export async function xoaToken() {
  const jar = await cookies();
  jar.delete(COOKIE_ACCESS);
  jar.delete(COOKIE_REFRESH);
}
