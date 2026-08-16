using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace TeachHub.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICurrentUser currentUser) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var response = await next();
            var ms = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            if (ms > SlowThresholdMs)
                logger.LogWarning("{Request} ran for {Elapsed:F0} ms (user {UserId}) — over the {Threshold} ms threshold",
                    requestName, ms, currentUser.UserId, SlowThresholdMs);
            else
                logger.LogInformation("{Request} completed in {Elapsed:F0} ms (user {UserId})",
                    requestName, ms, currentUser.UserId);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Request} failed after {Elapsed:F0} ms (user {UserId})",
                requestName, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, currentUser.UserId);
            throw;
        }
    }
}
