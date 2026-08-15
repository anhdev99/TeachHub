using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Offerings;
using TeachHub.Domain.Groups;

namespace TeachHub.Application.Features.Groups;

public sealed record GroupMemberDto(Guid EnrollmentId, string StudentCode, string FullName);

public sealed record GroupDto(Guid Id, Guid OfferingId, string Name, List<GroupMemberDto> Members);

// ── Đọc danh sách nhóm ──────────────────────────────────────────────────────────────────

public sealed record GetGroupsQuery(Guid OfferingId, Guid ActorId) : IQuery<Result<List<GroupDto>>>;

public sealed class GetGroupsHandler(IAppDbContext db)
    : IQueryHandler<GetGroupsQuery, Result<List<GroupDto>>>
{
    public async Task<Result<List<GroupDto>>> Handle(GetGroupsQuery request, CancellationToken ct)
    {
        // Giảng viên của lớp HOẶC sinh viên đang học lớp đó đều đọc được — sinh viên cần biết
        // mình ở nhóm nào. Không thuộc cả hai ⇒ 404, không bao giờ 403.
        var laGiangVien = await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.ActorId, ct);
        var laSinhVien = await db.Enrollments.AnyAsync(
            e => e.OfferingId == request.OfferingId && e.UserId == request.ActorId, ct);

        if (!laGiangVien && !laSinhVien)
            return Result<List<GroupDto>>.NotFound("Không tìm thấy lớp học phần.");

        var nhom = await db.StudentGroups.AsNoTracking()
            .Include(g => g.Members)
            .Where(g => g.OfferingId == request.OfferingId)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

        var ids = nhom.SelectMany(g => g.Members.Select(m => m.EnrollmentId)).ToList();

        var sv = await db.Enrollments.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.StudentCode, e.FullName })
            .ToListAsync(ct);

        return Result<List<GroupDto>>.Ok([.. nhom.Select(g => new GroupDto(
            g.Id, g.OfferingId, g.Name,
            [.. g.Members
                 .Select(m => sv.FirstOrDefault(x => x.Id == m.EnrollmentId))
                 .Where(x => x is not null)
                 .Select(x => new GroupMemberDto(x!.Id, x.StudentCode, x.FullName))
                 .OrderBy(x => x.FullName)]))]);
    }
}

// ── Tạo nhóm thủ công ───────────────────────────────────────────────────────────────────

public sealed record CreateGroupCommand(string Name, List<Guid> MemberEnrollmentIds)
    : ICommand<Result<GroupDto>>
{
    public Guid OfferingId { get; init; }
    public Guid ActorId { get; init; }
}

public sealed class CreateGroupValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Chưa đặt tên nhóm.").MaximumLength(100);
        RuleFor(x => x.MemberEnrollmentIds).NotEmpty()
            .WithMessage("Nhóm rỗng thì không có gì để chấm.");
    }
}

public sealed class CreateGroupHandler(IAppDbContext db)
    : ICommandHandler<CreateGroupCommand, Result<GroupDto>>
{
    public async Task<Result<GroupDto>> Handle(CreateGroupCommand request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.ActorId, ct))
            return Result<GroupDto>.NotFound("Không tìm thấy lớp học phần.");

        if (await db.StudentGroups.AnyAsync(
                g => g.OfferingId == request.OfferingId && g.Name == request.Name.Trim(), ct))
            return Result<GroupDto>.Conflict($"Lớp này đã có nhóm tên “{request.Name.Trim()}”.");

        // Luật theo dữ liệu đang có ⇒ ở handler, không ở validator: một sinh viên không được
        // ở hai nhóm cùng lúc. Database cũng giữ bất biến này bằng unique index — kiểm ở đây
        // chỉ để trả về câu tiếng Việt thay vì một lỗi ràng buộc thô.
        var daCoNhom = await db.GroupMembers.AsNoTracking()
            .Where(m => request.MemberEnrollmentIds.Contains(m.EnrollmentId))
            .Select(m => m.EnrollmentId)
            .ToListAsync(ct);

        if (daCoNhom.Count > 0)
        {
            var ten = await db.Enrollments.AsNoTracking()
                .Where(e => daCoNhom.Contains(e.Id)).Select(e => e.FullName).ToListAsync(ct);
            return Result<GroupDto>.Conflict($"Đã ở nhóm khác: {string.Join(", ", ten)}.");
        }

        var nhom = new StudentGroup
        {
            OfferingId = request.OfferingId,
            Name = request.Name.Trim(),
            Members = [.. request.MemberEnrollmentIds.Distinct()
                          .Select(id => new GroupMember { EnrollmentId = id })],
        };

        db.StudentGroups.Add(nhom);
        await db.SaveChangesAsync(ct);

        return await new GetGroupsHandler(db)
            .Handle(new GetGroupsQuery(request.OfferingId, request.ActorId), ct)
            is { IsSuccess: true } ds
                ? Result<GroupDto>.Ok(ds.Value.Single(g => g.Id == nhom.Id))
                : Result<GroupDto>.Ok(new GroupDto(nhom.Id, nhom.OfferingId, nhom.Name, []));
    }
}

