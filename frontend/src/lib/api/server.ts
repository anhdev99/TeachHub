import { cookies } from "next/headers";

export const API_BASE = process.env.API_BASE_URL ?? "http://localhost:9810";

export const COOKIE_ACCESS = "th_at";
export const COOKIE_REFRESH = "th_rt";

const isProd = process.env.NODE_ENV === "production";

/**
 * httpOnly: client-side JavaScript cannot read the token. secure is only enabled in
 * production because dev runs on http://localhost — forcing it on makes the browser drop the
 * cookie, and nobody can sign in on a development machine.
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

export async function saveTokens(pair: TokenPair) {
  const jar = await cookies();
  jar.set(COOKIE_ACCESS, pair.access_token, cookieOptions(pair.expires_in));
  jar.set(COOKIE_REFRESH, pair.refresh_token, cookieOptions(60 * 60 * 24 * 14));
}

export async function clearTokens() {
  const jar = await cookies();
  jar.delete(COOKIE_ACCESS);
  jar.delete(COOKIE_REFRESH);
}
