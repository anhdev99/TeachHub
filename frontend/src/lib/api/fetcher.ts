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

  // Đọc THÂN dưới dạng text trước rồi mới parse. Không được gọi thẳng res.json():
  // ToActionResult quy đổi Result.Ok() thành `Ok()` — HTTP 200 với THÂN RỖNG — và
  // res.json() trên thân rỗng ném "Unexpected end of JSON input". Kiểm content-length
  // cũng không cứu được: phản hồi rỗng của Kestrel không kèm header đó.
  //
  // Lớp lỗi này ảnh hưởng MỌI thao tác trả Result.Ok(): đổi mật khẩu, xoá chương, kéo-thả,
  // xuất bản, đánh dấu hoàn thành, vào lớp — thao tác thành công thật nhưng người dùng
  // nhận một lỗi đỏ. Không test backend nào bắt được vì backend trả đúng 200.
  const text = await res.text();

  if (!res.ok) {
    // Backend trả problem+json theo RFC 9457; `detail` là câu tiếng Việt hiển thị được.
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
