using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartBank.BuildingBlocks.Domain;

namespace SmartBank.BuildingBlocks.Application;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken))))
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();
            if (failures.Count != 0)
            {
                // Warning carries property names only — never echo raw values
                // (IBANs, narratives) into logs. Full messages go to Debug.
                var props = string.Join(", ", failures.Select(f => f.PropertyName).Distinct());
                _logger.LogWarning("{Request} validation failed ({Count} errors on {Properties})",
                    typeof(TRequest).Name, failures.Count, props);
                var message = string.Join("; ", failures.Select(f => f.ErrorMessage));
                _logger.LogDebug("{Request} validation details: {Errors}", typeof(TRequest).Name, message);
                var error = Error.Validation("VALIDATION", message);
                // Return Result.Failure via reflection (supports Result and Result<T>)
                var responseType = typeof(TResponse);
                if (responseType == typeof(Result))
                    return (Result.Failure(error) as TResponse)!;
                var resultType = typeof(Result);
                var genericArgs = responseType.GetGenericArguments();
                var failureOpen = resultType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .First(m => m.Name == nameof(Result.Failure) && m.IsGenericMethodDefinition);
                var failureMethod = failureOpen.MakeGenericMethod(genericArgs);
                return (TResponse)failureMethod.Invoke(null, [error])!;
            }
        }
        return await next();
    }
}
