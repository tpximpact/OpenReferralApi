using System.Net;
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

        var problemDetails = new ProblemDetails
        {
            Status = GetStatusCode(exception),
            Title = GetTitle(exception),
            Detail = _environment.IsDevelopment() ? exception.Message : "An error occurred processing your request.",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = httpContext.TraceIdentifier,
                ["timestamp"] = DateTime.UtcNow
            }
        };

        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
            problemDetails.Extensions["innerException"] = exception.InnerException?.Message;
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
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
internal sealed class ProblemDetails
{
    public int? Status { get; set; }
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public Dictionary<string, object?> Extensions { get; set; } = [];
}
