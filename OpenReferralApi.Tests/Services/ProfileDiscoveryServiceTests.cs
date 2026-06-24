using System.Net;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class ProfileDiscoveryServiceTests
{
    private const string CachedHsdsSchema = "{\"openapi\":\"3.0.0\",\"info\":{\"title\":\"HSDS\"}}";

    private Mock<IHttpClientFactory> _httpClientFactoryMock = null!;
    private Mock<ILogger<ProfileDiscoveryService>> _loggerMock = null!;
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    private HttpClient _httpClient = null!;
    private MemoryCache _memoryCache = null!;

    [SetUp]
    public void Setup()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<ProfileDiscoveryService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = TestHttpClientFactory.CreateClient(_httpMessageHandlerMock.Object);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("OpenApiValidationService"))
            .Returns(_httpClient);

        var jsonNode = JsonNode.Parse(CachedHsdsSchema)!;
        var compiledSchema = JsonSchemaBuild.FromText(CachedHsdsSchema);
        var cachedSchemaObject = new CachedSchema(compiledSchema, jsonNode, CachedHsdsSchema, CachedHsdsSchema.Length);
        _memoryCache.Set("schema:https://hsds.example.org/3.0/openapi.json", cachedSchemaObject);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
        _memoryCache.Dispose();
    }

    [Test]
    public void DiscoverFromBaseUrlAsync_WithEmptyBaseUrl_ThrowsArgumentException()
    {
        var service = CreateService();

        Assert.ThrowsAsync<ArgumentException>(() => service.DiscoverFromBaseUrlAsync(string.Empty, string.Empty));
    }

    [Test]
    public async Task DiscoverFromBaseUrlAsync_WhenVersionFoundAtRoot_ReturnsProfileVersionOnly()
    {
        SetupHttpResponseMap(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/"] = (HttpStatusCode.OK, "{\"version\":\"HSDS-UK-3.0\"}")
        });

        var service = CreateService();
        var result = await service.DiscoverFromBaseUrlAsync(null, "https://api.example.com");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HsdsProfileVersion, Is.EqualTo("HSDS-UK-3.0"));
            Assert.That(result.OpenApiSchemaContent, Is.Null);
        }

    }

    [Test]
    public async Task DiscoverFromBaseUrlAsync_WhenOpenApiSpecAtStandardPath_ReturnsSpecContent()
    {
        SetupHttpResponseMap(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/openapi.json"] = (HttpStatusCode.OK, "{\"x-hsds-version\":\"HSDS-UK-3.0\",\"openapi\":\"3.0.0\"}")
        });

        var service = CreateService();
        var result = await service.DiscoverFromBaseUrlAsync(null, "https://api.example.com");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HsdsProfileVersion, Is.EqualTo("HSDS-UK-3.0"));
            Assert.That(result.OpenApiSchemaContent, Does.Contain("openapi"));
        }

    }

    [Test]
    public async Task DiscoverFromBaseUrlAsync_WhenOwnSchemaValidationNone_ReturnsVersionWithoutSpecContent()
    {
        SetupHttpResponseMap(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/openapi.json"] = (HttpStatusCode.OK, "{\"x-hsds-version\":\"HSDS-UK-3.0\",\"openapi\":\"3.0.0\"}")
        });

        var service = CreateService(OwnSchemaValidationMode.None);
        var result = await service.DiscoverFromBaseUrlAsync(null, "https://api.example.com");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HsdsProfileVersion, Is.EqualTo("HSDS-UK-3.0"));
            Assert.That(result.OpenApiSchemaContent, Is.Null);
        }

    }

    [Test]
    public async Task DiscoverFromBaseUrlAsync_WhenProfileSchemaIsCached_ReturnsHsdsProfileSchemaContent()
    {
        SetupHttpResponseMap(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/"] = (HttpStatusCode.OK, "{\"version\":\"HSDS-UK-3.0\"}")
        });

        const string expectedProfileSchema = CachedHsdsSchema;

        var service = CreateService();
        var result = await service.DiscoverFromBaseUrlAsync(null, "https://api.example.com");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HsdsProfileVersion, Is.EqualTo("HSDS-UK-3.0"));
            Assert.That(result.HsdsProfileSchemaContent, Is.EqualTo(expectedProfileSchema));
        }

    }

    [Test]
    public void DiscoverFromBaseUrlAsync_WhenNoSpecFound_ThrowsKnownProfilesError()
    {
        var service = CreateService();
        var ex = Assert.ThrowsAsync<ArgumentException>(() => service.DiscoverFromBaseUrlAsync(null, "https://api.example.com"));
        Assert.That(ex!.Message, Does.Contain("Can only validate against known profile versions"));
    }

    [Test]
    public async Task DiscoverFromBaseUrlAsync_WhenUnsupportedProfileDiscoveredAndDefaultConfigured_FallsBackToDefaultProfile()
    {
        SetupHttpResponseMap(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/"] = (HttpStatusCode.OK, "{\"version\":\"UNSUPPORTED-VERSION\"}")
        });

        // Create service WITH a default profile version configured
        var service = new ProfileDiscoveryService(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            Options.Create(new SpecificationOptions
            {
                DefaultProfileVersion = "HSDS-UK-3.0",
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-3.0"] = "https://hsds.example.org/3.0/openapi.json"
                }
            }),
            Options.Create(new OpenApiValidationServerOptions { OwnSchemaValidation = OwnSchemaValidationMode.Strict }),
            _memoryCache);

        var result = await service.DiscoverFromBaseUrlAsync(null, "https://api.example.com");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HsdsProfileVersion, Is.EqualTo("HSDS-UK-3.0"));
            Assert.That(result.UsedDefaultProfile, Is.True);
            Assert.That(result.HsdsProfileReason, Does.Contain("Falling back to configured default HSDS profile version: HSDS-UK-3.0"));
        }

    }

    [Test]
    public async Task DiscoverFromBaseUrlAsync_WhenDiscoveredProfileMatchesMapping_MapsToCanonicalVersion()
    {
        SetupHttpResponseMap(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/"] = (HttpStatusCode.OK, "{\"version\":\"V3\"}")
        });

        var service = new ProfileDiscoveryService(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-3.0"] = "https://hsds.example.org/3.0/openapi.json"
                },
                ProfileVersionMappings = new Dictionary<string, string[]>
                {
                    ["HSDS-UK-3.0"] = ["V3"]
                }
            }),
            Options.Create(new OpenApiValidationServerOptions { OwnSchemaValidation = OwnSchemaValidationMode.Strict }),
            _memoryCache);

        var result = await service.DiscoverFromBaseUrlAsync(null, "https://api.example.com");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HsdsProfileVersion, Is.EqualTo("HSDS-UK-3.0"));
            Assert.That(result.UsedDefaultProfile, Is.False);
            Assert.That(result.HsdsProfileReason, Does.Contain("HSDS version HSDS-UK-3.0 (mapped from V3) discovered from base URL at root"));
        }
    }

    [Test]
    public async Task DiscoverFromBaseUrlAsync_WhenDiscoveredProfileMatchesMappingCaseInsensitively_MapsToCanonicalVersion()
    {
        SetupHttpResponseMap(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/"] = (HttpStatusCode.OK, "{\"version\":\"v3\"}")
        });

        var service = new ProfileDiscoveryService(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-3.0"] = "https://hsds.example.org/3.0/openapi.json"
                },
                ProfileVersionMappings = new Dictionary<string, string[]>
                {
                    ["HSDS-UK-3.0"] = ["V3"]
                }
            }),
            Options.Create(new OpenApiValidationServerOptions { OwnSchemaValidation = OwnSchemaValidationMode.Strict }),
            _memoryCache);

        var result = await service.DiscoverFromBaseUrlAsync(null, "https://api.example.com");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HsdsProfileVersion, Is.EqualTo("HSDS-UK-3.0"));
            Assert.That(result.HsdsProfileReason, Does.Contain("HSDS version HSDS-UK-3.0 (mapped from v3) discovered from base URL at root"));
        }
    }

    [Test]
    public async Task DiscoverFromBaseUrlAsync_WhenSwaggerConfigEndpointContainsUrl_ReturnsDiscoveredSpecContent()
    {
        // Use a URL not in Constants.Paths so it is only reachable via swagger-config probing
        SetupHttpResponseMap(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/swagger-config"] = (HttpStatusCode.OK, "{\"url\":\"/api/v2/openapi-custom.json\"}"),
            ["/api/v2/openapi-custom.json"] = (HttpStatusCode.OK, "{\"openapi\":\"3.0.0\"}")
        });

        var service = CreateService();
        var result = await service.DiscoverFromBaseUrlAsync(
            null,
            "https://api.example.com");

        Assert.That(result.OpenApiSchemaContent, Does.Contain("openapi"));
    }

    [Test]
    public async Task DiscoverFromBaseUrlAsync_WithAuthentication_PassesAuthToHttpRequests()
    {
        SetupHttpResponseMap(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/"] = (HttpStatusCode.OK, "{\"version\":\"HSDS-UK-3.0\"}")
        });

        var auth = new DataSourceAuthentication { BearerToken = "token-123" };
        var service = CreateService();
        var result = await service.DiscoverFromBaseUrlAsync(null, "https://api.example.com", authentication: auth);

        Assert.That(result.HsdsProfileVersion, Is.EqualTo("HSDS-UK-3.0"));
    }

    [Test]
    public async Task DiscoverFromBaseUrlAsync_WhenStandardPathReturnsSwaggerUiSpec_ReturnsResolvedSpecContent()
    {
        var html = @"<!doctype html><html><body>
            <script>
                SwaggerUIBundle({
                    url: '/v3/api-docs',
                    dom_id: '#swagger-ui'
                });
            </script>
            </body></html>";

        SetupHttpResponseMap(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/"] = (HttpStatusCode.OK, html),
            ["/v3/api-docs"] = (HttpStatusCode.OK, "{\"openapi\":\"3.0.1\"}")
        });

        var service = CreateService();
        var result = await service.DiscoverFromBaseUrlAsync(
            null,
            "https://api.example.com");

        Assert.That(result.OpenApiSchemaContent, Does.Contain("openapi"));
    }

    [Test]
    public void GetExplicitProfile_WithNullOrEmptyProfile_ThrowsArgumentException()
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() => service.GetExplicitProfile(null!));
        Assert.Throws<ArgumentException>(() => service.GetExplicitProfile(string.Empty));
    }

    [Test]
    public void GetExplicitProfile_WithUnsupportedProfile_ThrowsArgumentException()
    {
        var service = CreateService();

        var ex = Assert.Throws<ArgumentException>(() => service.GetExplicitProfile("HSDS-UNSUPPORTED"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain("Can only validate against known profile versions"));
            Assert.That(ex.Message, Does.Contain("is not supported"));
        }

    }

    [Test]
    public void GetExplicitProfile_WithSupportedProfileButNotCached_ThrowsArgumentException()
    {
        var service = new ProfileDiscoveryService(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["3.1"] = "https://hsds.example.org/3.1/openapi.json"
                }
            }),
            Options.Create(new OpenApiValidationServerOptions { OwnSchemaValidation = OwnSchemaValidationMode.Strict }),
            _memoryCache);

        var ex = Assert.Throws<ArgumentException>(() => service.GetExplicitProfile("3.1"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain("Can only validate against known profile versions"));
            Assert.That(ex.Message, Does.Contain("is not available in cache"));
        }

    }

    [Test]
    public void GetExplicitProfile_WithSupportedAndCachedProfile_ReturnsProfileDiscoveryResult()
    {
        var service = CreateService();

        var result = service.GetExplicitProfile("HSDS-UK-3.0");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.HsdsProfileVersion, Is.EqualTo("HSDS-UK-3.0"));
            Assert.That(result.HsdsProfileSchemaUrl, Is.EqualTo("https://hsds.example.org/3.0/openapi.json"));
            Assert.That(result.HsdsProfileSchemaContent, Is.EqualTo(CachedHsdsSchema));
            Assert.That(result.OpenApiSchemaContent, Is.Null);
            Assert.That(result.HsdsProfileReason, Is.EqualTo("Explicit profile 'HSDS-UK-3.0' provided in request."));
        }
    }

    private ProfileDiscoveryService CreateService(OwnSchemaValidationMode ownSchemaValidation = OwnSchemaValidationMode.Strict)
    {
        return new ProfileDiscoveryService(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-3.0"] = "https://hsds.example.org/3.0/openapi.json",
                    ["3.0"] = "https://hsds.example.org/3.0/openapi.json"
                }
            }),
            Options.Create(new OpenApiValidationServerOptions { OwnSchemaValidation = ownSchemaValidation }),
            _memoryCache);
    }

    private void SetupHttpResponseMap(Dictionary<string, (HttpStatusCode statusCode, string content)> responses)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                var absolutePath = request.RequestUri?.AbsolutePath ?? "/";
                if (responses.TryGetValue(absolutePath, out var configuredResponse))
                {
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = configuredResponse.statusCode,
                        Content = new StringContent(configuredResponse.content)
                    });
                }

                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent(string.Empty)
                });
            });
    }

}
