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
        var response = await next();
        sw.Stop();
        if (response.IsSuccess)
            _logger.LogInformation("{Request} succeeded in {ElapsedMs}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
        else
            _logger.LogWarning("{Request} failed with {Code} in {ElapsedMs}ms", typeof(TRequest).Name, response.Error.Code, sw.ElapsedMilliseconds);
        return response;
    }
}
