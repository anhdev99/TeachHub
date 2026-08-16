import { NextResponse } from "next/server";
import { clearTokens } from "@/lib/api/server";

export async function POST() {
  await clearTokens();
  return NextResponse.json({ ok: true });
}
