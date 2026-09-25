using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartBank.BuildingBlocks.Domain;

namespace SmartBank.BuildingBlocks.Web;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ILogger<ExceptionHandlingMiddleware> logger)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            // Business failure: warn (not error) but keep ErrorCode + correlation searchable.
            logger.LogWarning(ex, "Domain failure {ErrorCode} on {Method} {Path} (CorrelationId={CorrelationId})",
                ex.Code, context.Request.Method, context.Request.Path,
                context.Items["CorrelationId"]?.ToString());
            await WriteProblem(context, 409, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            // P0: this used to be swallowed silently. Always Error with trace join keys.
            logger.LogError(ex, "Unhandled exception on {Method} {Path} (CorrelationId={CorrelationId}, TraceId={TraceId})",
                context.Request.Method, context.Request.Path,
                context.Items["CorrelationId"]?.ToString(), CurrentTraceId(context));
            await WriteProblem(context, 500, "UNEXPECTED", "An unexpected error occurred.");
        }
    }

    private static string CurrentTraceId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

    private static Task WriteProblem(HttpContext context, int status, string code, string detail)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? context.TraceIdentifier;
        var body = new
        {
            type = $"https://smartbank.local/errors/{code.ToLowerInvariant().Replace('_', '-')}",
            title = code,
            status,
            detail,
            correlationId,
            code,
            traceId = CurrentTraceId(context)
        };
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }

    public static IResult ToHttp(Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    public static IResult ToHttp<T>(Result<T> result, int successStatus = 200) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatus) : Problem(result.Error);

    public static IResult Problem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => 400,
            ErrorType.Unauthorized => 401,
            ErrorType.Forbidden => 403,
            ErrorType.NotFound => 404,
            ErrorType.Conflict => 409,
            _ => 500
        };
        return Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: status,
            type: $"https://smartbank.local/errors/{error.Code.ToLowerInvariant().Replace('_', '-')}",
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}

public static class ResultExtensions
{
    public static IResult ToHttp(this Result result) => ExceptionHandlingMiddleware.ToHttp(result);
    public static IResult ToHttp<T>(this Result<T> result, int successStatus = 200) => ExceptionHandlingMiddleware.ToHttp(result, successStatus);
}
