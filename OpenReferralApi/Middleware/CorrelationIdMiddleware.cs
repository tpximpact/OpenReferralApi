namespace OpenReferralApi.Middleware;

/// <summary>
/// Middleware to add correlation IDs to requests for distributed tracing
/// </summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        _ = context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);

        await _next(context).ConfigureAwait(false);
    }
}
