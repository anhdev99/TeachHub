using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Common;

namespace TeachHub.Api.Controllers;

// KHÔNG gắn [Produces("application/json")] ở đây. Nó ép content-type cho MỌI phản hồi của
// mọi controller, kể cả phản hồi lỗi — nên 401/404/409 trả đúng thân ProblemDetails nhưng
// gắn nhãn "application/json" thay vì "application/problem+json" theo RFC 9457. Frontend
// phân nhánh theo content-type sẽ không nhận ra đó là lỗi có cấu trúc, và bước 6 của kịch
// bản nghiệm thu Phase 0 đỏ. JSON vẫn là định dạng duy nhất vì chỉ có formatter JSON đăng ký.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Id người gọi, lấy từ claim. Fallback policy bảo đảm mọi request tới được đây đều đã
    /// qua xác thực, nên thiếu claim là lỗi cấu hình của ta chứ không phải lỗi người dùng —
    /// ném ra để thành 500 và lộ ngay, đừng âm thầm trả Guid.Empty rồi truy vấn nhầm dữ liệu.
    /// </summary>
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("Request đã xác thực nhưng thiếu claim sub hợp lệ.");

    // Thành công LUÔN trả 200, kể cả overload không generic. Trả NoContent() (204) ở đây thì
    // mọi [ProducesResponseType] và mọi test ở Phase 1–4b khẳng định 200 sẽ sai — hợp đồng
    // OpenAPI lệch và một loạt test đỏ. Một mã thành công duy nhất cho cả hai overload là
    // quy tắc rẻ nhất để không bao giờ lệch lại.
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
        _ => throw new InvalidOperationException($"Trạng thái chưa xử lý: {result.Status}"),
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
        _ => throw new InvalidOperationException($"Trạng thái chưa xử lý: {result.Status}"),
    };
}
