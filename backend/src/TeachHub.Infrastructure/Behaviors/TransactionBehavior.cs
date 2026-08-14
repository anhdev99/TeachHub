using MediatR;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common.Messaging;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Infrastructure.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>(AppDbContext db)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // Query không cần transaction — đó là lý do IBaseCommand tồn tại.
        if (request is not IBaseCommand) return await next();

        // Đã có transaction bên ngoài (job nền, hoặc command lồng command) thì đi nhờ,
        // không mở transaction lồng nhau.
        if (db.Database.CurrentTransaction is not null) return await next();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var response = await next();
        await tx.CommitAsync(ct);
        return response;
    }
}
