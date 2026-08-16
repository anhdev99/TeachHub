using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Grading;

namespace TeachHub.Application.Features.Gradebook;

// ── Bộ cột mẫu ──────────────────────────────────────────────────────────────────────────

public sealed record GetSchemeTemplatesQuery(Guid InstructorId) : IQuery<List<GradeSchemeTemplateDto>>;

public sealed class GetSchemeTemplatesHandler(IAppDbContext db)
    : IQueryHandler<GetSchemeTemplatesQuery, List<GradeSchemeTemplateDto>>
{
    public async Task<List<GradeSchemeTemplateDto>> Handle(
        GetSchemeTemplatesQuery request, CancellationToken ct)
    {
        var ds = await db.GradeSchemeTemplates.AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.InstructorId == request.InstructorId)
            .OrderBy(t => t.OrderIndex)
            .ToListAsync(ct);

        return [.. ds.Select(t => new GradeSchemeTemplateDto(
            t.Id, t.Name, t.IsDefault, t.OrderIndex,
            [.. t.Items.OrderBy(i => i.OrderIndex).Select(i => new GradeSchemeTemplateItemDto(
                i.Key, i.Name, i.WeightPercent, i.OrderIndex,
                i.Aggregation.Kind, i.Aggregation.N, i.Aggregation.TreatMissingAsZero))]))];
    }
}

public sealed record SaveGradeSchemeTemplateCommand(
    Guid? Id, string Name, bool IsDefault, List<GradeSchemeTemplateItemDto> Items)
    : ICommand<Result<GradeSchemeTemplateDto>>
{
    public Guid InstructorId { get; init; }
}

public sealed class SaveGradeSchemeTemplateValidator : AbstractValidator<SaveGradeSchemeTemplateCommand>
{
    public SaveGradeSchemeTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Chưa đặt tên bộ cột.").MaximumLength(200);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Bộ cột phải có ít nhất một cột.");

        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(y => y.Key).NotEmpty().Matches("^[a-z0-9_]+$")
                .WithMessage("Khóa cột chỉ gồm chữ thường không dấu, số và gạch dưới.");
            i.RuleFor(y => y.Name).NotEmpty().MaximumLength(200);
        });

        RuleFor(x => x.Items).Must(i => i.Sum(y => y.WeightPercent) == 100m)
            .WithMessage(x => $"Tổng trọng số phải bằng 100%, hiện tại là {x.Items.Sum(y => y.WeightPercent)}%.");

        RuleFor(x => x.Items).Must(i => i.Select(y => y.Key).Distinct().Count() == i.Count)
            .WithMessage("Có hai cột trùng khóa.");
    }
}

public sealed class SaveGradeSchemeTemplateHandler(IAppDbContext db)
    : ICommandHandler<SaveGradeSchemeTemplateCommand, Result<GradeSchemeTemplateDto>>
{
    public async Task<Result<GradeSchemeTemplateDto>> Handle(
        SaveGradeSchemeTemplateCommand request, CancellationToken ct)
    {
        GradeSchemeTemplate mau;

        if (request.Id is { } id)
        {
            var tim = await db.GradeSchemeTemplates.Include(t => t.Items)
                .SingleOrDefaultAsync(t => t.Id == id && t.InstructorId == request.InstructorId, ct);

            if (tim is null) return Result<GradeSchemeTemplateDto>.NotFound("Không tìm thấy bộ cột mẫu.");

            mau = tim;

            // Remove/Add TƯỜNG MINH qua DbSet: Entity.Id tự sinh UUIDv7 lúc khởi tạo, nên
            // thêm con qua navigation của parent đang tracked làm EF tưởng hàng đã tồn tại
            // và phát UPDATE thay vì INSERT.
            db.GradeSchemeTemplateItems.RemoveRange(mau.Items);
            mau.Items.Clear();
        }
        else
        {
            if (await db.GradeSchemeTemplates.AnyAsync(
                    t => t.InstructorId == request.InstructorId && t.Name == request.Name.Trim(), ct))
                return Result<GradeSchemeTemplateDto>.Conflict($"Đã có bộ cột tên “{request.Name.Trim()}”.");

            mau = new GradeSchemeTemplate
            {
                InstructorId = request.InstructorId,
                Name = request.Name.Trim(),
            };

            db.GradeSchemeTemplates.Add(mau);
        }

        mau.Name = request.Name.Trim();
        mau.IsDefault = request.IsDefault;

        var muc = request.Items.Select(i => new GradeSchemeTemplateItem
        {
            TemplateId = mau.Id,
            Key = i.Key, Name = i.Name,
            WeightPercent = i.WeightPercent, OrderIndex = i.OrderIndex,
            Aggregation = new GradeAggregation
            {
                Kind = i.Kind, N = i.N, TreatMissingAsZero = i.TreatMissingAsZero,
            },
        }).ToList();

        if (request.Id is null) mau.Items.AddRange(muc);   // parent cũng mới ⇒ cả đồ thị Added
        else db.GradeSchemeTemplateItems.AddRange(muc);

        await db.SaveChangesAsync(ct);

        return Result<GradeSchemeTemplateDto>.Ok(new GradeSchemeTemplateDto(
            mau.Id, mau.Name, mau.IsDefault, mau.OrderIndex,
            [.. mau.Items.OrderBy(i => i.OrderIndex).Select(i => new GradeSchemeTemplateItemDto(
                i.Key, i.Name, i.WeightPercent, i.OrderIndex,
                i.Aggregation.Kind, i.Aggregation.N, i.Aggregation.TreatMissingAsZero))]));
    }
}

