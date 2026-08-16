import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  {
    // `react/no-danger` KHÔNG có trong bộ mặc định của eslint-config-next. Thiếu nó thì quy
    // ước XSS quan trọng nhất của dự án — "nội dung sinh viên nộp KHÔNG BAO GIỜ thành HTML" —
    // chỉ tồn tại trong tài liệu, và một `dangerouslySetInnerHTML` thêm vào sáu tháng nữa sẽ
    // lọt qua review mà không có gì kêu.
    //
    // Để mức "warn" chứ không "error": trong dự án có ĐÚNG hai chỗ dùng hợp lệ (chuỗi do Shiki
    // sinh ra từ text thô, và `locHtml` trong phan-bai-hoc.tsx), cả hai đã có chú thích
    // `eslint-disable-next-line` kèm lý do. Đặt "error" thì hai chỗ đó vẫn im, nhưng bất kỳ ai
    // thêm chỗ thứ ba sẽ bị chặn build và cám dỗ đầu tiên là rắc thêm một dòng disable nữa cho
    // qua. Cảnh báo buộc người thêm phải viết ra lý do, và lý do đó mới là thứ review được.
    rules: { "react/no-danger": "warn" },
  },
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    "out/**",
    "build/**",
    "next-env.d.ts",
  ]),
]);

export default eslintConfig;
