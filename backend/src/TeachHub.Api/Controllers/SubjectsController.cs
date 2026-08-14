using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Subjects;
using TeachHub.Application.Features.Subjects.Commands.CloneSubjectVersion;
using TeachHub.Application.Features.Subjects.Commands.CreateSubject;
using TeachHub.Application.Features.Subjects.Queries.GetSubject;
using TeachHub.Application.Features.Subjects.Queries.GetSubjects;
using TeachHub.Application.Features.Subjects.Queries.GetSubjectVersions;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

// Policy gắn xuống TỪNG action, không gắn ở cấp class: [Authorize] ở class và ở action
// CỘNG DỒN chứ không ghi đè, nên một action dành cho sinh viên thêm vào đây ở Phase 2
// sẽ đòi tài khoản có cả hai vai và không ai vào được.
[Route("api/subjects")]
[Tags("Subjects")]
public sealed class SubjectsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<IReadOnlyList<SubjectDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await sender.Send(new GetSubjectsQuery(CurrentUserId), ct));

    [HttpPost]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<SubjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateSubjectCommand command, CancellationToken ct)
        // Ghi đè InstructorId bằng claim. Tin giá trị client gửi lên là bất kỳ ai cũng
        // tạo được môn học đứng tên giảng viên khác.
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<SubjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetSubjectQuery(id, CurrentUserId), ct));

    [HttpGet("{id:guid}/versions")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<IReadOnlyList<SubjectVersionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetSubjectVersionsQuery(id, CurrentUserId), ct));

    [HttpPost("{id:guid}/versions")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<SubjectVersionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateVersion(
        Guid id, CloneSubjectVersionCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { SubjectId = id, InstructorId = CurrentUserId }, ct));
}
