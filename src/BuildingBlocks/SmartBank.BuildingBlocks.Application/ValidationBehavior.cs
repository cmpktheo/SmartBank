using FluentValidation;
using MediatR;
using SmartBank.BuildingBlocks.Domain;

namespace SmartBank.BuildingBlocks.Application;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

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
                var message = string.Join("; ", failures.Select(f => f.ErrorMessage));
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
