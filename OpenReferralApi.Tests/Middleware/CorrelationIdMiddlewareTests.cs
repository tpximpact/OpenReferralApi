using FluentAssertions;
using Microsoft.AspNetCore.Http;
using OpenReferralApi.Middleware;

namespace OpenReferralApi.Tests.Middleware;

[TestFixture]
public class CorrelationIdMiddlewareTests
{
    [Test]
    public async Task InvokeAsync_WithoutExistingCorrelationId_GeneratesNew()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Items.Should().ContainKey("CorrelationId");
        context.Items["CorrelationId"].Should().NotBeNull();
        context.Response.Headers.Should().ContainKey("X-Correlation-ID");
    }

    [Test]
    public async Task InvokeAsync_WithExistingCorrelationId_UsesExisting()
    {
        // Arrange
        var existingCorrelationId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = existingCorrelationId;

        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Items["CorrelationId"].Should().Be(existingCorrelationId);
        context.Response.Headers["X-Correlation-ID"].ToString().Should().Be(existingCorrelationId);
    }
}
