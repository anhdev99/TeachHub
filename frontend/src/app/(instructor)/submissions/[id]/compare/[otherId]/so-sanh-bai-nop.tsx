"use client";

import { FileCode } from "lucide-react";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { TrongRong } from "@/components/shell/trang";
import { XemMaNguon } from "@/components/submission/xem-ma-nguon";
import { diem } from "@/lib/dinh-dang";
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
    <Card className="min-w-0 gap-4">
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-x-3 gap-y-2">
        <CardTitle className="min-w-0 truncate text-base">{tieuDe}</CardTitle>
        {tep && (
          <Badge className="bg-warn-soft text-warn border-warn/25 shrink-0 border">
            {soDoan(tep)} đoạn trùng
          </Badge>
        )}
      </CardHeader>

      <CardContent className="flex min-w-0 flex-col gap-2">
        {files.length === 0 ? (
          <TrongRong
            Icon={FileCode}
            tieuDe="Không có tệp nào trùng"
            moTa="Bài này không có tệp mã nguồn nào trùng với bài kia."
          />
        ) : (
          <>
            <Select value={dangChon} onValueChange={setDangChon}>
              <SelectTrigger className="min-h-11 w-full">
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
      </CardContent>
    </Card>
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
      <div className="border-border bg-card flex flex-wrap items-center gap-x-4 gap-y-2 rounded-lg border px-3.5 py-3">
        <span className="flex items-baseline gap-2">
          <span className="text-warn text-2xl font-semibold tabular-nums">
            {diem(bc.similarity_percent)}%
          </span>
          <span className="text-muted-foreground text-sm">giống nhau</span>
        </span>
        <span className="text-muted-foreground text-sm">
          Jaccard {diem(bc.jaccard_percent)}% · {bc.shared_fingerprints} đoạn chung
        </span>
      </div>

      <p className="text-muted-foreground text-sm text-pretty">
        Hai danh sách tệp là độc lập: hệ thống chỉ ra tệp nào của mỗi bên <em>có</em> đoạn trùng,
        chứ không ghép tệp này với tệp kia. Chọn tệp ở từng cột để đọc đối chiếu.
      </p>

      {/* Hai cột từ `lg` chứ không phải `md`: ở 768px mỗi cột chỉ còn ~360px, và một khối mã
          360px thì dòng nào cũng phải cuộn ngang — đọc đối chiếu như vậy còn tệ hơn xếp dọc. */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <CotMaNguon tieuDe={tenA} submissionId={bc.submission_a_id} files={bc.files_a} />
        <CotMaNguon tieuDe={tenB} submissionId={bc.submission_b_id} files={bc.files_b} />
      </div>
    </div>
  );
}
