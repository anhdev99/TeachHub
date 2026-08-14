using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeachHub.Application.Features.Enrollments.Commands.ImportEnrollments;
using TeachHub.Application.Features.Enrollments.Queries.GetEnrollments;
using TeachHub.Application.Features.Offerings;
using TeachHub.Application.Features.Enrollments.Commands.CreateStudentAccounts;
using TeachHub.Application.Features.Offerings.Commands.CreateOffering;
using TeachHub.Application.Features.Offerings.Commands.JoinOffering;
using TeachHub.Application.Features.Offerings.Queries.GetMyOfferings;
using TeachHub.Application.Features.Offerings.Queries.GetOffering;
using TeachHub.Application.Features.Offerings.Queries.GetOfferings;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

// Class KHÔNG gắn policy: controller này phục vụ cả giảng viên lẫn sinh viên (action Join,
// MyOfferings ở Task 2.3). [Authorize] ở class và ở action CỘNG DỒN chứ không ghi đè, nên
// đặt Policies.Instructor ở class rồi Policies.Student cho một action là KHÔNG AI qua được cả hai.
[Route("api/offerings")]
[Tags("Offerings")]
public sealed class OfferingsController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<OfferingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateOfferingCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));

    [HttpGet]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<IReadOnlyList<OfferingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? subjectId, CancellationToken ct)
        => Ok(await sender.Send(new GetOfferingsQuery(CurrentUserId, subjectId), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<OfferingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetOfferingQuery(id, CurrentUserId), ct));

    [HttpGet("{id:guid}/enrollments")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<IReadOnlyList<EnrollmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrollments(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetEnrollmentsQuery(id, CurrentUserId), ct));

    [HttpPost("join")]
    [Authorize(Policy = Policies.Student)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Join(JoinOfferingCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { UserId = CurrentUserId }, ct));

    // Route bắt đầu bằng '/' là route TUYỆT ĐỐI: nó bỏ qua [Route("api/offerings")] của class,
    // nên đường dẫn ra đúng /api/me/offerings như hợp đồng SRS mục 7.
    [HttpGet("/api/me/offerings")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<IReadOnlyList<MyOfferingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MyOfferings(CancellationToken ct)
        => Ok(await sender.Send(new GetMyOfferingsQuery(CurrentUserId), ct));

    [HttpPost("{id:guid}/enrollments/create-accounts")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<CreateStudentAccountsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateStudentAccounts(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new CreateStudentAccountsCommand { OfferingId = id, InstructorId = CurrentUserId }, ct));

    [HttpPost("{id:guid}/enrollments/import")]
    [Authorize(Policy = Policies.Instructor)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]                             // danh sách lớp không bao giờ quá 10 MB
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)] // hạn mức riêng của model binder multipart
    [EnableRateLimiting("upload")]
    [IgnoreAntiforgeryToken]
    [ProducesResponseType<ImportEnrollmentsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ImportEnrollments(
        Guid id, [FromForm] ImportRosterForm form, CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();

        return ToActionResult(await sender.Send(new ImportEnrollmentsCommand(stream)
        {
            OfferingId = id, InstructorId = CurrentUserId,
        }, ct));
    }
}

/// <summary>
/// Bọc IFormFile trong lớp [FromForm] — bắt buộc, không phải thẩm mỹ. Với tham số rời,
/// OpenAPI sinh application/x-www-form-urlencoded và liệt kê thuộc tính nội bộ của IFormFile
/// thay vì một field nhị phân, nên client TypeScript sinh tự động không gọi được endpoint này.
/// </summary>
public sealed class ImportRosterForm
{
    [FromForm(Name = "file")]   // ghim tên trên dây; model binder form KHÔNG theo JsonNamingPolicy
    public required IFormFile File { get; init; }
}
