"use client";

import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { XemMaNguon } from "@/components/submission/xem-ma-nguon";
import type { components } from "@/lib/api/schema";

type SubmissionComparisonDto = components["schemas"]["SubmissionComparisonDto"];
type MatchedFileDto = components["schemas"]["MatchedFileDto"];

/**
 * Khoá định danh một tệp trong danh sách. Một bài nộp ZIP chỉ có MỘT `submission_file_id`
 * nhưng nhiều `entry_path`, nên riêng id tệp là trùng nhau hàng loạt — phải ghép thêm đường
 * dẫn bên trong. Dùng chỉ số mảng làm khoá thì đổi tệp đang xem sẽ khiến React tái sử dụng
 * nhầm node và khối mã cũ nằm lại trên màn hình của tệp mới.
 */
function khoaTep(f: MatchedFileDto) {
  return `${f.submission_file_id}::${f.entry_path ?? ""}`;
}

function nhanTep(f: MatchedFileDto) {
  return f.entry_path ?? f.file_name;
}

function soDoan(f: MatchedFileDto) {
  return f.ranges.length;
}

function CotMaNguon({
  tieuDe,
  submissionId,
  files,
}: {
  tieuDe: string;
  submissionId: string;
  files: MatchedFileDto[];
}) {
  const [dangChon, setDangChon] = useState<string>(files.length > 0 ? khoaTep(files[0]) : "");
  const tep = files.find((f) => khoaTep(f) === dangChon) ?? files[0];

  return (
    // min-w-0 là bắt buộc trên con của grid: thiếu nó thì khối <pre> bên trong không co lại
    // được, ô lưới phình theo dòng mã dài nhất và CẢ TRANG cuộn ngang trên điện thoại thay vì
    // chỉ khối mã cuộn.
    <section className="flex min-w-0 flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="font-medium">{tieuDe}</h2>
        {tep && <Badge variant="secondary">{soDoan(tep)} đoạn trùng</Badge>}
      </div>

      {files.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          Bài này không có tệp mã nguồn nào trùng với bài kia.
        </p>
      ) : (
        <>
          <Select value={dangChon} onValueChange={setDangChon}>
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {files.map((f) => (
                <SelectItem key={khoaTep(f)} value={khoaTep(f)}>
                  {nhanTep(f)} ({soDoan(f)})
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          {tep && (
            <XemMaNguon
              // Đổi tệp là đổi hẳn nội dung lẫn vùng tô sáng, nên ép React dựng lại component
              // thay vì tái dùng: giữ lại state cũ là khối mã của tệp trước còn nằm trên màn
              // hình trong lúc tệp mới đang tải, và giảng viên đọc nhầm bài.
              key={khoaTep(tep)}
              submissionId={submissionId}
              fileId={tep.submission_file_id}
              path={tep.entry_path}
              tenTep={tep.file_name}
              vungToSang={tep.ranges}
            />
          )}
        </>
      )}
    </section>
  );
}

export function SoSanhBaiNop({
  bc,
  tenA,
  tenB,
}: {
  bc: SubmissionComparisonDto;
  tenA: string;
  tenB: string;
}) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant="secondary">{bc.similarity_percent}% trùng</Badge>
        <span className="text-muted-foreground text-sm">
          Jaccard {bc.jaccard_percent}% · {bc.shared_fingerprints} đoạn chung
        </span>
      </div>

      <p className="text-muted-foreground text-sm">
        Hai danh sách tệp là độc lập: hệ thống chỉ ra tệp nào của mỗi bên CÓ đoạn trùng, chứ
        không ghép tệp này với tệp kia. Chọn tệp ở từng cột để đọc đối chiếu.
      </p>

      <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
        <CotMaNguon tieuDe={tenA} submissionId={bc.submission_a_id} files={bc.files_a} />
        <CotMaNguon tieuDe={tenB} submissionId={bc.submission_b_id} files={bc.files_b} />
      </div>
    </div>
  );
}
