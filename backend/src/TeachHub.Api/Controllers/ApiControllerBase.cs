using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Common;

namespace TeachHub.Api.Controllers;

// Do NOT put [Produces("application/json")] here. It forces the content-type onto EVERY
// response of every controller, error responses included — so 401/404/409 still carry a proper
// ProblemDetails body but get labelled "application/json" instead of RFC 9457's
// "application/problem+json". A frontend that branches on content-type then fails to recognise
// them as structured errors, and step 6 of the Phase 0 acceptance script goes red. JSON stays
// the only format anyway, because the JSON formatter is the only one registered.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Caller id, taken from the claim. The fallback policy guarantees that every request which
    /// reaches this point is already authenticated, so a missing claim is a configuration bug on
    /// our side and not a user error — throw, so it turns into a 500 and shows up right away;
    /// never quietly return Guid.Empty and go on to query the wrong data with it.
    /// </summary>
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("Request is authenticated but carries no valid sub claim.");

    // Success ALWAYS answers 200, the non-generic overload included. Returning NoContent() (204)
    // here would falsify every [ProducesResponseType] and every Phase 1–4b test that asserts
    // 200 — the OpenAPI contract drifts and a whole batch of tests goes red. One single success
    // code for both overloads is the cheapest rule that keeps them from drifting apart again.
    protected IActionResult ToActionResult(Result result) => result.Status switch
    {
        ResultStatus.Ok              => Ok(),
        ResultStatus.Invalid         => Problem(statusCode: StatusCodes.Status400BadRequest,
                                                title: "Dữ liệu không hợp lệ", detail: result.Error),
        ResultStatus.NotFound        => Problem(statusCode: StatusCodes.Status404NotFound,
                                                title: "Không tìm thấy", detail: result.Error),
        ResultStatus.Conflict        => Problem(statusCode: StatusCodes.Status409Conflict,
                                                title: "Xung đột trạng thái", detail: result.Error),
        ResultStatus.Forbidden       => Problem(statusCode: StatusCodes.Status403Forbidden,
                                                title: "Không có quyền", detail: result.Error),
        ResultStatus.Unauthorized    => Problem(statusCode: StatusCodes.Status401Unauthorized,
                                                title: "Chưa xác thực", detail: result.Error),
        ResultStatus.PayloadTooLarge => Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                                                title: "Tệp vượt dung lượng cho phép", detail: result.Error),
        _ => throw new InvalidOperationException($"Unhandled result status: {result.Status}"),
    };

    protected IActionResult ToActionResult<T>(Result<T> result) => result.Status switch
    {
        ResultStatus.Ok              => Ok(result.Value),
        ResultStatus.Invalid         => Problem(statusCode: StatusCodes.Status400BadRequest,
                                                title: "Dữ liệu không hợp lệ", detail: result.Error),
        ResultStatus.NotFound        => Problem(statusCode: StatusCodes.Status404NotFound,
                                                title: "Không tìm thấy", detail: result.Error),
        ResultStatus.Conflict        => Problem(statusCode: StatusCodes.Status409Conflict,
                                                title: "Xung đột trạng thái", detail: result.Error),
        ResultStatus.Forbidden       => Problem(statusCode: StatusCodes.Status403Forbidden,
                                                title: "Không có quyền", detail: result.Error),
        ResultStatus.Unauthorized    => Problem(statusCode: StatusCodes.Status401Unauthorized,
                                                title: "Chưa xác thực", detail: result.Error),
        ResultStatus.PayloadTooLarge => Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                                                title: "Tệp vượt dung lượng cho phép", detail: result.Error),
        _ => throw new InvalidOperationException($"Unhandled result status: {result.Status}"),
    };
}
