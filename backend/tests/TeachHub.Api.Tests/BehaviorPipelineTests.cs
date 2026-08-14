using System.Data;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TeachHub.Application.Common.Behaviors;
using TeachHub.Application.Common.Messaging;
using TeachHub.Infrastructure.Behaviors;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests;

/// <summary>Command chỉ dùng để dò pipeline — không thuộc nghiệp vụ nào.</summary>
public sealed record ProbeCommand(string Code) : ICommand<Unit>;

public sealed class ProbeValidator : AbstractValidator<ProbeCommand>
{
    public ProbeValidator() => RuleFor(x => x.Code).NotEmpty();
}

public class BehaviorPipelineTests(TeachHubApiFactory factory) : IClassFixture<TeachHubApiFactory>
{
    [Fact]
    public void Thu_tu_chay_la_logging_roi_validation_roi_transaction()
    {
        // Thứ tự ĐĂNG KÝ chính là thứ tự CHẠY, từ ngoài vào trong. Test này khóa nó lại:
        // đảo hai dòng AddOpenBehavior trong DependencyInjection là test đỏ ngay lập tức.
        //
        // ĐỌC TỪ CONTAINER THẬT của ứng dụng (factory.Services), KHÔNG dựng lại một
        // ServiceCollection mới rồi tự tay AddTransient(TransactionBehavior). Dựng lại thì thứ tự
        // là do chính test tạo ra, nên nó xanh kể cả khi Program.cs đăng ký sai thứ tự —
        // một test cho cảm giác an toàn mà không kiểm gì cả.
        var thuTu = factory.Services
            .GetServices<IPipelineBehavior<ProbeCommand, Unit>>()
            .Select(b => b.GetType().GetGenericTypeDefinition())
            .ToList();

        thuTu.ShouldBe([
            typeof(LoggingBehavior<,>),
            typeof(ValidationBehavior<,>),
            typeof(TransactionBehavior<,>),
        ]);
    }

    [Fact]
    public async Task Command_sai_du_lieu_bi_chan_truoc_khi_mo_transaction()
    {
        // Nếu Transaction bọc ngoài Validation thì mỗi request sai dữ liệu vẫn mở một
        // transaction rồi rollback: thừa một round-trip và giữ connection lâu hơn cần thiết.
        // Với endpoint nộp bài lúc sát hạn — nơi request sai dữ liệu nhiều nhất — đó là
        // đúng thời điểm pool connection không được phép cạn.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var validation  = new ValidationBehavior<ProbeCommand, Unit>([new ProbeValidator()]);
        var transaction = new TransactionBehavior<ProbeCommand, Unit>(db);
        var command     = new ProbeCommand("");        // rỗng ⇒ validator đỏ

        var handlerDaChay = false;
        RequestHandlerDelegate<Unit> handler = _ =>
        {
            handlerDaChay = true;
            return Task.FromResult(Unit.Value);
        };

        // Xếp đúng thứ tự sản xuất: Validation bọc ngoài Transaction.
        await Should.ThrowAsync<ValidationException>(() => validation.Handle(
            command,
            ct => transaction.Handle(command, handler, ct),
            CancellationToken.None));

        handlerDaChay.ShouldBeFalse();
        db.Database.CurrentTransaction.ShouldBeNull();

        // Bằng chứng cứng nhất: BeginTransactionAsync bắt buộc phải mở connection.
        // Connection còn đóng nghĩa là không ai chạm tới DB.
        db.Database.GetDbConnection().State.ShouldBe(ConnectionState.Closed);
    }
}
