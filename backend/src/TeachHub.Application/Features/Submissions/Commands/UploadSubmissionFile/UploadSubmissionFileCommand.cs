using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Learning;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Files;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Submissions.Commands.UploadSubmissionFile;

/// <summary>
/// Takes a <see cref="Stream"/>, not an IFormFile: IFormFile drags Microsoft.AspNetCore.Http into
/// the Application layer and turns every handler test into an exercise in faking HTTP. The
/// controller is what opens the stream.
/// </summary>
public sealed record UploadSubmissionFileCommand(
    Guid AssignmentId, string SlotKey, string FileName, long SizeBytes,
    Stream Content, Guid StudentUserId) : ICommand<Result<SubmissionFileDto>>;

public sealed class UploadSubmissionFileHandler(
    IAppDbContext db, IFileStorage storage, TimeProvider clock, ISubmissionFileQueue queue)
    : ICommandHandler<UploadSubmissionFileCommand, Result<SubmissionFileDto>>
{
    public async Task<Result<SubmissionFileDto>> Handle(
        UploadSubmissionFileCommand request, CancellationToken ct)
    {
        // The order of these checks is deliberate: bail out the moment something is wrong, without
        // reading any further into the request body.
        // 1 — is the student enrolled in the offering that runs this assignment
        var enrollment = await SubmissionAccess.EnrollmentForAssignmentAsync(
            db, request.AssignmentId, request.StudentUserId, ct);

        if (enrollment is null) return Result<SubmissionFileDto>.NotFound("Không tìm thấy bài tập.");

        // 1b — is the offering still accepting work. It goes right here, BEFORE reading a single
        // byte of the request body: once the offering is closed there is no reason to read a whole
        // 50 MB file only to reject it afterwards.
        if (await OfferingLock.IsClosedAsync(db, enrollment.OfferingId, ct))
            return Result<SubmissionFileDto>.Conflict(OfferingLock.ClosedMessage("nộp bài"));

        // 2 — does the slot exist on this assignment
        var slot = await db.SubmissionSlots
            .SingleOrDefaultAsync(s => s.AssignmentId == request.AssignmentId
                                    && s.SlotKey == request.SlotKey, ct);

        if (slot is null)
            return Result<SubmissionFileDto>.NotFound($"Bài tập này không có phần nộp “{request.SlotKey}”.");

        // 3 — is the extension on the accepted list (Accepts goes through GetExtension, so
        //     "bai.zip.exe" yields ".exe" and gets blocked right here)
        if (!slot.Accepts(request.FileName))
            return Result<SubmissionFileDto>.Invalid(
                $"Phần “{slot.Label}” chỉ nhận {string.Join(", ", slot.AcceptedFileTypes)}.");

        // 4 — size against the slot's own limit (the pipeline-wide limit is already enforced by
        //     Kestrel)
        if (request.SizeBytes > slot.MaxFileSizeBytes)
            return Result<SubmissionFileDto>.PayloadTooLarge(
                $"Tệp nặng {request.SizeBytes / 1024 / 1024} MB, vượt giới hạn {slot.MaxFileSizeMb} MB của phần “{slot.Label}”.");

        var baiNop = await LayHoacTaoBanNhapAsync(request.AssignmentId, enrollment, ct);

        // 5 — slot còn chỗ không
        var daCo = baiNop.Files.Count(f => f.SlotId == slot.Id);
        if (!slot.HasRoomFor(daCo))
            return Result<SubmissionFileDto>.Invalid(
                $"Phần “{slot.Label}” chỉ nhận tối đa {slot.MaxFiles} tệp. Hãy xóa bớt trước khi nộp thêm.");

        // 6 — đọc 8 byte đầu để kiểm chữ ký thật. Client nói dối là chuyện mặc định phải
        //     giả định: đổi đuôi .txt thành .zip vượt được bước 3 nhưng không vượt được bước này.
        var dau = new byte[FileSignatureValidator.HeaderBytes];
        var doc = await request.Content.ReadAtLeastAsync(dau, dau.Length, throwOnEndOfStream: false, ct);

        if (!FileSignatureValidator.Matches(request.FileName, dau.AsSpan(0, doc)))
            return Result<SubmissionFileDto>.Invalid(
                $"Nội dung tệp không khớp với định dạng {Path.GetExtension(request.FileName)}. " +
                "Có thể tệp đã bị đổi đuôi hoặc hỏng.");

        // 7 — lưu qua storage. Ghép lại 8 byte đã đọc với phần còn lại của stream.
        var noiDung = new ConcatStream(dau.AsMemory(0, doc), request.Content);

        var luu = await storage.SaveAsync(
            $"submissions/{baiNop.Id}", request.FileName, noiDung, "application/octet-stream", ct);

        var file = new SubmissionFile
        {
            SubmissionId = baiNop.Id,
            SlotId = slot.Id,
            SlotKey = slot.SlotKey,
            FileName = request.FileName,
            FilePath = luu.Path,
            SizeBytes = luu.SizeBytes,
            ContentType = luu.ContentType,
            UploadedAt = clock.GetUtcNow(),

            // Slot không nhận zip thì không có gì để xử lý nền — đánh dấu xong ngay để
            // giao diện không hiện "đang xử lý" mãi mãi cho một tấm ảnh.
            ProcessingStatus = slot.AcceptsZip ? FileProcessingStatus.Pending : FileProcessingStatus.Done,
        };

        db.SubmissionFiles.Add(file);
        await db.SaveChangesAsync(ct);

        // 8 — đẩy job giải nén lên queue riêng. Upload trả về NGAY, không chờ giải nén xong.
        if (slot.AcceptsZip) queue.EnqueueProcessing(file.Id);

        // 9 — ảnh thì dựng thêm ảnh xem trước, trên hàng đợi MẶC ĐỊNH (không phải queue "zip").
        // Job chạy trên scope DI riêng: nó tự đọc lại tệp từ database và tự thoát nếu chưa
        // thấy hàng, nên upload không phải chờ và không phải bảo đảm gì cho nó.
        if (ThumbnailPolicy.IsImage(file.FileName)) queue.EnqueueThumbnail(file.Id);

        return Result<SubmissionFileDto>.Ok(SubmissionFileDto.From(file));
    }

    private async Task<Submission> LayHoacTaoBanNhapAsync(
        Guid assignmentId, EnrollmentRef ghiDanh, CancellationToken ct)
    {
        var hienCo = await db.Submissions
            .Include(s => s.Files)
            .SingleOrDefaultAsync(s => s.AssignmentId == assignmentId
                                    && s.EnrollmentId == ghiDanh.EnrollmentId
                                    && s.IsCurrent, ct);

        // Đã nộp chính thức rồi mà tải tệp tiếp = bắt đầu một lượt nộp lại. Bản cũ chuyển
        // sang lịch sử ngay tại đây chứ không đợi lúc bấm "Nộp chính thức": nếu đợi thì
        // hai bản cùng is_current tồn tại giữa chừng và partial unique index sẽ chặn.
        if (hienCo is not null && hienCo.Status != SubmissionStatus.Draft)
        {
            hienCo.IsCurrent = false;
            await db.SaveChangesAsync(ct);
            hienCo = null;
        }

        if (hienCo is not null) return hienCo;

        var soLan = await db.Submissions.CountAsync(
            s => s.AssignmentId == assignmentId && s.EnrollmentId == ghiDanh.EnrollmentId, ct);

        var moi = new Submission
        {
            OwnerType = SlotOwnerType.Assignment,
            AssignmentId = assignmentId,
            OfferingId = ghiDanh.OfferingId,
            EnrollmentId = ghiDanh.EnrollmentId,
            AttemptNo = soLan + 1,
            IsCurrent = true,
            Status = SubmissionStatus.Draft,
        };

        db.Submissions.Add(moi);
        await db.SaveChangesAsync(ct);

        return moi;
    }
}

/// <summary>
/// Nối phần đầu đã đọc để kiểm chữ ký với phần còn lại của stream gốc. Cần vì stream
/// multipart KHÔNG seek được: đọc 8 byte rồi tua lại là không làm được, mà đọc cả file
/// vào bộ nhớ để tua thì một bài nộp 200 MB chiếm 200 MB RAM của máy chủ.
/// </summary>
internal sealed class ConcatStream(ReadOnlyMemory<byte> dau, Stream con) : Stream
{
    private int _viTriDau;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (_viTriDau < dau.Length)
        {
            var n = Math.Min(buffer.Length, dau.Length - _viTriDau);
            dau.Span.Slice(_viTriDau, n).CopyTo(buffer);
            _viTriDau += n;
            return n;
        }

        return con.Read(buffer);
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_viTriDau < dau.Length)
        {
            var n = Math.Min(buffer.Length, dau.Length - _viTriDau);
            dau.Slice(_viTriDau, n).CopyTo(buffer);
            _viTriDau += n;
            return n;
        }

        return await con.ReadAsync(buffer, ct);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
