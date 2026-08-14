using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace TeachHub.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICurrentUser currentUser) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int NguongChamMs = 500;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var ten = typeof(TRequest).Name;
        var moc = Stopwatch.GetTimestamp();

        try
        {
            var response = await next();
            var ms = Stopwatch.GetElapsedTime(moc).TotalMilliseconds;

            if (ms > NguongChamMs)
                logger.LogWarning("{Request} chạy {Elapsed:F0} ms (user {UserId}) — vượt ngưỡng {Nguong} ms",
                    ten, ms, currentUser.UserId, NguongChamMs);
            else
                logger.LogInformation("{Request} xong trong {Elapsed:F0} ms (user {UserId})",
                    ten, ms, currentUser.UserId);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Request} lỗi sau {Elapsed:F0} ms (user {UserId})",
                ten, Stopwatch.GetElapsedTime(moc).TotalMilliseconds, currentUser.UserId);
            throw;
        }
    }
}
