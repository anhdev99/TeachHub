using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Offerings;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Enrollments.Commands.ImportEnrollments;

public sealed record ImportErrorDto(int RowNumber, string Message);

public sealed record ImportEnrollmentsResponse(int Imported, int Updated, List<ImportErrorDto> Errors);

/// <summary>Nhận Stream chứ không nhận IFormFile: Application không được biết ASP.NET Core tồn tại.</summary>
public sealed record ImportEnrollmentsCommand(Stream Content) : ICommand<Result<ImportEnrollmentsResponse>>
{
    public Guid OfferingId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class ImportEnrollmentsCommandHandler(
    IAppDbContext db,
    IEnrollmentReader reader) : ICommandHandler<ImportEnrollmentsCommand, Result<ImportEnrollmentsResponse>>
{
    public async Task<Result<ImportEnrollmentsResponse>> Handle(
        ImportEnrollmentsCommand request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.InstructorId, ct))
            return Result<ImportEnrollmentsResponse>.NotFound("Không tìm thấy lớp học phần.");

        RosterReadResult doc;
        try
        {
            doc = reader.Read(request.Content);
        }
        catch (Exception ex)
        {
            // File không phải .xlsx, file hỏng, file có mật khẩu — đều là lỗi dữ liệu đầu vào
            // của người dùng chứ không phải sự cố hệ thống, nên 400 chứ không 500.
            return Result<ImportEnrollmentsResponse>.Invalid($"Không đọc được file: {ex.Message}");
        }

        if (doc.Error is not null) return Result<ImportEnrollmentsResponse>.Invalid(doc.Error);

        // Dòng hỏng bị loại ở đây, TRƯỚC khi có bất kỳ lệnh ghi nào — nên chúng không làm
        // rollback các dòng tốt. Các dòng tốt vào cùng một transaction do TransactionBehavior mở.
        var kq = EnrollmentImporter.Validate(doc.Rows);

        var maSv = kq.Valid.Select(v => v.StudentCode).ToList();
        var daCo = await db.Enrollments
            .Where(e => e.OfferingId == request.OfferingId && maSv.Contains(e.StudentCode))
            .ToDictionaryAsync(e => e.StudentCode, ct);

        var themMoi = 0;
        var capNhat = 0;

        foreach (var sv in kq.Valid)
        {
            if (daCo.TryGetValue(sv.StudentCode, out var cu))
            {
                // Import lại cùng file là chuyện chắc chắn xảy ra sau khi sửa vài dòng lỗi.
                // Cập nhật tên/email thay vì tạo bản trùng — unique index (offering_id, student_code)
                // cũng chặn ở tầng database nếu logic này sai.
                cu.FullName = sv.FullName;
                if (sv.Email.Length > 0) cu.Email = sv.Email;
                cu.ClassName = sv.ClassName;
                capNhat++;
                continue;
            }

            db.Enrollments.Add(new Enrollment
            {
                OfferingId = request.OfferingId,
                StudentCode = sv.StudentCode,
                FullName = sv.FullName,
                Email = sv.Email.Length > 0 ? sv.Email : null,
                ClassName = sv.ClassName,
                Status = EnrollmentStatus.Imported,
            });
            themMoi++;
        }

        await db.SaveChangesAsync(ct);

        return Result<ImportEnrollmentsResponse>.Ok(new ImportEnrollmentsResponse(
            themMoi, capNhat,
            [.. kq.Errors.Select(e => new ImportErrorDto(e.RowNumber, e.Message))]));
    }
}
