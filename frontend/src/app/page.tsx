import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { COOKIE_ACCESS } from "@/lib/api/server";

export default async function Home() {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  redirect(token ? "/dashboard" : "/login");
}
