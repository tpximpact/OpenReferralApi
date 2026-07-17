using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class ReferenceResolverTests
{
    private Mock<ILogger<SchemaResolverService>> _loggerMock;
    private MemoryCache _memoryCache;
    private IOptions<CacheOptions> _cacheOptions;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<SchemaResolverService>>();

        _memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 100 * 1024 * 1024 // 100 MB
        });

        _cacheOptions = Options.Create(new CacheOptions
        {
            Enabled = false,
            ExpirationMinutes = 60,
            UseSlidingExpiration = true,
            SlidingExpirationMinutes = 60
        });
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache?.Dispose();
    }

    [Test]
    public async Task ResolveNodeRefAsync_WithValidInternalPointer_ResolvesCorrectly()
    {
        // Arrange
        var schema = @"{
            ""definitions"": {
                ""User"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""name"": { ""type"": ""string"" }
                    }
                }
            }
        }";

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(@"{""type"": ""object""}")
            };
        });

        var httpClientFactory = TestHttpClientFactory.CreateFactory(handler);
        var loader = new RemoteSchemaLoader(httpClientFactory, _loggerMock.Object, _memoryCache, _cacheOptions);
        var resolver = new ReferenceResolver(_loggerMock.Object, loader);

        var rootDoc = JsonNode.Parse(schema);
        resolver.Initialize(rootDoc, "https://example.com/");

        // Act
        var result = await resolver.ResolveNodeRefAsync("#/definitions/User");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            var resultObj = result!.AsObject();
            Assert.That(resultObj["type"]!.GetValue<string>(), Is.EqualTo("object"));
            Assert.That(resultObj.ContainsKey("properties"), Is.True);
        }
    }

    [Test]
    public async Task ResolveNodeRefAsync_WithArrayIndex_ResolvesCorrectly()
    {
        // Arrange
        var schema = @"{
            ""items"": [
                { ""type"": ""string"" },
                { ""type"": ""number"" }
            ]
        }";

        var httpClientFactory = TestHttpClientFactory.CreateFactory(new MockHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage())));
        var loader = new RemoteSchemaLoader(httpClientFactory, _loggerMock.Object, _memoryCache, _cacheOptions);
        var resolver = new ReferenceResolver(_loggerMock.Object, loader);

        var rootDoc = JsonNode.Parse(schema);
        resolver.Initialize(rootDoc, "https://example.com/");

        // Act
        var result = await resolver.ResolveNodeRefAsync("#/items/1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            var resultObj = result?.AsObject();
            Assert.That(resultObj?["type"]?.GetValue<string>(), Is.EqualTo("number"));
        }
    }

    [Test]
    public async Task ResolveNodeRefAsync_WithEscapedPointer_UnescapesCorrectly()
    {
        // Arrange
        var schema = @"{
            ""definitions"": {
                ""field~name/path"": {
                    ""type"": ""string""
                }
            }
        }";

        var httpClientFactory = TestHttpClientFactory.CreateFactory(new MockHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage())));
        var loader = new RemoteSchemaLoader(httpClientFactory, _loggerMock.Object, _memoryCache, _cacheOptions);
        var resolver = new ReferenceResolver(_loggerMock.Object, loader);

        var rootDoc = JsonNode.Parse(schema);
        resolver.Initialize(rootDoc, "https://example.com/");

        // Act
        var result = await resolver.ResolveNodeRefAsync("#/definitions/field~0name~1path");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            var resultObj = result?.AsObject();
            Assert.That(resultObj?["type"]?.GetValue<string>(), Is.EqualTo("string"));
        }
    }

    [Test]
    public async Task ResolveNodeRefAsync_WithAnchor_ResolvesCorrectly()
    {
        // Arrange
        var schema = """
        {
            "$defs": {
                "User": {
                    "$anchor": "userAnchor",
                    "type": "object",
                    "properties": {
                        "name": { "type": "string" }
                    }
                }
            }
        }
        """;

        var httpClientFactory = TestHttpClientFactory.CreateFactory(new MockHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage())));
        var loader = new RemoteSchemaLoader(httpClientFactory, _loggerMock.Object, _memoryCache, _cacheOptions);
        var resolver = new ReferenceResolver(_loggerMock.Object, loader);

        var rootDoc = JsonNode.Parse(schema);
        resolver.Initialize(rootDoc, "https://example.com/");

        // Act
        var result = await resolver.ResolveNodeRefAsync("#userAnchor");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            var resultObj = result?.AsObject();
            Assert.That(resultObj?["type"]?.GetValue<string>(), Is.EqualTo("object"));
        }
    }

    [Test]
    public async Task ResolveNodeRefAsync_WithCircularReference_ReturnsNullAndAddsIssue()
    {
        // Arrange
        var schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""self"": { ""$ref"": ""#/properties/self"" }
            }
        }";

        var httpClientFactory = TestHttpClientFactory.CreateFactory(new MockHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage())));
        var loader = new RemoteSchemaLoader(httpClientFactory, _loggerMock.Object, _memoryCache, _cacheOptions);
        var resolver = new ReferenceResolver(_loggerMock.Object, loader);

        var rootDoc = JsonNode.Parse(schema);
        resolver.Initialize(rootDoc, "https://example.com/");

        // Act
        var result = await resolver.ResolveNodeRefAsync("#/properties/self");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null, "Circular lookup should return null to break loop");
            Assert.That(resolver.ResolutionIssues, Has.Count.GreaterThan(0));
            Assert.That(resolver.ResolutionIssues[0].ErrorCode, Is.EqualTo("CIRCULAR_SCHEMA_REFERENCE"));
        }
    }

    [Test]
    public async Task ResolveNodeRefAsync_WithRelativeLocalFileRef_ResolvesCorrectly()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"openreferral-ref-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var definitionsPath = Path.Combine(tempDirectory, "definitions.json");
            await File.WriteAllTextAsync(definitionsPath, """
            {
                "$defs": {
                    "name": {
                        "type": "string",
                        "minLength": 1
                    }
                }
            }
            """);

            var schema = """
            {
                "type": "object"
            }
            """;

            var httpClientFactory = TestHttpClientFactory.CreateFactory(new MockHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage())));
            var loader = new RemoteSchemaLoader(httpClientFactory, _loggerMock.Object, _memoryCache, _cacheOptions);
            var resolver = new ReferenceResolver(_loggerMock.Object, loader);

            var rootDoc = JsonNode.Parse(schema);
            var rootSchemaPath = Path.Combine(tempDirectory, "service.json");
            resolver.Initialize(rootDoc, rootSchemaPath);

            // Act
            var result = await resolver.ResolveNodeRefAsync("./definitions.json#/$defs/name");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                var nameSchema = result!.AsObject();
                Assert.That(nameSchema["type"]!.GetValue<string>(), Is.EqualTo("string"));
                Assert.That(nameSchema["minLength"]!.GetValue<int>(), Is.EqualTo(1));
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private class MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>>? handler = null) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler = handler ?? (req => Task.FromResult(new HttpResponseMessage()));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