// ── Chia nhóm ngẫu nhiên ────────────────────────────────────────────────────────────────

public sealed record AutoGenerateGroupsCommand(int GroupSize, int Seed, bool Preview)
    : ICommand<Result<List<GroupDto>>>
{
    public Guid OfferingId { get; init; }
    public Guid ActorId { get; init; }
}

public sealed class AutoGenerateGroupsValidator : AbstractValidator<AutoGenerateGroupsCommand>
{
    public AutoGenerateGroupsValidator()
    {
        RuleFor(x => x.GroupSize).InclusiveBetween(2, 20)
            .WithMessage("Số người mỗi nhóm phải từ 2 trở lên — nhóm một người không phải bài tập nhóm.");
    }
}

public sealed class AutoGenerateGroupsHandler(IAppDbContext db)
    : ICommandHandler<AutoGenerateGroupsCommand, Result<List<GroupDto>>>
{
    public async Task<Result<List<GroupDto>>> Handle(
        AutoGenerateGroupsCommand request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.ActorId, ct))
            return Result<List<GroupDto>>.NotFound("Không tìm thấy lớp học phần.");

        var sv = await db.Enrollments.AsNoTracking()
            .Where(e => e.OfferingId == request.OfferingId)
            .OrderBy(e => e.StudentCode)         // thứ tự ổn định ⇒ cùng seed cho cùng kết quả
            .Select(e => new { e.Id, e.StudentCode, e.FullName })
            .ToListAsync(ct);

        if (sv.Count == 0)
            return Result<List<GroupDto>>.Invalid("Lớp chưa có sinh viên nào để chia nhóm.");

        var chia = GroupAllocator.Allocate([.. sv.Select(x => x.Id)], request.GroupSize, request.Seed);

        var ketQua = chia.Select((g, i) => new GroupDto(
            Guid.Empty, request.OfferingId, $"Nhóm {i + 1}",
            [.. g.Select(id => sv.First(x => x.Id == id))
                 .Select(x => new GroupMemberDto(x.Id, x.StudentCode, x.FullName))])).ToList();

        // Xem trước: KHÔNG ghi gì xuống DB. Cùng seed sẽ cho lại đúng kết quả này lúc xác nhận.
        if (request.Preview) return Result<List<GroupDto>>.Ok(ketQua);

        // Xác nhận: xóa nhóm cũ rồi ghi nhóm mới. Xóa được vì StudentGroup không phải dữ liệu
        // điểm — bài nộp nhóm trỏ tới group_id với DeleteBehavior.Restrict, nên nếu đã có bài
        // nộp nhóm thì lệnh xóa này thất bại và ta trả 409 thay vì làm mồ côi bài nộp.
        var cu = await db.StudentGroups.Include(g => g.Members)
            .Where(g => g.OfferingId == request.OfferingId).ToListAsync(ct);

        if (cu.Count > 0)
        {
            var idCu = cu.Select(g => g.Id).ToList();
            if (await db.Submissions.AnyAsync(s => s.GroupId != null && idCu.Contains(s.GroupId.Value), ct))
                return Result<List<GroupDto>>.Conflict(
                    "Đã có bài nộp theo nhóm hiện tại. Chia lại nhóm sẽ làm bài nộp không còn chủ — hãy sửa từng nhóm thay vì chia lại.");

            db.StudentGroups.RemoveRange(cu);
            await db.SaveChangesAsync(ct);
        }

        var moi = chia.Select((g, i) => new StudentGroup
        {
            OfferingId = request.OfferingId,
            Name = $"Nhóm {i + 1}",
            Members = [.. g.Select(id => new GroupMember { EnrollmentId = id })],
        }).ToList();

        db.StudentGroups.AddRange(moi);
        await db.SaveChangesAsync(ct);

        return await new GetGroupsHandler(db)
            .Handle(new GetGroupsQuery(request.OfferingId, request.ActorId), ct);
    }
}