// ── Mẫu xuất Excel ──────────────────────────────────────────────────────────────────────

public sealed record GetExportTemplatesQuery(Guid InstructorId) : IQuery<List<GradeExportTemplateDto>>;

public sealed class GetExportTemplatesHandler(IAppDbContext db)
    : IQueryHandler<GetExportTemplatesQuery, List<GradeExportTemplateDto>>
{
    public async Task<List<GradeExportTemplateDto>> Handle(
        GetExportTemplatesQuery request, CancellationToken ct)
    {
        var ds = await db.GradeExportTemplates.AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.InstructorId == request.InstructorId)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return [.. ds.Select(t => new GradeExportTemplateDto(
            t.Id, t.Name, t.IsDefault,
            [.. t.Items.OrderBy(i => i.OrderIndex).Select(i => new GradeExportItemDto(
                i.Source, i.ColumnKey, i.Header, i.OrderIndex, i.DecimalPlaces))]))];
    }
}

public sealed record SaveGradeExportTemplateCommand(
    Guid? Id, string Name, bool IsDefault, List<GradeExportItemDto> Items)
    : ICommand<Result<GradeExportTemplateDto>>
{
    public Guid InstructorId { get; init; }
}

public sealed class SaveGradeExportTemplateValidator : AbstractValidator<SaveGradeExportTemplateCommand>
{
    public SaveGradeExportTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Chưa đặt tên mẫu xuất.").MaximumLength(200);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Mẫu xuất phải có ít nhất một cột.");

        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(y => y.Header).NotEmpty().MaximumLength(200);
            i.RuleFor(y => y.DecimalPlaces).InclusiveBetween(0, 3);
            i.RuleFor(y => y.ColumnKey).NotEmpty()
                .When(y => y.Source == GradeExportSource.GradeColumn)
                .WithMessage("Cột lấy từ sổ điểm thì phải chọn khóa cột.");
        });
    }
}

public sealed class SaveGradeExportTemplateHandler(IAppDbContext db)
    : ICommandHandler<SaveGradeExportTemplateCommand, Result<GradeExportTemplateDto>>
{
    public async Task<Result<GradeExportTemplateDto>> Handle(
        SaveGradeExportTemplateCommand request, CancellationToken ct)
    {
        GradeExportTemplate mau;

        if (request.Id is { } id)
        {
            var tim = await db.GradeExportTemplates.Include(t => t.Items)
                .SingleOrDefaultAsync(t => t.Id == id && t.InstructorId == request.InstructorId, ct);

            if (tim is null) return Result<GradeExportTemplateDto>.NotFound("Không tìm thấy mẫu xuất.");

            mau = tim;
            db.GradeExportItems.RemoveRange(mau.Items);
            mau.Items.Clear();
        }
        else
        {
            if (await db.GradeExportTemplates.AnyAsync(
                    t => t.InstructorId == request.InstructorId && t.Name == request.Name.Trim(), ct))
                return Result<GradeExportTemplateDto>.Conflict($"Đã có mẫu xuất tên “{request.Name.Trim()}”.");

            mau = new GradeExportTemplate
            {
                InstructorId = request.InstructorId,
                Name = request.Name.Trim(),
            };

            db.GradeExportTemplates.Add(mau);
        }

        mau.Name = request.Name.Trim();
        mau.IsDefault = request.IsDefault;

        var muc = request.Items.Select(i => new GradeExportItem
        {
            TemplateId = mau.Id,
            Source = i.Source,
            // Nối bằng KEY chứ không bằng id: mẫu thuộc giảng viên, cột thuộc lớp.
            ColumnKey = i.ColumnKey,
            Header = i.Header,
            OrderIndex = i.OrderIndex,
            DecimalPlaces = i.DecimalPlaces,
        }).ToList();

        if (request.Id is null) mau.Items.AddRange(muc);
        else db.GradeExportItems.AddRange(muc);

        await db.SaveChangesAsync(ct);

        return Result<GradeExportTemplateDto>.Ok(new GradeExportTemplateDto(
            mau.Id, mau.Name, mau.IsDefault,
            [.. mau.Items.OrderBy(i => i.OrderIndex).Select(i => new GradeExportItemDto(
                i.Source, i.ColumnKey, i.Header, i.OrderIndex, i.DecimalPlaces))]));
    }
}

