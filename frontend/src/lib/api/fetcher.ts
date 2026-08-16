/**
 * Call the API through Next's BFF (`/api/**`), where the JWT lives in an httpOnly cookie and
 * gets attached to the Authorization header server-side (ADR-007). The browser never sees the
 * token.
 */
export async function apiFetch<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    ...init,
    headers: {
      ...(init?.body ? { "content-type": "application/json" } : {}),
      ...init?.headers,
    },
  });

  // Read the BODY as text first, then parse. Calling res.json() directly is not allowed:
  // ToActionResult turns Result.Ok() into `Ok()` — HTTP 200 with an EMPTY BODY — and
  // res.json() on an empty body throws "Unexpected end of JSON input". Checking
  // content-length does not save us either: Kestrel's empty responses do not send that header.
  //
  // This class of bug affects EVERY operation that returns Result.Ok(): changing a password,
  // deleting a chapter, drag-and-drop, publishing, marking complete, joining a class — the
  // operation genuinely succeeds but the user gets a red error. No backend test catches it,
  // because the backend really does return 200.
  const text = await res.text();

  if (!res.ok) {
    // The backend returns problem+json per RFC 9457; `detail` is a Vietnamese sentence that
    // can be shown as-is.
    let problem: { detail?: string; title?: string } | null = null;
    try {
      problem = text ? JSON.parse(text) : null;
    } catch {
      problem = null;
    }
    throw new Error(problem?.detail ?? problem?.title ?? `Lỗi ${res.status}`);
  }

  return (text ? JSON.parse(text) : undefined) as T;
}
