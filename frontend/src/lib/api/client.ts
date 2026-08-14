import createClient from "openapi-fetch";
import type { paths } from "./schema";

/**
 * Client gọi API, sinh type từ /openapi/v1.json (pnpm gen:api).
 *
 * baseUrl trỏ về CHÍNH Next.js chứ không phải .NET API: mọi request đi qua BFF route
 * handler ở src/app/api/**, nơi JWT nằm trong cookie httpOnly và được gắn vào header
 * Authorization phía máy chủ. Trình duyệt không bao giờ thấy token (ADR-007) — một lỗ
 * XSS vì thế không kéo theo việc lộ toàn bộ điểm số của lớp.
 */
export const api = createClient<paths>({ baseUrl: "/" });
