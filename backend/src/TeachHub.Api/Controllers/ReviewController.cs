using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Review;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

/// <summary>
/// Ôn tập ngắt quãng — toàn bộ endpoint đều thuộc phía sinh viên.
///
/// Class KHÔNG mang [Authorize]: policy ở cấp class và cấp action CỘNG DỒN chứ không ghi đè,
/// nên đặt policy ở cả hai chỗ là đòi tài khoản có cả hai vai và không ai vào được.
/// </summary>
[Route("api")]
[Tags("Review")]
public sealed class ReviewController(ISender sender) : ApiControllerBase
{
    /// <summary>Mục "Ôn tập hôm nay" trên trang chủ sinh viên.</summary>
    [HttpGet("review/today")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<TodayReviewDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Today(
        // Model binder của query string KHÔNG theo JsonNamingPolicy — tham số phải khai
        // [FromQuery(Name = ...)] tường minh, nếu không nó lặng lẽ luôn nhận null.
        [FromQuery(Name = "limit")] int? limit, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new GetTodayReviewQuery(limit) { UserId = CurrentUserId }, ct));

    /// <summary>Trả lời một mục ôn tập — đúng thì lên bậc, sai thì quay về đầu chu kỳ.</summary>
    [HttpPost("review/items/{id:guid}/answer")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<ReviewAnswerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Answer(
        Guid id, AnswerReviewItemCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { ItemId = id, UserId = CurrentUserId }, ct));

    /// <summary>3–5 câu ôn đầu giờ của một component <c>recap</c>, lấy theo nhãn chương trước.</summary>
    [HttpGet("lesson-components/{id:guid}/recap")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<RecapDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Recap(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetRecapQuestionsQuery(id, CurrentUserId), ct));
}
