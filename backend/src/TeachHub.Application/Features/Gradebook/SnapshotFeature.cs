using System.Text.Encodings.Web;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Offerings;
using TeachHub.Domain.Grading;

namespace TeachHub.Application.Features.Gradebook;

// ── Hình dạng bản chốt ──────────────────────────────────────────────────────────────────

public sealed record GradeSnapshotSourceDto(
    GradeSourceType SourceType, string Title, decimal? WeightWithinColumn);

public sealed record GradeSnapshotColumnDto(
    string Key, string Name, decimal WeightPercent, int OrderIndex,
    AggregationKind Kind, int N, bool TreatMissingAsZero,
    List<GradeSnapshotSourceDto> Sources);

public sealed record GradeSnapshotCellDto(
    string ColumnKey, decimal RawScore, decimal Score, decimal Bonus, bool IsManualOverride);

public sealed record GradeSnapshotRowDto(
    Guid EnrollmentId, string StudentCode, string FullName, string? ClassName,
    List<GradeSnapshotCellDto> Cells, decimal BonusToFinal, decimal FinalScore);

/// <summary>
/// Nội dung bản chốt. Cố ý KHÔNG chứa <c>TakenAt</c> và <c>TakenBy</c> dù hai thứ đó có trên
/// entity: nhờ vậy hash là dấu vân tay của RIÊNG bảng điểm, và hai bản chốt trùng hash chứng
/// minh được rằng giữa hai lần đóng lớp không có gì đổi. Nhét thời điểm chốt vào payload thì
/// mọi bản chốt đều khác hash nhau và câu hỏi "có gì đổi không" không trả lời được nữa.
/// </summary>
public sealed record GradeSnapshotPayload(
    Guid OfferingId, string ClassCode, string SubjectCode, string SubjectName,
    BonusTarget BonusDefaultTarget, string? BonusDefaultColumnKey,
    decimal? BonusMaxPointsPerStudent,
    List<GradeSnapshotColumnDto> Columns,
    List<GradeConditionDto> Conditions,
    List<string> UnknownConditionColumnKeys,
    List<GradeSnapshotRowDto> Rows);

public static class GradeSnapshotBuilder
{
    /// <summary>
    /// Tất định TUYỆT ĐỐI. Mọi danh sách đều sắp xếp bằng <see cref="StringComparer.Ordinal"/>
    /// chứ không theo thứ tự trả về của truy vấn: thứ tự sắp xếp mặc định của Postgres phụ
    /// thuộc collation của database, nên cùng một bảng điểm trên hai máy chủ sẽ cho hai chuỗi
    /// JSON khác nhau và hai hash khác nhau — bản chốt mất giá trị đối chiếu.
    /// </summary>
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // Enum ra CHUỖI: số thứ tự đổi nghĩa ngay khi ai đó chèn một giá trị vào giữa enum,
        // và lúc đó mọi bản chốt cũ nói sai về chính nó.
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        // Không escape tiếng Việt thành \uXXXX: bản chốt còn phải đọc được bằng mắt khi sinh
        // viên khiếu nại, và JSON escape làm cả trang toàn ký tự thoát.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    public static GradeSnapshotPayload Build(GradebookDto so)
    {
        var khoaTheoId = so.Columns.ToDictionary(c => c.Id, c => c.Key);

        var cot = so.Columns
            .OrderBy(c => c.OrderIndex).ThenBy(c => c.Key, StringComparer.Ordinal)
            .Select(c => new GradeSnapshotColumnDto(
                c.Key, c.Name, c.WeightPercent, c.OrderIndex,
                c.Kind, c.N, c.TreatMissingAsZero,
                [.. c.Sources
                    .OrderBy(s => s.SourceType)
                    .ThenBy(s => s.SourceTitle, StringComparer.Ordinal)
                    .Select(s => new GradeSnapshotSourceDto(s.SourceType, s.SourceTitle, s.WeightWithinColumn))]))
            .ToList();

        var hang = so.Rows
            .OrderBy(r => r.StudentCode, StringComparer.Ordinal)
            .Select(r => new GradeSnapshotRowDto(
                r.EnrollmentId, r.StudentCode, r.FullName, r.ClassName,
                [.. r.Cells
                    // Ô trỏ tới cột đã biến mất thì bỏ qua chứ không ghi khóa rỗng: một khóa
                    // rỗng trong bản chốt trông y hệt một cột thật không có tên.
                    .Where(o => khoaTheoId.ContainsKey(o.ColumnId))
                    .OrderBy(o => khoaTheoId[o.ColumnId], StringComparer.Ordinal)
                    .Select(o => new GradeSnapshotCellDto(
                        khoaTheoId[o.ColumnId], o.RawScore, o.Score, o.Bonus, o.IsManualOverride))],
                r.BonusToFinal, r.FinalScore))
            .ToList();

        return new GradeSnapshotPayload(
            so.OfferingId, so.ClassCode, so.SubjectCode, so.SubjectName,
            so.BonusDefaultTarget,
            // Ghi KEY chứ không ghi id: id không nói lên điều gì khi đọc bản chốt sáu tháng sau,
            // và cột có thể đã bị xóa nên tra ngược id là ngõ cụt.
            so.BonusDefaultColumnId is { } id ? khoaTheoId.GetValueOrDefault(id) : null,
            so.BonusMaxPointsPerStudent,
            cot,
            [.. so.Conditions.OrderBy(d => d.ColumnKey, StringComparer.Ordinal).ThenBy(d => d.Op)],
            [.. so.UnknownConditionColumnKeys.OrderBy(k => k, StringComparer.Ordinal)],
            hang);
    }

