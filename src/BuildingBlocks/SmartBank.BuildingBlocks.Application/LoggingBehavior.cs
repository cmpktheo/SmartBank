using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartBank.BuildingBlocks.Domain;

namespace SmartBank.BuildingBlocks.Application;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();
            if (response.IsSuccess)
                _logger.LogInformation("{Request} succeeded in {ElapsedMs}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
            else
                _logger.LogWarning("{Request} failed with {ErrorCode} in {ElapsedMs}ms: {ErrorMessage}",
                    typeof(TRequest).Name, response.Error.Code, sw.ElapsedMilliseconds, response.Error.Message);
            return response;
        }
        catch (Exception ex)
        {
            // P0: unhandled handler exceptions used to bypass logging entirely.
            sw.Stop();
            _logger.LogError(ex, "{Request} threw {ExceptionType} after {ElapsedMs}ms",
                typeof(TRequest).Name, ex.GetType().Name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
