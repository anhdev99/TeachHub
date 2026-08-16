import { cookies } from "next/headers";
import { API_BASE, COOKIE_ACCESS } from "./server";

export type CurrentUser = {
  full_name?: string;
  email?: string;
  roles?: string[];
};

/**
 * Read the current user on the server.
 *
 * Pulled out of the individual pages because each role's layout needs it to build its
 * navigation, and if every page did its own call then one page forgetting to check the token
 * is one page that renders with empty data instead of redirecting to the login screen.
 * Returns `null` when nobody is signed in, so the caller decides where to redirect — calling
 * `redirect()` from inside a shared helper makes the control flow hard for the caller to
 * follow.
 */
export async function getCurrentUser(): Promise<CurrentUser | null> {
  const token = (await cookies()).get(COOKIE_ACCESS)?.value;
  if (!token) return null;

  const res = await fetch(`${API_BASE}/api/auth/me`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (!res.ok) return null;
  return (await res.json()) as CurrentUser;
}

/** Initial used to build an avatar while there is no picture. */
export function initialOf(name?: string): string {
  const trimmed = (name ?? "").trim();
  if (!trimmed) return "?";
  const parts = trimmed.split(/\s+/);
  // Vietnamese names put the family name first, but people remember each other by the GIVEN
  // name — so take the initial of the LAST word: "Nguyễn Văn An" gives "A", not "N" (which
  // half the class would share).
  return parts[parts.length - 1]!.charAt(0).toUpperCase();
}
