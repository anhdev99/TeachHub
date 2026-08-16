import createClient from "openapi-fetch";
import type { paths } from "./schema";

/**
 * API client, with types generated from /openapi/v1.json (pnpm gen:api).
 *
 * baseUrl points at Next.js ITSELF rather than the .NET API: every request goes through the
 * BFF route handler in src/app/api/**, where the JWT lives in an httpOnly cookie and is
 * attached to the Authorization header server-side. The browser never sees the token
 * (ADR-007) — so an XSS hole does not also leak the whole class's grades.
 */
export const api = createClient<paths>({ baseUrl: "/" });