// ── Sửa thành viên một nhóm ─────────────────────────────────────────────────────────────

public sealed record UpdateGroupMembersCommand(List<Guid> MemberEnrollmentIds) : ICommand<Result>
{
    public Guid GroupId { get; init; }
    public Guid ActorId { get; init; }
}

public sealed class UpdateGroupMembersHandler(IAppDbContext db)
    : ICommandHandler<UpdateGroupMembersCommand, Result>
{
    public async Task<Result> Handle(UpdateGroupMembersCommand request, CancellationToken ct)
    {
        var nhom = await db.StudentGroups.Include(g => g.Members)
            .SingleOrDefaultAsync(g => g.Id == request.GroupId, ct);

        if (nhom is null ||
            !await OfferingOwnership.OwnsOfferingAsync(db, nhom.OfferingId, request.ActorId, ct))
            return Result.NotFound("Không tìm thấy nhóm.");

        var moi = request.MemberEnrollmentIds.Distinct().ToList();

        var oNhomKhac = await db.GroupMembers.AsNoTracking()
            .Where(m => moi.Contains(m.EnrollmentId) && m.GroupId != nhom.Id)
            .Select(m => m.EnrollmentId).ToListAsync(ct);

        if (oNhomKhac.Count > 0)
        {
            var ten = await db.Enrollments.AsNoTracking()
                .Where(e => oNhomKhac.Contains(e.Id)).Select(e => e.FullName).ToListAsync(ct);
            return Result.Conflict($"Đã ở nhóm khác: {string.Join(", ", ten)}.");
        }

        // Add/Remove TƯỜNG MINH qua DbSet chứ không qua navigation của parent đang tracked:
        // Entity.Id tự sinh UUIDv7 lúc khởi tạo nên EF nhìn khóa khác default rồi kết luận
        // hàng đã tồn tại, phát UPDATE thay vì INSERT và chết với "expected to affect 1 row".
        db.GroupMembers.RemoveRange(nhom.Members.Where(m => !moi.Contains(m.EnrollmentId)));

        db.GroupMembers.AddRange(
            moi.Where(id => nhom.Members.All(m => m.EnrollmentId != id))
               .Select(id => new GroupMember { GroupId = nhom.Id, EnrollmentId = id }));

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

// ── Sinh viên tự tham gia nhóm ──────────────────────────────────────────────────────────

public sealed record JoinGroupCommand(Guid GroupId, Guid UserId) : ICommand<Result>;

public sealed class JoinGroupHandler(IAppDbContext db) : ICommandHandler<JoinGroupCommand, Result>
{
    public async Task<Result> Handle(JoinGroupCommand request, CancellationToken ct)
    {
        var nhom = await db.StudentGroups.SingleOrDefaultAsync(g => g.Id == request.GroupId, ct);
        if (nhom is null) return Result.NotFound("Không tìm thấy nhóm.");

        var ghiDanh = await db.Enrollments
            .Where(e => e.OfferingId == nhom.OfferingId && e.UserId == request.UserId)
            .Select(e => (Guid?)e.Id).SingleOrDefaultAsync(ct);

        // Không học lớp có nhóm này ⇒ 404 chứ không 403: 403 xác nhận nhóm đó có thật.
        if (ghiDanh is null) return Result.NotFound("Không tìm thấy nhóm.");

        if (await db.GroupMembers.AnyAsync(m => m.EnrollmentId == ghiDanh, ct))
            return Result.Conflict("Bạn đã ở trong một nhóm của lớp này.");

        db.GroupMembers.Add(new GroupMember { GroupId = nhom.Id, EnrollmentId = ghiDanh.Value });
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
