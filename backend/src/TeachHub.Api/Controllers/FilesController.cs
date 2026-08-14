using MediatR;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Content.Queries.GetLessonFile;

namespace TeachHub.Api.Controllers;

[Route("api/files")]
[Tags("Files")]
public sealed class FilesController(ISender sender) : ApiControllerBase
{
    /// <summary>
    /// KHÔNG gắn policy: cả giảng viên lẫn sinh viên đều xem file bài giảng. Ai được xem file
    /// nào do handler quyết định theo quyền sở hữu nội dung / ghi danh lớp — chứ không theo vai trò.
    ///
    /// <c>{**path}</c> là catch-all: đường dẫn lưu trữ có dấu '/' bên trong ("lessons/&lt;uuid&gt;.pdf").
    /// </summary>
    [HttpGet("{**path}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string path, CancellationToken ct)
    {
        var kq = await sender.Send(new GetLessonFileQuery(path, CurrentUserId), ct);
        if (!kq.IsSuccess) return ToActionResult(kq);

        // inline chứ không attachment: mục tiêu là XEM TRƯỚC trong trình duyệt, không phải tải về.
        Response.Headers.ContentDisposition = $"inline; filename=\"{kq.Value.FileName}\"";
        return File(kq.Value.Content, kq.Value.ContentType);
    }
}
