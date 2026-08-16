using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Offerings;
using TeachHub.Domain.Grading;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Gradebook;

/// <param name="Provisional">
/// Xem điểm tạm tính: bài chưa chấm bị LOẠI khỏi mẫu số thay vì tính 0. Chỉ đổi cách xem,
/// tuyệt đối không đổi dữ liệu — xem ghi chú trong handler.
/// </param>
public sealed record GetGradebookQuery(Guid OfferingId, Guid InstructorId, bool Provisional = false)
    : IQuery<Result<GradebookDto>>;

public sealed class GetGradebookHandler(IAppDbContext db)
    : IQueryHandler<GetGradebookQuery, Result<GradebookDto>>
{
    public async Task<Result<GradebookDto>> Handle(GetGradebookQuery request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.InstructorId, ct))
            return Result<GradebookDto>.NotFound("Không tìm thấy lớp học phần.");

        var lop = await db.CourseOfferings.AsNoTracking()
            .Where(o => o.Id == request.OfferingId)
            .Select(o => new
            {
                o.Id, o.ClassCode, o.BonusDefaultTarget, o.BonusDefaultColumnId,
                o.BonusMaxPointsPerStudent, o.GradeConditions,
                Code = db.Subjects.Where(s => s.Id == o.SubjectId).Select(s => s.Code).Single(),
                Name = db.Subjects.Where(s => s.Id == o.SubjectId).Select(s => s.Name).Single(),
            })
            .SingleAsync(ct);

        // AsNoTracking ở MỌI truy vấn chỉ-đọc, và biến thể "tạm tính" dựng OBJECT MỚI chứ không
        // sửa entity đang tracked. GradeAggregation là owned entity có setter: sửa nó rồi để
        // một SaveChangesAsync bất kỳ trong cùng scope chạy là ghi đè vĩnh viễn cách tính điểm
        // của cả lớp — không thông báo, không log, không ai lần ra nguyên nhân.
        var cot = await db.GradeColumns.AsNoTracking()
            .Include(c => c.Sources)
            .Where(c => c.OfferingId == request.OfferingId)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(ct);

        // Chế độ "tạm tính" dựng BẢN SAO của quy tắc gộp, không sửa Aggregation của cột.
        // Aggregation là owned entity có setter: sửa nó rồi để một SaveChangesAsync bất kỳ
        // trong cùng scope chạy là ghi đè vĩnh viễn cách tính điểm của cả lớp.
        var quyTac = cot.ToDictionary(c => c.Id, c => new GradeAggregation
        {
            Kind = c.Aggregation.Kind,
            N = c.Aggregation.N,
            TreatMissingAsZero = !request.Provisional && c.Aggregation.TreatMissingAsZero,
        });

        var sv = await db.Enrollments.AsNoTracking()
            .Where(e => e.OfferingId == request.OfferingId)
            .OrderBy(e => e.FullName)
            .Select(e => new { e.Id, e.StudentCode, e.FullName, e.ClassName })
            .ToListAsync(ct);

        var cotIds = cot.Select(c => c.Id).ToList();

        var oDiem = await db.GradeRecords.AsNoTracking()
            .Where(r => cotIds.Contains(r.GradeColumnId))
            .ToListAsync(ct);

        var diemCong = await db.BonusPoints.AsNoTracking()
            .Where(b => b.OfferingId == request.OfferingId)
            .ToListAsync(ct);

        // Điểm từ các nguồn: đọc thang điểm gốc CÙNG LÚC với đọc điểm. Gõ
        // new SourceScore(diem, 10m) ở đây là vô hiệu hóa toàn bộ phần chuẩn hóa, và bài
        // rubric thang 20 sẽ vào cột nguyên xi rồi bị kẹp ở 10 — sai theo hướng nới tay.
        var nguon = cot.SelectMany(c => c.Sources).ToList();
        var idBaiTap = nguon.Where(s => s.SourceType == GradeSourceType.Assignment).Select(s => s.SourceId).ToList();
        var idCuoiMon = nguon.Where(s => s.SourceType == GradeSourceType.FinalAssessment).Select(s => s.SourceId).ToList();

        var tranBaiTap = await db.Assignments.AsNoTracking()
            .Where(a => idBaiTap.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.MaxScore, ct);

        var tranCuoiMon = await db.FinalAssessments.AsNoTracking()
            .Where(f => idCuoiMon.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.MaxScore, ct);

        // Nguồn Quiz (Phase 5). QuizAttempt đã lưu sẵn NormalizedScore trên thang 10 nên
        // thang gốc ở đây là 10 — chuẩn hóa làm MỘT lần lúc chấm, không làm lại lần thứ hai.
        var idQuiz = nguon.Where(s => s.SourceType == GradeSourceType.Quiz).Select(s => s.SourceId).ToList();

        var diemQuiz = await db.QuizAttempts.AsNoTracking()
            .Where(a => idQuiz.Contains(a.QuizId) && a.NormalizedScore != null)
            .Select(a => new { a.QuizId, a.EnrollmentId, a.NormalizedScore })
            .ToListAsync(ct);

        var baiNop = await db.Submissions.AsNoTracking()
            .Where(s => s.OfferingId == request.OfferingId && s.IsCurrent && s.Score != null)
            .Select(s => new { s.AssignmentId, s.FinalAssessmentId, s.EnrollmentId, s.GroupId, s.Score })
            .ToListAsync(ct);

        // Bài nhóm: điểm của nhóm là điểm của mọi thành viên (mặc định MVP).
        var thanhVien = await db.GroupMembers.AsNoTracking()
            .Where(m => db.StudentGroups.Any(g => g.Id == m.GroupId && g.OfferingId == request.OfferingId))
            .Select(m => new { m.GroupId, m.EnrollmentId })
            .ToListAsync(ct);

        var caiDat = new GradebookSettings
        {
            DefaultTarget = lop.BonusDefaultTarget,
            DefaultColumnId = lop.BonusDefaultColumnId,
            MaxBonusPerStudent = lop.BonusMaxPointsPerStudent,
        };

        // Cast sang kiểu nullable rồi ?? [] là CẦN THIẾT dù property khai non-nullable: hàng
        // tạo TRƯỚC migration này có grade_conditions = NULL trong DB, và EF materialize cột
        // jsonb rỗng thành null chứ không thành danh sách rỗng. Lỗi chỉ xuất hiện trên
        // database đã có dữ liệu — chạy trên DB trống thì không bao giờ gặp.
        var dieuKien = (IReadOnlyList<GradeCondition>?)lop.GradeConditions ?? [];
        var keyLa = new HashSet<string>();
        var hang = new List<GradeRowDto>(sv.Count);

        foreach (var e in sv)
        {
            var soDiemCong = BonusLedger.Build(
                diemCong.Where(b => b.EnrollmentId == e.Id).Select(b => b.ToEntry()), caiDat);

            var diemThoTheoCot = new Dictionary<Guid, decimal>();
            var o = new List<GradeCellDto>(cot.Count);

            foreach (var c in cot)
            {
                var ghi = oDiem.FirstOrDefault(r => r.EnrollmentId == e.Id && r.GradeColumnId == c.Id);

                var nguonCuaCot = c.Sources.Select(s => new SourceScore(
                    DiemCuaNguon(s, e.Id),
                    s.SourceType switch
                    {
                        GradeSourceType.Assignment => tranBaiTap.GetValueOrDefault(s.SourceId, 10m),
                        GradeSourceType.FinalAssessment => tranCuoiMon.GetValueOrDefault(s.SourceId, 10m),
                        _ => 10m,                       // Quiz đã chuẩn hóa sẵn về thang 10
                    },
                    s.WeightWithinColumn)).ToList();

                var suaTay = ghi?.IsManualOverride ?? false;

                var tho = suaTay || quyTac[c.Id].Kind == AggregationKind.Manual
                    ? Math.Clamp(ghi?.Score ?? 0m, 0m, caiDat.MaxScore)
                    : GradeAggregator.Apply(quyTac[c.Id], nguonCuaCot, caiDat.MaxScore);

                diemThoTheoCot[c.Id] = tho;

                o.Add(new GradeCellDto(
                    c.Id, tho,
                    GradebookCalculator.ColumnScore(c, tho, soDiemCong, caiDat),
                    ghi?.IsManualOverride ?? false,
                    soDiemCong.ForColumn(c.Id)));
            }

            var tong = GradebookCalculator.FinalScore(cot, diemThoTheoCot, soDiemCong, dieuKien, caiDat);

            // Cảnh báo điều kiện trỏ tới cột không tồn tại: im lặng ở đây nghĩa là quy định
            // điểm sàn của môn không có hiệu lực mà không ai biết.
            foreach (var k in GradeConditionEvaluator
                         .Apply(tong, cot, o.ToDictionary(x => x.ColumnId, x => x.Score), dieuKien)
                         .UnknownColumnKeys)
                keyLa.Add(k);

            hang.Add(new GradeRowDto(
                e.Id, e.StudentCode, e.FullName, e.ClassName, o, soDiemCong.ToFinalTotal, tong));

            continue;

            decimal? DiemCuaNguon(GradeColumnSource s, Guid enrollmentId)
            {
                // Quiz: lấy lượt CAO NHẤT của sinh viên. Quiz cho làm nhiều lần chính là để
                // học lại cho tới khi nắm được; lấy lượt cuối thì một lần bấm nhầm xóa sạch
                // kết quả tốt trước đó.
                if (s.SourceType == GradeSourceType.Quiz)
                    return diemQuiz.Where(x => x.QuizId == s.SourceId && x.EnrollmentId == enrollmentId)
                                   .Select(x => x.NormalizedScore).Max();

                var nhomCuaToi = thanhVien.Where(m => m.EnrollmentId == enrollmentId)
                                          .Select(m => m.GroupId).ToList();

                var bn = baiNop.FirstOrDefault(x =>
                    (s.SourceType == GradeSourceType.Assignment
                        ? x.AssignmentId == s.SourceId
                        : x.FinalAssessmentId == s.SourceId)
                    && (x.EnrollmentId == enrollmentId
                        || (x.GroupId != null && nhomCuaToi.Contains(x.GroupId.Value))));

                return bn?.Score;
            }
        }

        var hopLe = GradebookCalculator.ValidateColumns(cot);

        return Result<GradebookDto>.Ok(new GradebookDto(
            lop.Id, lop.ClassCode, lop.Code, lop.Name,
            [.. cot.Select(ChuyenCot)],
            hang,
            [.. dieuKien.Select(d => new GradeConditionDto(d.ColumnKey, d.Op, d.Threshold, d.Action, d.ActionValue))],
            lop.BonusDefaultTarget, lop.BonusDefaultColumnId, lop.BonusMaxPointsPerStudent,
            hopLe.IsValid, hopLe.Error, [.. keyLa]));

        GradeColumnDto ChuyenCot(GradeColumn c) => new(
            c.Id, c.Key, c.Name, c.WeightPercent, c.OrderIndex,
            c.Aggregation.Kind, c.Aggregation.N, c.Aggregation.TreatMissingAsZero,
            [.. c.Sources.Select(s => new GradeColumnSourceDto(
                s.Id, s.SourceType, s.SourceId,
                s.SourceType switch
                {
                    GradeSourceType.Assignment =>
                        db.Assignments.Where(a => a.Id == s.SourceId).Select(a => a.Title).FirstOrDefault() ?? "(đã xóa)",
                    GradeSourceType.Quiz =>
                        db.Quizzes.Where(q => q.Id == s.SourceId).Select(q => q.Title).FirstOrDefault() ?? "(đã xóa)",
                    _ =>
                        db.FinalAssessments.Where(f => f.Id == s.SourceId).Select(f => f.Title).FirstOrDefault() ?? "(đã xóa)",
                },
                s.WeightWithinColumn))]);
    }
}
