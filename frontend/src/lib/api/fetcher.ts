/**
 * Gọi API qua BFF của Next (`/api/**`), nơi JWT nằm trong cookie httpOnly và được gắn vào
 * header Authorization ở phía máy chủ (ADR-007). Trình duyệt không bao giờ thấy token.
 */
export async function apiFetch<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    ...init,
    headers: {
      ...(init?.body ? { "content-type": "application/json" } : {}),
      ...init?.headers,
    },
  });

  if (!res.ok) {
    // Backend trả problem+json theo RFC 9457; `detail` là câu tiếng Việt hiển thị được.
    const problem = await res.json().catch(() => null);
    throw new Error(problem?.detail ?? problem?.title ?? `Lỗi ${res.status}`);
  }

  if (res.status === 204 || res.headers.get("content-length") === "0") {
    return undefined as T;
  }

  return (await res.json()) as T;
}
