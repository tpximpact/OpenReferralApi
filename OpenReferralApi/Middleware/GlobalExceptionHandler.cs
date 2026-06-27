using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using OpenReferralApi.Logging;

namespace OpenReferralApi.Middleware;

/// <summary>
/// Global exception handler middleware for centralized error handling
/// </summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;
    private readonly IHostEnvironment _environment = environment;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        _logger.UnhandledExceptionOccurred(exception, httpContext.TraceIdentifier);

        var isDev = _environment.IsDevelopment();
        var problemDetails = new ProblemDetails
        {
            Status = GetStatusCode(exception),
            Title = GetTitle(exception),
            Detail = isDev ? exception.Message : "An error occurred processing your request.",
            Instance = httpContext.Request.Path,
            Extensions = new ProblemDetailsExtensions
            {
                TraceId = httpContext.TraceIdentifier,
                Timestamp = DateTime.UtcNow,
                StackTrace = isDev ? (exception.StackTrace ?? string.Empty) : null,
                InnerException = isDev ? (exception.InnerException?.Message ?? string.Empty) : null
            }
        };

        httpContext.Response.StatusCode = problemDetails.Status;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static int GetStatusCode(Exception exception) => exception switch
    {
        ArgumentNullException => (int)HttpStatusCode.BadRequest,
        ArgumentException => (int)HttpStatusCode.BadRequest,
        InvalidOperationException => (int)HttpStatusCode.BadRequest,
        UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
        NotImplementedException => (int)HttpStatusCode.NotImplemented,
        TimeoutException => (int)HttpStatusCode.RequestTimeout,
        _ => (int)HttpStatusCode.InternalServerError
    };

    private static string GetTitle(Exception exception) => exception switch
    {
        ArgumentNullException => "Bad Request",
        ArgumentException => "Bad Request",
        InvalidOperationException => "Bad Request",
        UnauthorizedAccessException => "Unauthorized",
        NotImplementedException => "Not Implemented",
        TimeoutException => "Request Timeout",
        _ => "Internal Server Error"
    };
}

/// <summary>
/// Standard problem details response
/// </summary>
internal readonly struct ProblemDetails
{
    public required int Status { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required string Instance { get; init; }
    public required ProblemDetailsExtensions Extensions { get; init; }
}

/// <summary>
/// Extensions property container for standard problem details response
/// </summary>
internal readonly struct ProblemDetailsExtensions
{
    public required string TraceId { get; init; }
    public required DateTime Timestamp { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StackTrace { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InnerException { get; init; }
}
