using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Quizzes;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

/// <summary>Ngân hàng câu hỏi — thuộc giảng viên, dùng lại cho mọi môn và mọi học kỳ.</summary>
[Route("api/questions")]
[Tags("Quizzes")]
[Authorize(Policy = Policies.Instructor)]
public sealed class QuestionsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<QuizQuestionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        // Model binder của query string KHÔNG theo JsonNamingPolicy — tham số nhiều chữ phải
        // khai [FromQuery(Name = ...)] tường minh, nếu không nó lặng lẽ luôn nhận null.
        [FromQuery(Name = "tag")] string? tag, CancellationToken ct)
        => Ok(await sender.Send(new GetQuestionsQuery(CurrentUserId, tag), ct));

    [HttpPost]
    [ProducesResponseType<QuizQuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Save(SaveQuestionCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new DeleteQuestionCommand(id, CurrentUserId), ct));
}

// Class KHÔNG mang policy: cùng tiền tố phục vụ cả giảng viên (soạn đề) lẫn sinh viên
// (bắt đầu làm bài). [Authorize] ở class và action CỘNG DỒN chứ không ghi đè.
[Route("api")]
[Tags("Quizzes")]
public sealed class QuizzesController(ISender sender) : ApiControllerBase
{
    [HttpGet("quizzes")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<List<QuizDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(
        [FromQuery(Name = "subject_version_id")] Guid? subjectVersionId,
        [FromQuery(Name = "offering_id")] Guid? offeringId,
        CancellationToken ct)
        => ToActionResult(await sender.Send(
            new GetQuizzesQuery(subjectVersionId, offeringId, CurrentUserId), ct));

    [HttpGet("quizzes/{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<QuizDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetQuizQuery(id, CurrentUserId), ct));

    [HttpPost("quizzes")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<QuizDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Save(SaveQuizCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));

    [HttpPut("quizzes/{id:guid}/questions")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<QuizDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetQuestions(
        Guid id, SetQuizQuestionsCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { QuizId = id, InstructorId = CurrentUserId }, ct));

    // ── Phía sinh viên ──────────────────────────────────────────────────────────────────

    [HttpGet("offerings/{offeringId:guid}/quizzes")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<List<MyQuizDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MyQuizzes(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetMyQuizzesQuery(offeringId, CurrentUserId), ct));

    [HttpPost("quizzes/{id:guid}/attempts")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<TakeAttemptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new StartQuizAttemptCommand(id, CurrentUserId), ct));

    [HttpGet("quiz-attempts/{id:guid}")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<TakeAttemptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttempt(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetQuizAttemptQuery(id, CurrentUserId), ct));

    /// <summary>Tự lưu sau mỗi câu — không đợi tới lúc nộp.</summary>
    [HttpPut("quiz-attempts/{id:guid}/answers")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveAnswers(
        Guid id, SaveQuizAnswersCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { AttemptId = id, UserId = CurrentUserId }, ct));

    [HttpPost("quiz-attempts/{id:guid}/submit")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<TakeAttemptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new SubmitQuizAttemptCommand(id, CurrentUserId), ct));
}
