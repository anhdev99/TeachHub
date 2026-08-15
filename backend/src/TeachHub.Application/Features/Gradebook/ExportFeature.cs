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

public sealed record ExportGradebookQuery(Guid OfferingId, Guid InstructorId, Guid? TemplateId)
    : IQuery<Result<ExportedFile>>;

public sealed class ExportGradebookHandler(IAppDbContext db, ISender sender)
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

        return Result<ExportedFile>.Ok(GradeCsvWriter.Write(so.Value, cot));
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
/// Xuất CSV có BOM UTF-8 thay vì .xlsx. Đánh đổi được ghi lại có chủ ý: Excel mở CSV bình
/// thường, còn thêm một thư viện sinh .xlsx là thêm một phụ thuộc nữa phải nuôi cho một tính
/// năng mà bản CSV đã phục vụ đủ. BOM là bắt buộc — thiếu nó thì Excel trên Windows đọc
/// tiếng Việt thành ký tự rác, và đó là lỗi người dùng gặp ngay lần xuất đầu tiên.
/// </summary>
public static class GradeCsvWriter
{
    public static ExportedFile Write(GradebookDto so, List<GradeExportItemDto> cot)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine(string.Join(",", cot.Select(c => Escape(c.Header))));

        foreach (var h in so.Rows)
        {
            var o = cot.Select(c => c.Source switch
            {
                GradeExportSource.StudentCode => Escape(h.StudentCode),
                GradeExportSource.FullName => Escape(h.FullName),
                GradeExportSource.ClassName => Escape(h.ClassName ?? ""),
                GradeExportSource.BonusTotal => So(h.BonusToFinal, c.DecimalPlaces),
                GradeExportSource.FinalScore => So(h.FinalScore, c.DecimalPlaces),
                GradeExportSource.GradeColumn => So(DiemCot(h, c.ColumnKey), c.DecimalPlaces),
                _ => "",
            });

            sb.AppendLine(string.Join(",", o));
        }

        // BOM UTF-8 để Excel nhận đúng tiếng Việt.
        var bytes = new System.Text.UTF8Encoding(true).GetBytes(sb.ToString());

        return new ExportedFile(bytes, $"so-diem-{so.ClassCode}.csv", "text/csv; charset=utf-8");

        decimal? DiemCot(GradeRowDto h, string? key)
        {
            var c = so.Columns.FirstOrDefault(x => x.Key == key);
            return c is null ? null : h.Cells.FirstOrDefault(x => x.ColumnId == c.Id)?.Score;
        }
    }

    private static string So(decimal? v, int soLe) =>
        v is null ? "" : v.Value.ToString($"F{soLe}", System.Globalization.CultureInfo.InvariantCulture);

    private static string Escape(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;
}