    public static string Serialize(GradeSnapshotPayload payload) =>
        JsonSerializer.Serialize(payload, Json);
}

// ── DTO trả ra API ──────────────────────────────────────────────────────────────────────

public sealed record GradeSnapshotDto(
    Guid Id, Guid OfferingId, DateTimeOffset TakenAt, Guid TakenBy, string PayloadHash);

/// <param name="PayloadJson">
/// Chuỗi JSON nguyên văn, KHÔNG phải object lồng. Cố ý: hash được tính trên đúng chuỗi này,
/// nên đưa nó qua một vòng parse–serialize nữa ở tầng HTTP là làm người nhận không còn kiểm
/// lại được hash. Frontend tự gọi JSON.parse.
/// </param>
public sealed record GradeSnapshotDetailDto(
    Guid Id, Guid OfferingId, DateTimeOffset TakenAt, Guid TakenBy,
    string PayloadHash, string PayloadJson);

// ── Chốt sổ ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Chốt sổ điểm của một lớp. CHỈ GHI THÊM: không sửa, không xóa, không có đường ghi đè —
/// mở lại lớp rồi đóng lần nữa sinh một bản mới, bản cũ không đổi một byte (ADR-006).
///
/// CHƯA ĐƯỢC NỐI VÀO VIỆC ĐÓNG LỚP. Plan Phase 4b giả định lớp chuyển sang <c>Closed</c> thì
/// tự chốt một bản, nhưng repo hiện KHÔNG có endpoint nào đổi <c>OfferingStatus</c>,
/// nên đây tạm là một hành động tường minh do giảng viên bấm.
///
/// Ai viết endpoint đổi trạng thái lớp: gọi command này TRONG cùng lệnh đó, đừng dựng đường
/// chốt sổ thứ hai. Nó đã idempotent theo nghĩa "chỉ ghi thêm" nên gọi thừa một lần chỉ tốn
/// một hàng, còn quên gọi thì lớp đóng mà không có bản đối chiếu nào — và không có gì trong
/// hệ thống nói cho bạn biết điều đó cho tới lúc sinh viên khiếu nại điểm.
/// </summary>
public sealed record TakeGradeSnapshotCommand : ICommand<Result<GradeSnapshotDto>>
{
    public Guid OfferingId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class TakeGradeSnapshotHandler(IAppDbContext db, ISender sender, TimeProvider clock)
    : ICommandHandler<TakeGradeSnapshotCommand, Result<GradeSnapshotDto>>
{
    public async Task<Result<GradeSnapshotDto>> Handle(
        TakeGradeSnapshotCommand request, CancellationToken ct)
    {
        // Dùng LẠI GetGradebookQuery thay vì tự truy vấn và tự cộng: hai đường tính điểm là
        // hai con số khác nhau, và con số trong bản chốt phải đúng bằng con số giảng viên
        // nhìn thấy trên màn hình lúc bấm nút — đó là toàn bộ ý nghĩa của việc chốt sổ.
        // Query này cũng đã tự kiểm quyền sở hữu và trả NotFound nếu lớp không phải của người gọi.
        var so = await sender.Send(new GetGradebookQuery(request.OfferingId, request.InstructorId), ct);
        if (!so.IsSuccess) return Result<GradeSnapshotDto>.NotFound(so.Error);

        // Trọng số chưa hợp lệ thì điểm tổng đang sai. Đóng băng một bảng điểm sai thành "bản
        // chính thức" còn tệ hơn không có bản nào: về sau nó là thứ được đem ra đối chiếu.
        if (!so.Value.WeightsValid)
            return Result<GradeSnapshotDto>.Conflict(
                $"Chưa chốt sổ được. {so.Value.WeightsError}");

        var json = GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(so.Value));

        var ban = new GradeSnapshot
        {
            OfferingId = request.OfferingId,
            TakenAt = clock.GetUtcNow(),
            TakenBy = request.InstructorId,
            PayloadJson = json,
            PayloadHash = GradeSnapshotHash.Compute(json),
        };

        // Add tường minh qua DbSet. Entity.Id tự sinh UUIDv7 lúc khởi tạo, nên thêm con qua
        // navigation của một parent đang tracked làm EF phát UPDATE thay vì INSERT.
        db.GradeSnapshots.Add(ban);
        await db.SaveChangesAsync(ct);

        return Result<GradeSnapshotDto>.Ok(new GradeSnapshotDto(
            ban.Id, ban.OfferingId, ban.TakenAt, ban.TakenBy, ban.PayloadHash));
    }
}