// ── Xuất file ───────────────────────────────────────────────────────────────────────────

public sealed record ExportedFile(byte[] Content, string FileName, string ContentType);

/// <summary>
/// Định dạng file xuất. Mặc định <c>Xlsx</c> — đó là thứ phòng đào tạo nhận. Đường CSV giữ
/// lại vì nó đang chạy được và không phụ thuộc thư viện nào: khi ClosedXML gặp sự cố với một
/// bảng điểm cụ thể, giảng viên vẫn còn một cách lấy dữ liệu ra thay vì bị chặn hoàn toàn.
/// </summary>
public enum GradeExportFormat { Xlsx, Csv }

/// <summary>
/// Cổng dựng file .xlsx. Hiện thực dùng ClosedXML và nằm ở Infrastructure: kéo ClosedXML vào
/// Application là đảo chiều phụ thuộc, và mọi test handler sẽ phải tải một thư viện Excel chỉ
/// để kiểm một nhánh <c>if</c>.
/// </summary>
public interface IGradebookExcelWriter
{
    ExportedFile Write(GradebookDto so, IReadOnlyList<GradeExportItemDto> cot);
}

public sealed record ExportGradebookQuery(
    Guid OfferingId, Guid InstructorId, Guid? TemplateId,
    GradeExportFormat Format = GradeExportFormat.Xlsx) : IQuery<Result<ExportedFile>>;

public sealed class ExportGradebookHandler(IAppDbContext db, ISender sender, IGradebookExcelWriter excel)
    : IQueryHandler<ExportGradebookQuery, Result<ExportedFile>>
{
    public async Task<Result<ExportedFile>> Handle(ExportGradebookQuery request, CancellationToken ct)
    {
        var so = await sender.Send(new GetGradebookQuery(request.OfferingId, request.InstructorId), ct);
        if (!so.IsSuccess) return Result<ExportedFile>.NotFound(so.Error);

        var mau = request.TemplateId is { } tid
            ? await db.GradeExportTemplates.AsNoTracking().Include(t => t.Items)
                .SingleOrDefaultAsync(t => t.Id == tid && t.InstructorId == request.InstructorId, ct)
            : await db.GradeExportTemplates.AsNoTracking().Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.InstructorId == request.InstructorId && t.IsDefault, ct);

        var cot = mau is not null
            ? [.. mau.Items.OrderBy(i => i.OrderIndex)
                   .Select(i => new GradeExportItemDto(i.Source, i.ColumnKey, i.Header, i.OrderIndex, i.DecimalPlaces))]
            : MacDinh(so.Value);

        return Result<ExportedFile>.Ok(request.Format == GradeExportFormat.Csv
            ? GradeCsvWriter.Write(so.Value, cot)
            : excel.Write(so.Value, cot));
    }

    /// <summary>Chưa soạn mẫu nào thì xuất đủ mọi cột — vẫn dùng được ngay lần đầu.</summary>
    private static List<GradeExportItemDto> MacDinh(GradebookDto so)
    {
        var ds = new List<GradeExportItemDto>
        {
            new(GradeExportSource.StudentCode, null, "MSSV", 0, 0),
            new(GradeExportSource.FullName, null, "Họ và tên", 1, 0),
        };

        var i = 2;
        foreach (var c in so.Columns)
            ds.Add(new GradeExportItemDto(GradeExportSource.GradeColumn, c.Key, c.Name, i++, 1));

        ds.Add(new GradeExportItemDto(GradeExportSource.BonusTotal, null, "Điểm cộng", i++, 1));
        ds.Add(new GradeExportItemDto(GradeExportSource.FinalScore, null, "Điểm tổng", i, 1));

        return ds;
    }
}

