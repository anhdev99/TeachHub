import { NextResponse } from "next/server";
import { xoaToken } from "@/lib/api/server";

export async function POST() {
  await xoaToken();
  return NextResponse.json({ ok: true });
}