// ── Đọc lại bản chốt ────────────────────────────────────────────────────────────────────

public sealed record GetGradeSnapshotsQuery(Guid OfferingId, Guid InstructorId)
    : IQuery<Result<List<GradeSnapshotDto>>>;

public sealed class GetGradeSnapshotsHandler(IAppDbContext db)
    : IQueryHandler<GetGradeSnapshotsQuery, Result<List<GradeSnapshotDto>>>
{
    public async Task<Result<List<GradeSnapshotDto>>> Handle(
        GetGradeSnapshotsQuery request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.InstructorId, ct))
            return Result<List<GradeSnapshotDto>>.NotFound("Không tìm thấy lớp học phần.");

        // Không nạp payload_json ở màn hình danh sách: mỗi bản chốt của lớp 300 sinh viên là
        // vài trăm KB JSON, và danh sách 6 bản kéo về vài MB chỉ để hiện sáu dòng ngày tháng.
        var ds = await db.GradeSnapshots.AsNoTracking()
            .Where(s => s.OfferingId == request.OfferingId)
            .OrderByDescending(s => s.TakenAt)
            .Select(s => new GradeSnapshotDto(s.Id, s.OfferingId, s.TakenAt, s.TakenBy, s.PayloadHash))
            .ToListAsync(ct);

        return Result<List<GradeSnapshotDto>>.Ok(ds);
    }
}

public sealed record GetGradeSnapshotQuery(Guid SnapshotId, Guid InstructorId)
    : IQuery<Result<GradeSnapshotDetailDto>>;

public sealed class GetGradeSnapshotHandler(IAppDbContext db)
    : IQueryHandler<GetGradeSnapshotQuery, Result<GradeSnapshotDetailDto>>
{
    public async Task<Result<GradeSnapshotDetailDto>> Handle(
        GetGradeSnapshotQuery request, CancellationToken ct)
    {
        // Điều kiện sở hữu nằm NGAY TRONG câu truy vấn: nạp rồi mới kiểm là vừa lộ sự tồn tại
        // của id, vừa dễ quên nhánh kiểm khi ai đó chép handler này sang chỗ khác.
        var ban = await db.GradeSnapshots.AsNoTracking()
            .Where(s => s.Id == request.SnapshotId
                        && db.CourseOfferings.Any(o => o.Id == s.OfferingId
                            && db.Subjects.Any(m => m.Id == o.SubjectId
                                && m.InstructorId == request.InstructorId)))
            .Select(s => new GradeSnapshotDetailDto(
                s.Id, s.OfferingId, s.TakenAt, s.TakenBy, s.PayloadHash, s.PayloadJson))
            .SingleOrDefaultAsync(ct);

        // Bản chốt của giảng viên khác trả 404, KHÔNG BAO GIỜ 403: 403 xác nhận id đó có thật,
        // và dò id tuần tự là đếm được đồng nghiệp đã chốt sổ bao nhiêu lớp.
        return ban is null
            ? Result<GradeSnapshotDetailDto>.NotFound("Không tìm thấy bản chốt sổ điểm.")
            : Result<GradeSnapshotDetailDto>.Ok(ban);
    }
}
