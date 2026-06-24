using Microsoft.Extensions.DependencyInjection;

namespace OpenReferralApi.Tests.Services;

internal static class TestHttpClientFactory
{
    private const string DefaultClientName = "OpenApiValidationService";

    public static HttpClient CreateClient(HttpMessageHandler? handler = null, string clientName = DefaultClientName)
    {
        return CreateFactory(handler, clientName).CreateClient(clientName);
    }

    public static IHttpClientFactory CreateFactory(HttpMessageHandler? handler = null, string clientName = DefaultClientName)
    {
        var services = new ServiceCollection();
        var builder = services.AddHttpClient(clientName);

        if (handler != null)
        {
            builder.ConfigurePrimaryHttpMessageHandler(() => handler);
        }

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>();
    }
}
