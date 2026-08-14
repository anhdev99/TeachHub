---
name: teachhub-frontend
description: Dựng và sửa frontend Next.js 15 (App Router, React 19, Tailwind v4, shadcn/ui, TanStack Query) của TeachHub — cấu trúc (instructor)/(student), client gọi API sinh tự động bằng pnpm gen:api, BFF route handler giữ JWT trong cookie httpOnly (ADR-007), cập nhật lạc quan cho kéo-thả, form react-hook-form + zod với payload snake_case, render an toàn nội dung sinh viên nộp bằng Shiki, và responsive cho điện thoại. Dùng skill này khi làm bất cứ thứ gì dưới frontend/src/**, khi tạo trang/màn hình mới (6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.10), khi thêm component shadcn, khi backend đổi DTO và cần sinh lại client TS, khi kéo-thả bị giật hoặc nhảy thứ tự, khi form gửi sai khoá JSON, và cả khi "chạy lại", "sửa", "bổ sung", "cập nhật", "làm tiếp", "review" một màn hình đã có.
---

# Frontend TeachHub — Next.js 15

Phạm vi: mọi thứ trong `frontend/`. Stack đã chốt ở ADR (bảng §2 tài liệu kiến trúc):
**Next.js 15 App Router · React 19 · TypeScript · Tailwind v4 · shadcn/ui · TanStack Query ·
openapi-fetch + openapi-typescript · react-hook-form + zod · dnd-kit**.
Không thay thư viện nào trong danh sách này bằng lựa chọn khác mà không sửa ADR trước.

Backend chạy ở `http://localhost:5080`, frontend ở `http://localhost:3000`.
Hợp đồng API là **snake_case** (§5 tài liệu kiến trúc), lỗi luôn là ProblemDetails RFC 9457.

---

## 1. Luật số một cũng áp cho frontend: khung do CÔNG CỤ SINH

| Việc | ✅ Phải chạy | ❌ Cấm |
|---|---|---|
| Tạo dự án | `pnpm create next-app@latest frontend --ts --tailwind --app --src-dir --eslint --import-alias "@/*" --no-turbopack` | Dựng tay cấu trúc Next.js |
| Khởi tạo shadcn | `pnpm dlx shadcn@latest init` | Gõ tay `components.json`, `lib/utils.ts` |
| Thêm component UI | `pnpm dlx shadcn@latest add <tên>` | Chép tay mã component từ trí nhớ |
| Client gọi API | `pnpm gen:api` | Gõ tay type hoặc hàm fetch |
| Thư viện khác | `pnpm add <gói>` | Sửa tay `package.json` rồi mong nó khớp |

Hậu quả nếu gõ tay: mã shadcn chép từ trí nhớ lệch với bản Radix/Tailwind v4 đang cài — component
biên dịch được nhưng vỡ layout, sai token màu, mất hành vi accessibility (focus trap, aria) mà
không có lỗi nào chỉ ra nguyên nhân. Type API gõ tay thì tệ hơn: xem mục 3.

**Không chạy được lệnh → BÁO LẠI, không đoán rồi gõ tay thay thế.**

---

## 2. Cấu trúc thư mục

```
frontend/src/
├─ app/
│  ├─ (instructor)/            # route group vai giảng viên — KHÔNG xuất hiện trong URL
│  │  ├─ layout.tsx            #   sidebar + topbar của giảng viên
│  │  ├─ dashboard/page.tsx
│  │  ├─ subjects/[id]/versions/page.tsx                  # màn hình 6.10
│  │  ├─ subjects/[id]/versions/[versionId]/page.tsx      # màn hình 6.2
│  │  ├─ assignments/[id]/edit/page.tsx                   # màn hình 6.4
│  │  └─ grading/[submissionId]/page.tsx                  # màn hình 6.7
│  ├─ (student)/               # route group vai sinh viên
│  │  ├─ layout.tsx            #   layout tối giản, ưu tiên điện thoại
│  │  ├─ learn/[offeringId]/page.tsx                      # màn hình 6.3
│  │  └─ assignments/[id]/submit/page.tsx                 # màn hình 6.6
│  ├─ api/                     # BFF — xem mục 5
│  │  ├─ auth/login/route.ts
│  │  └─ [...path]/route.ts
│  └─ login/page.tsx
├─ components/
│  ├─ ui/                      # CHỈ do `shadcn add` sinh — không sửa tay trừ khi có lý do ghi lại
│  ├─ content-viewer/          # PDF / video / ảnh — dùng chung giảng viên lẫn sinh viên
│  └─ <miền>/                  # chapter-editor/, gradebook/, submission/ …
└─ lib/
   ├─ api/schema.d.ts          # SINH RA, không sửa tay, không commit sửa đổi
   ├─ api/client.ts            # openapi-fetch cho Client Component
   ├─ api/server.ts            # openapi-fetch cho Server Component
   └─ query/                   # QueryClient, query key
```

Quy tắc đặt component:

- Dùng ở **một** trang → đặt trong thư mục của trang đó (`app/(instructor)/grading/[submissionId]/_components/`).
- Dùng ở **hai trang trở lên** → nâng lên `src/components/<miền>/`.
- Nguyên thuỷ shadcn → luôn ở `src/components/ui/`, luôn do CLI sinh.

Không nhân bản component vì "trang kia hơi khác". `BonusPointPopover` là ví dụ đã ghi trong plan
Phase 4a: viết tạm một bản riêng cho màn hình chấm thì ô điểm cộng ở màn chấm và ở sổ điểm hiển thị
lệch nhau, và người dùng mất niềm tin vào con số.

Server Component là mặc định. Chỉ thêm `"use client"` khi component thật sự cần state, event,
hoặc hook của TanStack Query. Đánh `"use client"` lên page rồi kéo cả cây xuống client là cách
nhanh nhất làm trang học phình bundle trên điện thoại của sinh viên.

---

## 3. Tầng gọi API: CHỈ dùng client sinh tự động

`frontend/package.json`:

```json
"scripts": {
  "gen:api": "openapi-typescript http://localhost:5080/openapi/v1.json -o src/lib/api/schema.d.ts"
}
```

Chạy `pnpm gen:api` **mỗi lần backend đổi DTO, thêm endpoint, đổi tên trường**. API phải đang chạy.

Đây là lợi ích trực tiếp của OpenAPI 3.1 mà backend .NET 10 sinh sẵn: tầng gọi API không phải viết
tay và không bao giờ lệch với backend. Đổi một DTO ở C#, chạy lại `pnpm gen:api`, **TypeScript báo
lỗi ngay tại chỗ dùng sai** — trước khi ai đó mở trình duyệt.

### Cấm tuyệt đối

```ts
// ❌ SAI — tự khai type theo trí nhớ
type Lesson = { id: string; title: string; orderIndex: number };
const res = await fetch("/api/lessons").then(r => r.json() as Lesson[]);
```

```ts
// ✅ ĐÚNG — type lấy từ schema sinh ra, path được TypeScript kiểm
import type { paths, components } from "@/lib/api/schema";
type Lesson = components["schemas"]["LessonDto"];
const { data, error } = await api.GET("/api/lessons", { params: { query: { chapter_id } } });
```

Hậu quả của bản SAI: `orderIndex` không tồn tại — API trả `order_index`. Code biên dịch sạch, chạy
ra `undefined`, danh sách bài học hiển thị đúng thứ tự **do may mắn** (thứ tự trả về từ server),
rồi vỡ ngay lần đầu người dùng kéo-thả. Không có test nào bắt được vì type đã nói dối.

### Hai client, hai chỗ dùng

```ts
// src/lib/api/client.ts — cho Client Component. baseUrl rỗng ⇒ đi qua BFF cùng origin,
// cookie httpOnly tự đính kèm, JS không bao giờ chạm token.
import createClient from "openapi-fetch";
import type { paths } from "./schema";
export const api = createClient<paths>({ baseUrl: "" });
```

```ts
// src/lib/api/server.ts — cho Server Component. Gọi thẳng .NET API, tự gắn Authorization
// từ cookie đọc bằng next/headers. KHÔNG dùng URL tương đối trong Server Component:
// fetch phía Node không có origin, "/api/..." sẽ ném lỗi lúc chạy chứ không phải lúc build.
import createClient from "openapi-fetch";
import { cookies } from "next/headers";
import type { paths } from "./schema";

export async function serverApi() {
  const at = (await cookies()).get("th_at")?.value;
  return createClient<paths>({
    baseUrl: process.env.API_BASE_URL ?? "http://localhost:5080",
    headers: at ? { Authorization: `Bearer ${at}` } : {},
  });
}
```

### Khi `pnpm gen:api` chết

Không được gõ tay type để đi tiếp. `/openapi/v1.json` trả 500 gần như luôn là lỗi **backend**, và
có một nguyên nhân đã trả giá: record command dùng tham số positional kiểu giá trị có giá trị mặc
định (`Guid InstructorId = default`) làm JsonSchemaExporter ném JsonException. Sửa backend
(chuyển sang property `init`-only) rồi sinh lại. Gõ tay type để "tạm chạy" nghĩa là frontend build
xanh trên một hợp đồng không tồn tại.

Tương tự, nếu client sinh ra có khoá **camelCase**: backend thiếu `ConfigureHttpJsonOptions(...)`
bên cạnh `AddControllers().AddJsonOptions(...)`. Hai chỗ đọc hai IOptions khác nhau. Đừng "sửa" ở
frontend — runtime vẫn trả snake_case, và bạn sẽ dựng cả một tầng ánh xạ cho một cấu hình thiếu.

---

## 4. shadcn/ui

```bash
pnpm dlx shadcn@latest add button input label card form sonner table dialog dropdown-menu
pnpm dlx shadcn@latest add badge tabs sheet skeleton tooltip popover   # thêm khi cần
```

- Cần component mới → chạy `add`, **không** chép mã từ trí nhớ hay từ blog.
- File trong `components/ui/` sinh ra là để **được sửa**, nhưng chỉ sửa sau khi CLI đã tạo, và ghi
  chú lý do ngay trên chỗ sửa. Sửa mà không ghi thì lần `add` sau ghi đè, mất im lặng.
- Biến thể riêng của dự án (ví dụ nút "Nộp chính thức" có trạng thái khoá) → bọc ngoài
  (`components/submission/SubmitButton.tsx`), không sửa `ui/button.tsx`.
- Màu/spacing lấy từ token Tailwind v4 của theme shadcn. Gõ mã màu hex thẳng vào className là chỗ
  sinh ra giao diện sáng/tối lệch nhau.

---

## 5. BFF route handler + JWT trong cookie httpOnly (ADR-007)

**Quyết định**: Next.js Route Handler proxy tới .NET API; access token + refresh token nằm trong
cookie `httpOnly; secure; sameSite=lax`, **không bao giờ chạm tới JavaScript phía client**.

**Lý do** (nguyên văn ADR-007): lưu JWT trong localStorage là con đường ngắn nhất từ một lỗ XSS tới
việc lộ toàn bộ điểm số lớp. Đổi lại phải thêm một hop mạng — chấp nhận được với quy mô một giảng viên.

```ts
// src/app/api/auth/login/route.ts
const { access_token, refresh_token, expires_in } = await res.json();
const jar = await cookies();

jar.set("th_at", access_token, {
  httpOnly: true, secure: process.env.NODE_ENV === "production",
  sameSite: "lax", path: "/", maxAge: expires_in,
});
jar.set("th_rt", refresh_token, {
  httpOnly: true, secure: process.env.NODE_ENV === "production",
  sameSite: "lax", path: "/", maxAge: 60 * 60 * 24 * 14,
});
return NextResponse.json({ ok: true });   // KHÔNG trả token về cho client
```

`src/app/api/[...path]/route.ts` — đọc cookie `th_at`, gắn header `Authorization`, chuyển tiếp tới
.NET API; nhận 401 thì tự gọi refresh **một lần** rồi thử lại, thất bại thì trả 401 để client
chuyển sang `/login`.

### Bốn điều cấm, kèm hậu quả

1. **Không** `localStorage` / `sessionStorage` / cookie thường cho token. Một lỗ XSS bất kỳ
   (một link sinh viên nộp, một tên file, một thư viện npm) đọc được token là đọc được toàn bộ
   điểm của lớp, và refresh token còn cho phép duy trì truy cập sau khi đổi mật khẩu.
2. **Không** `NEXT_PUBLIC_` cho bất cứ thứ gì là bí mật. Tiền tố này nhúng giá trị thẳng vào bundle
   trình duyệt — `NEXT_PUBLIC_API_KEY` là công bố công khai, không phải cấu hình.
3. **Không** trả token về body cho client "cho tiện". Một lần trả là mọi lý do của ADR-007 mất sạch.
4. **Không** bỏ `secure` ở production hay đổi sang `sameSite: "none"` mà không sửa ADR. `lax` là
   mức chặn CSRF cơ bản đang được dựa vào; đổi nó là mở lại lớp tấn công vừa đóng.

Retry refresh phải **có giới hạn một lần**. Vòng lặp 401 → refresh → 401 sẽ nhân đôi tải lên API
và làm trang treo im lặng thay vì đưa người dùng về trang đăng nhập.

---

## 6. TanStack Query — cập nhật lạc quan cho kéo-thả

Kéo-thả mà phải chờ round-trip mạng mới thấy thứ tự đổi sẽ khiến việc soạn một chương 20 bài trở
nên khó chịu tới mức bạn sẽ ngừng dùng. Đây là lý do bắt buộc dùng cập nhật lạc quan ở **mọi** thao
tác sắp xếp: kéo-thả bài học và component (màn hình 6.2), kéo-thả slot bài tập (6.4), kéo-thả chia
nhóm (6.5).

```tsx
const qc = useQueryClient();
const key = ["lessons", chapterId] as const;

const reorder = useMutation({
  // đường dẫn phải chọn từ gợi ý của `paths` trong schema.d.ts — TypeScript chỉ nhận path có thật
  mutationFn: (payload: ReorderPayload) => reorderLessons(payload),

  onMutate: async (payload) => {
    await qc.cancelQueries({ queryKey: key });      // chặn refetch đang bay ghi đè bản lạc quan
    const previous = qc.getQueryData<Lesson[]>(key);
    qc.setQueryData<Lesson[]>(key, (old) => applyOrder(old ?? [], payload));
    return { previous };                            // context để hoàn tác
  },

  onError: (_err, _payload, ctx) => {
    if (ctx?.previous) qc.setQueryData(key, ctx.previous);   // hoàn tác đúng bản trước đó
    toast.error("Không lưu được thứ tự mới. Đã khôi phục thứ tự cũ.");
  },

  onSettled: () => qc.invalidateQueries({ queryKey: key }),  // server là nguồn chân lý
});
```

Bốn chỗ sai và hậu quả cụ thể:

- **Quên `cancelQueries`**: một refetch đang bay trả về dữ liệu cũ và ghi đè bản lạc quan — người
  dùng thấy hàng vừa kéo nhảy ngược về chỗ cũ rồi lại nhảy tới. Không tái hiện được trên máy nhanh.
- **Quên `onError` hoàn tác**: API lỗi mà UI vẫn giữ thứ tự mới. Người dùng tin là đã lưu, tải lại
  trang thấy thứ tự cũ, và không hiểu mình mất gì.
- **Quên `onSettled` invalidate**: `order_index` do server chuẩn hoá lại (Reindex về 0..n-1) không
  về được client; lần kéo tiếp theo tính trên chỉ số sai.
- **Dùng index của mảng làm `key` React hoặc id dnd-kit**: kéo xong React tái sử dụng nhầm DOM
  node, ô đang mở accordion nhảy sang bài khác. Luôn dùng id thật (uuid).

Query key đặt theo tài nguyên + tham số: `["lessons", chapterId]`, `["gradebook", offeringId]`.
Đặt key trùng nhau giữa hai lớp học phần khác nhau là hai lớp thấy điểm của nhau trong cache.

---

## 7. Form: react-hook-form + zod, payload snake_case

Schema zod khai **đúng khoá snake_case của hợp đồng API**, và dùng luôn khoá đó làm tên field.
Không có tầng ánh xạ camelCase ↔ snake_case ở giữa.

```ts
// ✅ ĐÚNG
const schema = z.object({
  name: z.string().min(1, "Tên cột không được để trống"),
  weight_percent: z.coerce.number().min(0).max(100),
  order_index: z.coerce.number().int().min(0),
});
type FormValues = z.infer<typeof schema>;

const form = useForm<FormValues>({ resolver: zodResolver(schema) });
// <FormField name="weight_percent" … />  → onSubmit gửi thẳng values, không map lại
```

```ts
// ❌ SAI — form camelCase rồi map tay
const payload = { name: v.name, weightPercent: v.weightPercent };
```

Hậu quả của bản SAI: server bỏ qua khoá lạ và nhận `weight_percent = 0`. API trả **200**, cột điểm
được tạo với trọng số 0, và lỗi chỉ lộ ra lúc tính điểm cuối kỳ. Thêm một trường mới ở C# thì bộ
ánh xạ tay im lặng bỏ sót nó — đúng loại lỗi mà client sinh tự động sinh ra để loại bỏ.

Quy tắc thêm:

- Thông báo lỗi trong zod viết **tiếng Việt**, nói rõ phải làm gì ("Tổng trọng số phải bằng 100%"),
  không phải "Invalid input".
- Kiểm tra phía client là để trải nghiệm tốt, **không** thay cho kiểm tra phía server. Với upload
  (màn hình 6.6) phải có **cả hai**: client chặn sai định dạng/quá dung lượng trước khi tốn băng
  thông của sinh viên, server mới là thứ bảo vệ hệ thống.
- Lỗi từ API là ProblemDetails RFC 9457. Đọc `detail` để hiện lên `toast`, và ánh xạ lỗi 400 theo
  trường về `form.setError(<tên trường snake_case>, …)`. Hiện "Đã có lỗi xảy ra" cho một lỗi
  validation có sẵn câu tiếng Việt là vứt đi thông tin backend đã cất công trả về.
- Upload đi bằng `FormData`, tên field **snake_case** (`slot_key`) — model binder form của
  ASP.NET Core không theo JsonNamingPolicy, gửi `slotKey` là server nhận `null`.

---

## 8. An toàn khi render: nội dung sinh viên nộp KHÔNG BAO GIỜ thành HTML

Rủi ro đã ghi trong plan Phase 4a: mở một bài nộp làm chạy script trên phiên của giảng viên — tức
là trên phiên có quyền sửa điểm cả lớp.

### Mã nguồn: tô màu bằng Shiki, render phía server

Shiki nhận vào **chuỗi text** và sinh ra **HTML đã thoát ký tự**, không thực thi nội dung đầu vào.

```tsx
// ✅ ĐÚNG — Server Component, không cần dangerouslySetInnerHTML
import { codeToHast } from "shiki";
import { toJsxRuntime } from "hast-util-to-jsx-runtime";
import { Fragment, jsx, jsxs } from "react/jsx-runtime";

export async function CodeView({ source, lang }: { source: string; lang: string }) {
  const hast = await codeToHast(source, { lang, theme: "github-dark" });
  return toJsxRuntime(hast, { Fragment, jsx, jsxs });
}
```

```tsx
// ❌ SAI — mọi biến thể dưới đây đều là lỗ XSS
<div dangerouslySetInnerHTML={{ __html: submission.content }} />
<div dangerouslySetInnerHTML={{ __html: markdownToHtml(studentText) }} />
<iframe srcDoc={studentHtmlFile} />
```

Ngoại lệ **duy nhất** được phép dùng `dangerouslySetInnerHTML`: chuỗi do chính Shiki `codeToHtml`
sinh ra từ một đầu vào text thô, gọi ở phía server. Không có ngoại lệ thứ hai — kể cả khi backend
trả về một trường tên `html`.

Chuỗi thường (tên file trong ZIP, tên nhóm, nhận xét, tên sinh viên) render qua text node của React
là đã được thoát tự động — cứ để React làm.

### Link và video: chỉ từ danh sách nguồn cho phép

Cho nhúng iframe tuỳ ý nghĩa là bất kỳ ai sửa được nội dung bài học đều có thể nhúng trang lừa đảo
vào trang học của sinh viên. Danh sách chốt ở Phase 2:

```
youtube.com · www.youtube.com · youtu.be · player.vimeo.com · vimeo.com
```

- Server đã kiểm khi lưu; **client vẫn phải kiểm lại trước khi render** — dữ liệu cũ có thể được
  nhập trước khi luật này tồn tại.
- Kiểm bằng `new URL(u).hostname` so khớp **chính xác** với danh sách. Đừng dùng
  `u.includes("youtube.com")`: `https://youtube.com.evil.tld/x` khớp ngay.
- Bắt buộc `protocol === "https:"`. URL do người dùng nhập gắn vào `href`/`src` mà không lọc scheme
  thì `javascript:...` chạy khi bấm — vẫn là XSS, chỉ đổi đường vào.
- `<iframe>` luôn kèm `sandbox` và `referrerPolicy="no-referrer"`.
- Video sinh viên tải lên: `<video>` gốc + presigned URL, không phải iframe.

---

## 9. Responsive là bắt buộc, không phải "làm sau"

SRS mục 9: giao diện sinh viên tối giản, **mobile-responsive vì SV hay xem trên điện thoại**.

- Mọi trang trong `(student)` phải đọc được ở bề rộng **360px** mà **không cuộn ngang**.
- Sidebar trang học thu vào drawer (`Sheet` của shadcn) dưới `768px`.
- Nội dung PPT/PDF luôn vừa khung (`max-width: 100%`), không tràn.
- Bảng rộng (sổ điểm, danh sách nộp bài) đặt trong khối `overflow-x: auto` riêng — **khối đó** cuộn,
  không phải cả trang. Sổ điểm còn phải ghim cột MSSV + Họ tên khi cuộn ngang, nếu không thì cuộn
  tới cột "Cuối môn" là không còn biết đang nhập điểm cho ai.
- Viết mobile-first: class mặc định cho điện thoại, `md:`/`lg:` mới mở rộng. Làm ngược lại thì
  bản điện thoại luôn là bản bị bỏ quên.
- Vùng bấm ≥ 44px. Kéo-thả trên cảm ứng cần `TouchSensor` của dnd-kit kèm `activationConstraint`,
  nếu không thì thao tác cuộn trang bị hiểu nhầm thành kéo item.

Nghiệm thu Phase 2 có một bước làm tay: mở trang học trên điện thoại → đọc được, không phải cuộn
ngang. Chưa qua bước đó thì task chưa xong.

---

## 10. Quyền và trạng thái trên giao diện

- Version không phải `draft`: hiện banner "Phiên bản này đã xuất bản — chỉ xem", **tất cả** control
  sửa ở trạng thái `disabled`, kèm nút "Tạo phiên bản mới từ đây". Đây là chỗ ngăn giảng viên vô ý
  sửa nội dung của lớp đang chạy — mà lớp đang chạy đã có bài nộp chấm theo nội dung đó.
- Backend trả **404 chứ không bao giờ 403** cho tài nguyên không thuộc người gọi (403 xác nhận id
  có tồn tại). Vì vậy UI hiện "Không tìm thấy nội dung này", **không** hiện "Bạn không có quyền" —
  viết "không có quyền" là tự tay làm rò rỉ đúng thứ backend vừa che.
- Ẩn nút theo vai chỉ là trang trí. Mọi luật thật nằm ở server; đừng bao giờ dựa vào việc ẩn nút để
  bảo vệ dữ liệu.
- Vai trợ giảng **tắt hoàn toàn ở MVP** (ADR-013). Không dựng UI, không dựng nhánh điều kiện cho nó.

---

## 11. Checklist trước khi tick một task frontend

- [ ] Mọi type/hàm gọi API đến từ `schema.d.ts` sinh bằng `pnpm gen:api` — không có type gõ tay, không `any`.
- [ ] Component shadcn mới đều do `pnpm dlx shadcn@latest add` sinh.
- [ ] Không có token nào ngoài cookie httpOnly; không `NEXT_PUBLIC_` cho bí mật.
- [ ] Mọi mutation sắp xếp có `onMutate` + `onError` hoàn tác + `onSettled` invalidate.
- [ ] Payload gửi lên là snake_case, khớp `schema.d.ts`.
- [ ] Không có `dangerouslySetInnerHTML` nào ngoài output của Shiki.
- [ ] URL video được kiểm hostname chính xác + https trước khi render.
- [ ] Mở ở 360px: đọc được, không cuộn ngang.
- [ ] `pnpm build` sạch, `pnpm lint` sạch.
- [ ] Thông báo lỗi và nhãn hiển thị bằng tiếng Việt; tên biến/hàm/commit bằng tiếng Anh.
- [ ] Commit theo Conventional Commits (`feat(web): …`).

DoD của roadmap có một mục hay bị bỏ: **nếu task chạm API thì phải sinh lại được OpenAPI doc, sinh
lại được client TS, và frontend vẫn build**. Backend xanh mà frontend không build lại được thì task
chưa xong.

---

## 12. Khi quay lại sửa màn hình đã có

Đọc code hiện có trước, **cải thiện tại chỗ, không viết lại từ đầu**. Thứ tự kiểm:

1. `pnpm gen:api` đã chạy sau thay đổi backend gần nhất chưa? Lỗi type xuất hiện sau khi sinh lại
   chính là danh sách việc cần sửa — sửa hết, không `// @ts-expect-error` cho qua.
2. Component sắp viết đã có sẵn ở `src/components/` chưa? Có thì dùng lại và mở rộng.
3. Mutation mới có phải loại sắp xếp/tương tác tức thì không? Nếu có thì phải có cập nhật lạc quan.
4. Chỗ nào mới render dữ liệu do người dùng nhập hoặc sinh viên nộp? Chạy lại mục 8.
5. Layout mới có bảng, có sidebar không? Chạy lại mục 9 ở 360px.
