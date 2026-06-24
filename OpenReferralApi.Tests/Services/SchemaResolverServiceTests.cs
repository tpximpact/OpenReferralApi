using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class SchemaResolverServiceTests
{
    private Mock<ILogger<SchemaResolverService>> _loggerMock;
    private MemoryCache _memoryCache;
    private IOptions<CacheOptions> _cacheOptions;
    private SchemaResolverService _service;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<SchemaResolverService>>();

        // Create real MemoryCache for testing
        _memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 100 * 1024 * 1024 // 100 MB
        });

        // Create cache options with caching disabled for most tests
        _cacheOptions = Options.Create(new CacheOptions
        {
            Enabled = false, // Disabled by default to not affect existing tests
            ExpirationMinutes = 60,
            MaxSizeMB = 100
        });

        _service = new SchemaResolverService(CreateFactory(TestHttpClientFactory.CreateClient()), _loggerMock.Object, _memoryCache, _cacheOptions);
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache.Dispose();
    }

    #region System.Text.Json ResolveAsync Tests

    [Test]
    public async Task ResolveAsync_WithStringInput_ResolvesReferences()
    {
        // Arrange
        var schemaJson = @"
        {
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" }
            }
        }";

        // Act
        var result = await _service.ResolveAsync(schemaJson);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("object"));
        }
    }

    [Test]
    public async Task ResolveAsync_WithJsonNode_ResolvesReferences()
    {
        // Arrange
        var schemaJson = @"
        {
            ""type"": ""object"",
            ""properties"": {
                ""id"": { ""type"": ""string"" }
            }
        }";
        var jsonNode = JsonNode.Parse(schemaJson);
        Assert.That(jsonNode, Is.Not.Null);

        // Act
        var result = await _service.ResolveAsync(jsonNode!);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result?["type"]?.GetValue<string>(), Is.EqualTo("object"));
        }
    }

    [Test]
    public async Task ResolveAsync_WithInternalRef_ResolvesJsonPointer()
    {
        // Arrange
        var schemaJson = @"
        {
            ""type"": ""object"",
            ""properties"": {
                ""config"": { ""$ref"": ""#/definitions/Config"" }
            },
            ""definitions"": {
                ""Config"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""setting"": { ""type"": ""string"" }
                    }
                }
            }
        }";
        var jsonNode = JsonNode.Parse(schemaJson);
        Assert.That(jsonNode, Is.Not.Null);

        // Act
        var result = await _service.ResolveAsync(jsonNode!);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    #endregion

    #region Json.Schema CreateSchemaFromJsonAsync Tests

    [Test]
    public async Task CreateSchemaFromJsonAsync_WithValidSchema_ReturnsSchema()
    {
        // Arrange
        var schemaJson = @"
        {
            ""type"": ""object"",
            ""properties"": {
                ""name"": { ""type"": ""string"" },
                ""age"": { ""type"": ""integer"" }
            },
            ""required"": [""name""]
        }";

        // Act
        var result = await _service.CreateSchemaFromJsonAsync(schemaJson, null);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(System.Text.Json.JsonSerializer.Serialize(result), Does.Contain("object"));
            Assert.That(System.Text.Json.JsonSerializer.Serialize(result), Does.Contain("name"));
            Assert.That(System.Text.Json.JsonSerializer.Serialize(result), Does.Contain("age"));
        }
    }

    [Test]
    public async Task CreateSchemaFromJsonAsync_WithDocumentUri_CreatesSchemaWithBaseUri()
    {
        // Arrange
        var schemaJson = @"
        {
            ""type"": ""object"",
            ""properties"": {
                ""id"": { ""type"": ""string"" }
            }
        }";
        var documentUri = "https://example.com/schemas/test.json";

        // Act
        var result = await _service.CreateSchemaFromJsonAsync(schemaJson, documentUri);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(System.Text.Json.JsonSerializer.Serialize(result), Does.Contain("object"));
        }
    }

    [Test]
    public async Task CreateSchemaFromJsonAsync_WithOfflineRef_HandlesReference()
    {
        // Arrange
        var schemaJson = @"
        {
            ""type"": ""object"",
            ""properties"": {
                ""config"": { ""$ref"": ""#/definitions/Config"" }
            },
            ""definitions"": {
                ""Config"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""setting"": { ""type"": ""string"" }
                    }
                }
            }
        }";

        // Act
        var result = await _service.CreateSchemaFromJsonAsync(schemaJson);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task CreateSchemaFromJsonAsync_WithDraft202012MetaSchema_ParsesSuccessfully()
    {
        // Arrange
        var rootSchemaJson = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://example.com/draft/2020-12/schema",
          "$dynamicAnchor": "meta",
          "allOf": [
            { "$ref": "meta/core" }
          ],
          "type": ["object", "boolean"]
        }
        """;

        var coreMetaJson = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://example.com/draft/2020-12/meta/core",
          "$dynamicAnchor": "meta",
          "type": ["object", "boolean"],
          "properties": {
            "$defs": {
              "type": "object",
              "additionalProperties": { "$dynamicRef": "#meta" }
            }
          }
        }
        """;

        var handler = new MockHttpMessageHandler(async request =>
        {
            var uri = request.RequestUri?.GetLeftPart(UriPartial.Path);
            if (uri == "https://example.com/draft/2020-12/meta/core")
            {
                return new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent(coreMetaJson)
                };
            }

            return new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.NotFound };
        });

        using var httpClient = TestHttpClientFactory.CreateClient(handler);
        var service = new SchemaResolverService(CreateFactory(httpClient), _loggerMock.Object, _memoryCache, _cacheOptions);

        // Act
        var result = await service.CreateSchemaFromJsonAsync(rootSchemaJson, "https://example.com/draft/2020-12/schema");

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    #endregion

    #region Cache Tests

    [Test]
    public async Task LoadRemoteSchemaAsync_WithCacheEnabled_UsesCache()
    {
        // Arrange
        var schemaUrl = "https://example.com/schema.json";
        var schemaJson = @"{""type"": ""object""}";

        // Create service with caching enabled
        var cacheOptions = Options.Create(new CacheOptions
        {
            Enabled = true,
            ExpirationMinutes = 60
        });

        var memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 100 * 1024 * 1024
        });

        var handler = new MockHttpMessageHandler(async request =>
        {
            if (request.RequestUri?.ToString() == schemaUrl)
            {
                return new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent(schemaJson)
                };
            }
            return new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.NotFound };
        });

        var httpClient = TestHttpClientFactory.CreateClient(handler);
        var service = new SchemaResolverService(CreateFactory(httpClient), _loggerMock.Object, memoryCache, cacheOptions);

        // Create a simple schema with external ref
        var mainSchemaJson = @"{
      ""type"": ""object"",
      ""properties"": {
        ""ref"": { ""$ref"": """ + schemaUrl + @""" }
      }
    }";

        // Act - First call should fetch from HTTP
        var result1 = await service.ResolveAsync(mainSchemaJson, "https://example.com/");

        // Act - Second call should use cache (we can verify this by checking logs or cache state)
        var result2 = await service.ResolveAsync(mainSchemaJson, "https://example.com/");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result1, Is.Not.Null);
            Assert.That(result2, Is.Not.Null);

            // Verify cache contains the schema
            var cacheKey = $"schema:{schemaUrl}";
            Assert.That(memoryCache.TryGetValue(cacheKey, out CachedSchema? _), Is.True);
        }
    }

    [Test]
    public async Task LoadRemoteSchemaAsync_WithCacheDisabled_SkipsCache()
    {
        // Arrange
        var schemaUrl = "https://example.com/schema.json";
        var schemaJson = @"{""type"": ""object""}";

        // Create service with caching disabled
        var cacheOptions = Options.Create(new CacheOptions
        {
            Enabled = false
        });

        var memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 100 * 1024 * 1024
        });

        var handler = new MockHttpMessageHandler(async request =>
        {
            if (request.RequestUri?.ToString() == schemaUrl)
            {
                return new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent(schemaJson)
                };
            }
            return new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.NotFound };
        });

        var httpClient = TestHttpClientFactory.CreateClient(handler);
        var service = new SchemaResolverService(CreateFactory(httpClient), _loggerMock.Object, memoryCache, cacheOptions);

        // Create a simple schema with external ref
        var mainSchemaJson = @"{
      ""type"": ""object"",
      ""properties"": {
        ""ref"": { ""$ref"": """ + schemaUrl + @""" }
      }
    }";

        // Act
        var result = await service.ResolveAsync(mainSchemaJson, "https://example.com/");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);

            // Verify cache does not contain the schema
            var cacheKey = $"schema:{schemaUrl}";
            Assert.That(memoryCache.TryGetValue(cacheKey, out CachedSchema? _), Is.False);
        }
    }

    [Test]
    public async Task ResolveAsync_WithNullAuth_DoesNotApplyAuthenticationHeaders()
    {
        // Arrange
        var schemaUrl = "https://example.com/schema.json";
        var schemaJson = @"{""type"": ""object""}";
        HttpRequestMessage? capturedRequest = null;

        var handler = new MockHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(schemaJson)
            };
        });

        using var httpClient = TestHttpClientFactory.CreateClient(handler);
        var service = new SchemaResolverService(CreateFactory(httpClient), _loggerMock.Object, _memoryCache, _cacheOptions);

        var mainSchemaJson = @"{
      ""type"": ""object"",
      ""properties"": {
        ""ref"": { ""$ref"": """ + schemaUrl + @""" }
      }
    }";

        // Act
        var result = await service.ResolveAsync(mainSchemaJson, "https://example.com/", null);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.Headers.Authorization, Is.Null);
            Assert.That(capturedRequest.Headers.Contains("X-API-Key"), Is.False);
        }
    }

    [Test]
    public async Task ResolveAsync_WithApiKeyAuth_AppliesApiKeyHeader()
    {
        // Arrange
        var schemaUrl = "https://example.com/schema.json";
        var schemaJson = @"{""type"": ""object""}";
        HttpRequestMessage? capturedRequest = null;

        var handler = new MockHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(schemaJson)
            };
        });

        using var httpClient = TestHttpClientFactory.CreateClient(handler);
        var service = new SchemaResolverService(CreateFactory(httpClient), _loggerMock.Object, _memoryCache, _cacheOptions);

        var mainSchemaJson = @"{
      ""type"": ""object"",
      ""properties"": {
        ""ref"": { ""$ref"": """ + schemaUrl + @""" }
      }
    }";

        var auth = new DataSourceAuthentication
        {
            ApiKey = "test-api-key",
            ApiKeyHeader = "X-Test-Api-Key"
        };

        // Act
        var result = await service.ResolveAsync(mainSchemaJson, "https://example.com/", auth);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.Headers.Contains("X-Test-Api-Key"), Is.True);
            Assert.That(capturedRequest.Headers.TryGetValues("X-Test-Api-Key", out var values), Is.True);
            Assert.That(values, Is.Not.Null);
            Assert.That(values!.Single(), Is.EqualTo("test-api-key"));
        }
    }

#endregion

    private static IHttpClientFactory CreateFactory(HttpClient httpClient)
    {
        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        return mock.Object;
    }

    /// <summary>
    /// Mock HTTP message handler for testing
    /// </summary>
    private class MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
