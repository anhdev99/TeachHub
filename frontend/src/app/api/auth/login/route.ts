import { NextRequest, NextResponse } from "next/server";
import { API_BASE, saveTokens, type TokenPair } from "@/lib/api/server";

export async function POST(req: NextRequest) {
  const body = await req.json();

  const res = await fetch(`${API_BASE}/api/auth/login`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    // Pass the backend's problem+json through untouched so the form shows the exact
    // Vietnamese message.
    const error = await res.text();
    return new NextResponse(error, {
      status: res.status,
      headers: { "content-type": res.headers.get("content-type") ?? "application/json" },
    });
  }

  // The backend returns snake_case (SRS §7). Reading the wrong key here leaves the token
  // undefined, the cookie empty, and every later request 401 with no clear error anywhere.
  const pair: TokenPair = await res.json();
  await saveTokens(pair);

  // Return a destination that MATCHES THE ROLE. The login page used to always push to
  // /dashboard, so students landed on the instructor screen — and if they still had to change
  // their password on first sign-in, nothing led them to where they could unblock themselves.
  const me = await fetch(`${API_BASE}/api/auth/me`, {
    headers: { authorization: `Bearer ${pair.access_token}` },
    cache: "no-store",
  });

  let destination = "/dashboard";
  if (me.ok) {
    const { roles } = (await me.json()) as { roles: string[] };
    destination = roles.includes("Instructor") ? "/subjects" : "/learn";
  }

  return NextResponse.json({ ok: true, redirect_to: destination });
}
