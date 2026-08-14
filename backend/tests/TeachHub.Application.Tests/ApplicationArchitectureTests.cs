using Shouldly;
using TeachHub.Application.Common;

namespace TeachHub.Application.Tests;

public class ApplicationArchitectureTests
{
    [Fact]
    public void Application_khong_duoc_tham_chieu_ASP_NET_Core()
    {
        // ArchitectureTests ở Domain.Tests CHỈ kiểm assembly Domain. Tầng Application vì thế
        // có thể kéo Microsoft.AspNetCore.Http vào mà không test nào đỏ — và đó chính là cách
        // IFormFile trôi xuống Application, biến mọi test handler thành bài dựng HTTP giả.
        // Quy ước: command nhận Stream + tên file + độ dài; IFormFile chỉ xuất hiện trong Controllers/.
        //
        // Microsoft.AspNetCore.Identity KHÔNG nằm trong danh sách cấm: UserManager/RoleManager
        // thật ra đến từ Microsoft.Extensions.Identity.Core, là POCO thuần không kéo hạ tầng web.
        string[] cam =
        [
            "Microsoft.AspNetCore.Http",
            "Microsoft.AspNetCore.Mvc",
            "Microsoft.AspNetCore.Server",
            "Hangfire",
            "AWSSDK",
            "ClosedXML",
        ];

        var thamChieu = typeof(Result).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        foreach (var c in cam)
            thamChieu.ShouldNotContain(n => n.StartsWith(c, StringComparison.Ordinal),
                $"Application đang tham chiếu {c} — tầng nghiệp vụ không được biết hạ tầng tồn tại.");
    }
}