/// <summary>
/// Quy MỘT ô của mẫu xuất về giá trị thật. Bản DUY NHẤT: cả CSV lẫn .xlsx đều đi qua đây.
/// Mỗi bản chép lại là một chỗ sẽ lệch, và hai file xuất cho cùng một lớp ra hai con số khác
/// nhau là kiểu lỗi không ai đối chiếu cho tới khi sinh viên khiếu nại.
/// </summary>
public static class GradeExportValues
{
    /// <summary>
    /// Nguồn cho ra SỐ (điểm) hay cho ra CHỮ. Quyết định này đi thẳng vào ô Excel: ghi điểm
    /// dưới dạng chuỗi thì mọi công thức SUM/AVERAGE của phòng đào tạo trả 0 mà không báo lỗi.
    /// </summary>
    public static bool IsNumeric(GradeExportSource nguon) => nguon
        is GradeExportSource.GradeColumn
        or GradeExportSource.BonusTotal
        or GradeExportSource.FinalScore;

    /// <summary>Giá trị số của ô, hoặc null khi chưa có điểm (ô để TRỐNG, không ghi 0).</summary>
    public static decimal? Number(GradebookDto so, GradeRowDto hang, GradeExportItemDto o) => o.Source switch
    {
        GradeExportSource.BonusTotal => hang.BonusToFinal,
        GradeExportSource.FinalScore => hang.FinalScore,
        GradeExportSource.GradeColumn => DiemCot(so, hang, o.ColumnKey),
        _ => null,
    };

    /// <summary>Giá trị chữ của ô. MSSV nằm ở đây chứ không ở nhánh số — xem ghi chú trong writer.</summary>
    public static string Text(GradeRowDto hang, GradeExportItemDto o) => o.Source switch
    {
        GradeExportSource.StudentCode => hang.StudentCode,
        GradeExportSource.FullName => hang.FullName,
        GradeExportSource.ClassName => hang.ClassName ?? "",
        _ => "",                       // Email chưa có trong GradebookDto, Blank là cột để trống
    };

    private static decimal? DiemCot(GradebookDto so, GradeRowDto hang, string? key)
    {
        var c = so.Columns.FirstOrDefault(x => x.Key == key);
        return c is null ? null : hang.Cells.FirstOrDefault(x => x.ColumnId == c.Id)?.Score;
    }
}

/// <summary>
/// Đường xuất CSV — GIỮ LẠI bên cạnh .xlsx chứ không thay thế. Không phụ thuộc thư viện nào,
/// nên khi bộ sinh .xlsx gặp sự cố với một bảng điểm cụ thể thì giảng viên vẫn lấy được dữ
/// liệu ra. BOM UTF-8 là bắt buộc — thiếu nó thì Excel trên Windows đọc tiếng Việt thành ký
/// tự rác, và đó là lỗi người dùng gặp ngay lần xuất đầu tiên.
/// </summary>
public static class GradeCsvWriter
{
    public static ExportedFile Write(GradebookDto so, IReadOnlyList<GradeExportItemDto> cot)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine(string.Join(",", cot.Select(c => Escape(c.Header))));

        foreach (var h in so.Rows)
        {
            var o = cot.Select(c => GradeExportValues.IsNumeric(c.Source)
                ? So(GradeExportValues.Number(so, h, c), c.DecimalPlaces)
                : Escape(GradeExportValues.Text(h, c)));

            sb.AppendLine(string.Join(",", o));
        }

        // BOM UTF-8 để Excel nhận đúng tiếng Việt — GHÉP TAY, không trông vào cờ của encoding.
        // `new UTF8Encoding(true).GetBytes(...)` KHÔNG chèn BOM: cờ đó chỉ ảnh hưởng
        // GetPreamble() và StreamWriter. Bản trước viết đúng như vậy và tin là đã có BOM, nên
        // mọi file CSV xuất ra đều mở lên thành "Nguy?n V?n ?nh" trên Excel Windows.
        var utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        byte[] bytes = [.. utf8.GetPreamble(), .. utf8.GetBytes(sb.ToString())];

        return new ExportedFile(bytes, $"so-diem-{so.ClassCode}.csv", "text/csv; charset=utf-8");
    }

    private static string So(decimal? v, int soLe) =>
        v is null ? "" : v.Value.ToString($"F{soLe}", System.Globalization.CultureInfo.InvariantCulture);

    private static string Escape(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;
}
