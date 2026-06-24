using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OpenReferralApi.Tests.Swagger;

[TestFixture]
public class SwaggerDocumentEndpointTests
{
    [Test]
    public async Task SwaggerJson_IncludesAtLeastOnePath()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Specification:WarmupEnabled"] = "false",
                        ["FeedValidation:Enabled"] = "false"
                    });
                });
            });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v3/swagger.json").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var document = JsonDocument.Parse(content);

        Assert.That(document.RootElement.TryGetProperty("paths", out var pathsElement), Is.True,
            "Swagger JSON should contain a 'paths' object.");
        Assert.That(pathsElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(pathsElement.EnumerateObject().Any(), Is.True,
            "Swagger 'paths' should contain at least one API endpoint.");
    }
}
