using Shouldly;

namespace TeachHub.Domain.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_khong_duoc_phu_thuoc_vao_ha_tang()
    {
        // MediatR và FluentValidation nằm trong danh sách cấm cùng lý do với EF Core:
        // ICommand/IQuery là hợp đồng của tầng Application, Domain không được biết chúng tồn tại.
        // Không chặn ở đây thì cám dỗ "cho Domain phát INotification làm domain event" sẽ thắng,
        // và luật nghiệp vụ mất khả năng test trong mili-giây — thứ đắt nhất mà kiến trúc này mua được.
        string[] cam = ["Microsoft.EntityFrameworkCore", "Npgsql", "Hangfire",
                        "MediatR", "FluentValidation",
                        "Microsoft.AspNetCore", "AWSSDK"];

        var thamChieu = typeof(TeachHub.Domain.Common.Entity).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        foreach (var c in cam)
            thamChieu.ShouldNotContain(n => n.StartsWith(c, StringComparison.Ordinal),
                $"Domain đang tham chiếu {c} — vi phạm ADR-002.");
    }
}
