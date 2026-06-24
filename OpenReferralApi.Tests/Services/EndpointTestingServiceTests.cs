using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenReferralApi.Core.Services;
using System.Text.Json.Nodes;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class EndpointTestingServiceTests
{
  private Mock<ILogger<EndpointTestingService>> _loggerMock = null!;
  private Mock<IJsonValidatorService> _jsonValidatorServiceMock = null!;
  private Mock<IHsdsComplianceService> _hsdsComplianceServiceMock = null!;
  private HttpClient _httpClient = null!;
  private EndpointTestingService _service = null!;

  [SetUp]
  public void Setup()
  {
    _loggerMock = new Mock<ILogger<EndpointTestingService>>();
    _jsonValidatorServiceMock = new Mock<IJsonValidatorService>();
    _hsdsComplianceServiceMock = new Mock<IHsdsComplianceService>();

    _jsonValidatorServiceMock
        .Setup(x => x.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ValidationResult
        {
          IsValid = true,
          Errors = [],
          SchemaVersion = "test",
          Duration = TimeSpan.Zero
        });

    _hsdsComplianceServiceMock
      .Setup(x => x.ApplyAdditionalFieldPolicy(It.IsAny<ValidationResult?>(), It.IsAny<bool>()));

    SetupService((_, __) => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
    {
      Content = new StringContent("{}")
    });
  }

  [TearDown]
  public void TearDown()
  {
    _httpClient.Dispose();
  }

  [Test]
  public async Task TestEndpointsAsync_LogsUnifiedMemoryCheckpointPayload()
  {
    var results = await _service.TestEndpointsAsync(
      CreateRequiredEndpointSpec(),
      "https://api.example.com",
      new OpenApiValidationOptions(),
      null,
      CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(1));
      Assert.That(HasInformationLogContaining(_loggerMock, "Memory checkpoint EndpointTestingService/start."), Is.True);
      Assert.That(HasInformationLogContaining(_loggerMock, "ManagedHeapBytes:"), Is.True);
      Assert.That(HasInformationLogContaining(_loggerMock, "GcHeapSizeBytes:"), Is.True);
    }
  }

  [Test]
  public async Task TestEndpointsAsync_ParameterizedEndpointWithoutExtractedIds_ReturnsNotTestedWarning()
  {
    var spec = CreateParameterizedOnlySpec();

    var results = await _service.TestEndpointsAsync(
        spec,
        "https://api.example.com",
        new OpenApiValidationOptions(),
        null,
        CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(1));
      var endpoint = results[0];
      Assert.That(endpoint.Status, Is.EqualTo(EndpointTestStatus.NotTested));
      Assert.That(endpoint.TestResults, Has.Count.EqualTo(1));
      Assert.That(endpoint.TestResults[0].ValidationResult, Is.Not.Null);
      Assert.That(endpoint.TestResults[0].ValidationResult!.Errors,
          Has.Some.Matches<ValidationError>(e => e.ErrorCode == "NO_IDS_AVAILABLE"));
    }
  }

  [Test]
  public async Task TestEndpointsAsync_CollectionThenParameterized_UsesExtractedIdsAndPasses()
  {
    SetupService((request, _) =>
    {
      var uri = request.RequestUri!.ToString();
      if (uri.EndsWith("/services", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent("{\"data\":[{\"id\":\"1\"},{\"id\":\"2\"}]}")
        };
      }

      if (uri.Contains("/services/1", StringComparison.OrdinalIgnoreCase) ||
              uri.Contains("/services/2", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent("{\"id\":\"ok\"}")
        };
      }

      return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
      {
        Content = new StringContent("{}")
      };
    });

    var results = await _service.TestEndpointsAsync(
        CreateCollectionAndParameterizedSpec(),
        "https://api.example.com",
        new OpenApiValidationOptions(),
        null,
        CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(2));
      var parameterized = results.Single(r => r.Path == "/services/{id}");
      Assert.That(parameterized.Status, Is.EqualTo(EndpointTestStatus.PassedValidation));
      Assert.That(parameterized.TestResults, Has.Count.EqualTo(2));
      Assert.That(parameterized.TestResults.All(r => !string.IsNullOrWhiteSpace(r.TestedId)), Is.True);
    }
  }

  [Test]
  public async Task TestEndpointsAsync_CollectionWithRefThenParameterized_UsesExtractedIdsAndPasses()
  {
    SetupService((request, _) =>
    {
      var uri = request.RequestUri!.ToString();
      if (uri.EndsWith("/services", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent("{\"content\":[{\"id\":\"1\"},{\"id\":\"2\"}]}")
        };
      }

      if (uri.Contains("/services/1", StringComparison.OrdinalIgnoreCase) ||
              uri.Contains("/services/2", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent("{\"id\":\"ok\"}")
        };
      }

      return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
      {
        Content = new StringContent("{}")
      };
    });

    var results = await _service.TestEndpointsAsync(
        CreateCollectionWithRefAndParameterizedSpec(),
        "https://api.example.com",
        new OpenApiValidationOptions(),
        null,
        CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(2));
      var parameterized = results.Single(r => r.Path == "/services/{id}");
      Assert.That(parameterized.Status, Is.EqualTo(EndpointTestStatus.PassedValidation));
      Assert.That(parameterized.TestResults, Has.Count.EqualTo(2));
      Assert.That(parameterized.TestResults.All(r => !string.IsNullOrWhiteSpace(r.TestedId)), Is.True);
    }
  }

  [Test]
  public async Task TestEndpointsAsync_CollectionWithRefAndCustomPropThenParameterized_UsesExtractedIdsAndPasses()
  {
    SetupService((request, _) =>
    {
      var uri = request.RequestUri!.ToString();
      if (uri.EndsWith("/services", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent("{\"services\":[{\"service_id\":\"1\"},{\"service_id\":\"2\"}]}")
        };
      }

      if (uri.Contains("/services/1", StringComparison.OrdinalIgnoreCase) ||
              uri.Contains("/services/2", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent("{\"id\":\"ok\"}")
        };
      }

      return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
      {
        Content = new StringContent("{}")
      };
    });

    var results = await _service.TestEndpointsAsync(
        CreateCollectionWithRefAndCustomPropAndParameterizedSpec(),
        "https://api.example.com",
        new OpenApiValidationOptions(),
        null,
        CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(2));
      var parameterized = results.Single(r => r.Path == "/services/{id}");
      Assert.That(parameterized.Status, Is.EqualTo(EndpointTestStatus.PassedValidation));
      Assert.That(parameterized.TestResults, Has.Count.EqualTo(2));
      Assert.That(parameterized.TestResults.All(r => !string.IsNullOrWhiteSpace(r.TestedId)), Is.True);
    }
  }

  [Test]
  public async Task TestEndpointsAsync_CqcLikeSpec_UsesExtractedIdsAndPasses()
  {
    SetupService((request, _) =>
    {
      var uri = request.RequestUri!.ToString();
      if (uri.EndsWith("/services", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent("{\"totalElements\":2,\"totalPages\":1,\"number\":1,\"size\":2,\"first\":true,\"last\":false,\"content\":[{\"id\":\"1\",\"name\":\"Service 1\",\"status\":\"active\"},{\"id\":\"2\",\"name\":\"Service 2\",\"status\":\"active\"}]}")
        };
      }

      if (uri.Contains("/services/1", StringComparison.OrdinalIgnoreCase) ||
              uri.Contains("/services/2", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent("{\"id\":\"ok\"}")
        };
      }

      return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
      {
        Content = new StringContent("{}")
      };
    });

    var results = await _service.TestEndpointsAsync(
        CreateCqcLikeSpec(),
        "https://api.example.com",
        new OpenApiValidationOptions(),
        null,
        CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(2));
      var parameterized = results.Single(r => r.Path == "/services/{id}");
      Assert.That(parameterized.Status, Is.EqualTo(EndpointTestStatus.PassedValidation));
      Assert.That(parameterized.TestResults, Has.Count.EqualTo(2));
      Assert.That(parameterized.TestResults.All(r => !string.IsNullOrWhiteSpace(r.TestedId)), Is.True);
    }
  }

  [Test]
  public async Task TestEndpointsAsync_OptionalEndpoint404_ReturnsPassedWithWarnings()
  {
    SetupService((request, _) =>
    {
      if (request.RequestUri!.ToString().Contains("/optional", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
        {
          Content = new StringContent("{}")
        };
      }

      return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
      {
        Content = new StringContent("{}")
      };
    });

    var results = await _service.TestEndpointsAsync(
        CreateOptionalEndpointSpec(),
        "https://api.example.com",
        new OpenApiValidationOptions(),
        null,
        CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(1));
      var endpoint = results[0];
      Assert.That(endpoint.Status, Is.EqualTo(EndpointTestStatus.PassedWithWarnings));
      Assert.That(endpoint.TestResults[0].ValidationResult!.Errors,
          Has.Some.Matches<ValidationError>(e => e.ErrorCode == "OPTIONAL_ENDPOINT_NON_SUCCESS"));
    }
  }

  [Test]
  public async Task TestEndpointsAsync_RequiredEndpoint404_ReturnsFailedValidation()
  {
    SetupService((_, __) => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
    {
      Content = new StringContent("{}")
    });

    var results = await _service.TestEndpointsAsync(
        CreateRequiredEndpointSpec(),
        "https://api.example.com",
        new OpenApiValidationOptions(),
        null,
        CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(1));
      var endpoint = results[0];
      Assert.That(endpoint.Status, Is.EqualTo(EndpointTestStatus.FailedValidation));
      Assert.That(endpoint.TestResults[0].ValidationResult!.Errors,
          Has.Some.Matches<ValidationError>(e => e.ErrorCode == "REQUIRED_ENDPOINT_FAILED"));
    }
  }

  [Test]
  public async Task TestEndpointsAsync_ResponseSchemaWithComponentsRef_WrapsSchemaWithComponentsContext()
  {
    ValidationRequest? capturedValidationRequest = null;

    _jsonValidatorServiceMock
      .Setup(x => x.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
      .Callback<ValidationRequest, CancellationToken>((request, _) => capturedValidationRequest = request)
      .ReturnsAsync(new ValidationResult
      {
        IsValid = true,
        Errors = [],
        SchemaVersion = "test",
        Duration = TimeSpan.Zero
      });

    var results = await _service.TestEndpointsAsync(
      CreateSpecWithComponentRefResponseSchema(),
      "https://api.example.com",
      new OpenApiValidationOptions(),
      null,
      CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(1));
      Assert.That(capturedValidationRequest, Is.Not.Null);
      Assert.That(capturedValidationRequest!.Schema, Is.TypeOf<JsonSchema>());

      var schema = (JsonSchema)capturedValidationRequest.Schema!;
      var schemaJson = System.Text.Json.JsonSerializer.Serialize(schema);
      Assert.That(schemaJson, Does.Contain("\"components\""));
      Assert.That(schemaJson, Does.Not.Contain("x-validation-schema"));
      Assert.That(schemaJson, Does.Contain("#/components/schemas/Service"));
    }
  }

  [Test]
  public async Task TestEndpointsAsync_PaginatedEndpointEmptyFeed_ReturnsWarning()
  {
    SetupService((request, _) =>
    {
      if (request.RequestUri!.ToString().Contains("/services?page=1", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent("{\"total_pages\":3,\"data\":[]}")
        };
      }

      return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
      {
        Content = new StringContent("{}")
      };
    });

    var results = await _service.TestEndpointsAsync(
        CreatePaginatedCollectionSpec(),
        "https://api.example.com",
        new OpenApiValidationOptions(),
        null,
        CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(1));
      var endpoint = results[0];
      Assert.That(endpoint.Status, Is.EqualTo(EndpointTestStatus.PassedWithWarnings));
      Assert.That(endpoint.TestResults[0].ValidationResult!.Errors,
          Has.Some.Matches<ValidationError>(e => e.ErrorCode == "EMPTY_FEED_WARNING"));
    }
  }

  [Test]
  public async Task TestEndpointsAsync_CollectionWithMoreThanTenIds_TestsAtMostTenParameterizedRequests()
  {
    var ids = Enumerable.Range(1, 11)
      .Select(i => $"{{\"id\":\"{i}\"}}")
      .ToArray();

    SetupService((request, _) =>
    {
      var uri = request.RequestUri!.ToString();
      if (uri.EndsWith("/services", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent($"{{\"data\":[{string.Join(",", ids)}]}}")
        };
      }

      return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
      {
        Content = new StringContent("{\"id\":\"ok\"}")
      };
    });

    var results = await _service.TestEndpointsAsync(
      CreateCollectionAndParameterizedSpec(),
      "https://api.example.com",
      new OpenApiValidationOptions(),
      null,
      CancellationToken.None);

    var parameterized = results.Single(r => r.Path == "/services/{id}");
    Assert.That(parameterized.TestResults, Has.Count.EqualTo(10));
  }

  [Test]
  public async Task TestEndpointsAsync_OptionalParameterizedEndpoint_WhenDisabled_IsSkipped()
  {
    SetupService((request, _) =>
    {
      var uri = request.RequestUri!.ToString();
      if (uri.EndsWith("/services", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent("{\"data\":[{\"id\":\"1\"}]}")
        };
      }

      return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
      {
        Content = new StringContent("{\"id\":\"1\"}")
      };
    });

    // TestOptionalEndpoints is now server-configurable; create a service with it disabled
    var serverOptions = Options.Create(new OpenApiValidationServerOptions { TestOptionalEndpoints = false });
    var serviceWithOptionalDisabled = new EndpointTestingService(
        _loggerMock.Object,
        CreateFactory(_httpClient),
        _jsonValidatorServiceMock.Object,
        _hsdsComplianceServiceMock.Object,
        serverOptions);

    var results = await serviceWithOptionalDisabled.TestEndpointsAsync(
      CreateCollectionAndOptionalParameterizedSpec(),
      "https://api.example.com",
      new OpenApiValidationOptions(),
      null,
      CancellationToken.None);

    var parameterized = results.Single(r => r.Path == "/services/{id}");
    using (Assert.EnterMultipleScope())
    {
      Assert.That(parameterized.Status, Is.EqualTo(EndpointTestStatus.Skipped));
      Assert.That(parameterized.TestResults, Is.Empty);
    }
  }

  [Test]
  public async Task TestEndpointsAsync_AuthProvidedOnHttp_DoesNotSendAuthHeaders()
  {
    var sawApiKeyHeader = false;
    SetupService((request, _) =>
    {
      sawApiKeyHeader = request.Headers.Contains("X-API-Key");
      return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
      {
        Content = new StringContent("{}")
      };
    });

    var auth = new DataSourceAuthentication
    {
      ApiKey = "secret",
      ApiKeyHeader = "X-API-Key"
    };

    await _service.TestEndpointsAsync(
      CreateRequiredEndpointSpec(),
      "http://api.example.com",
      new OpenApiValidationOptions(),
      auth,
      CancellationToken.None);

    Assert.That(sawApiKeyHeader, Is.False);
  }

  [Test]
  public async Task TestEndpointsAsync_PaginatedEndpointWithMultiplePages_RequestsMiddleAndLastPage()
  {
    var requestedUris = new List<string>();

    SetupService((request, _) =>
    {
      var uri = request.RequestUri!.ToString();
      requestedUris.Add(uri);

      if (uri.Contains("page=1", StringComparison.OrdinalIgnoreCase))
      {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent("{\"total_pages\":4,\"data\":[{\"id\":\"1\"}]}")
        };
      }

      return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
      {
        Content = new StringContent("{\"data\":[{\"id\":\"1\"}]}")
      };
    });

    var results = await _service.TestEndpointsAsync(
      CreatePaginatedCollectionSpec(),
      "https://api.example.com",
      new OpenApiValidationOptions(),
      null,
      CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(1));
      Assert.That(requestedUris, Has.Count.EqualTo(3));
      Assert.That(requestedUris.Any(u => u.Contains("page=2", StringComparison.OrdinalIgnoreCase)), Is.True);
      Assert.That(requestedUris.Any(u => u.Contains("page=4", StringComparison.OrdinalIgnoreCase)), Is.True);
    }
  }

  [Test]
  public async Task TestEndpointsAsync_WhenHttpRequestThrows_ReturnsErrorStatus()
  {
    SetupService((_, __) => throw new HttpRequestException("boom"));

    var results = await _service.TestEndpointsAsync(
      CreateRequiredEndpointSpec(),
      "https://api.example.com",
      new OpenApiValidationOptions(),
      null,
      CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(1));
      var endpoint = results[0];
      Assert.That(endpoint.Status, Is.EqualTo(EndpointTestStatus.Error));
      Assert.That(endpoint.TestResults, Has.Count.EqualTo(1));
      Assert.That(endpoint.TestResults[0].IsSuccessStatusCode, Is.False);
      Assert.That(endpoint.TestResults[0].ErrorMessage, Does.Contain("boom"));
    }
  }

  [Test]
  public async Task TestEndpointsAsync_AuthProvidedOnHttps_SendsApiKeyHeader()
  {
    var sawApiKeyHeader = false;
    SetupService((request, _) =>
    {
      sawApiKeyHeader = request.Headers.Contains("X-API-Key");
      return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
      {
        Content = new StringContent("{}")
      };
    });

    var auth = new DataSourceAuthentication
    {
      ApiKey = "secret",
      ApiKeyHeader = "X-API-Key"
    };

    await _service.TestEndpointsAsync(
      CreateRequiredEndpointSpec(),
      "https://api.example.com",
      new OpenApiValidationOptions(),
      auth,
      CancellationToken.None);

    Assert.That(sawApiKeyHeader, Is.True);
  }

  [Test]
  public async Task TestEndpointsAsync_WhenPathsMissing_ReturnsEmptyResults()
  {
    var spec = JsonNode.Parse("""
    {
      "openapi": "3.0.0",
      "info": { "title": "Test API", "version": "1.0.0" }
    }
    """)!.AsObject();

    var results = await _service.TestEndpointsAsync(
      spec,
      "https://api.example.com",
      new OpenApiValidationOptions(),
      null,
      CancellationToken.None);

    Assert.That(results, Is.Empty);
  }

  [Test]
  public async Task TestEndpointsAsync_WhenRetentionDisabled_ResponseBodyIsNullButValidationPasses()
  {
    SetupService((_, __) => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
    {
      Content = new StringContent("{\"id\":\"abc\"}")
    });

    var results = await _service.TestEndpointsAsync(
        CreateRequiredEndpointSpec(),
        "https://api.example.com",
        new OpenApiValidationOptions { IncludeResponseBody = false },
        null,
        CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(results, Has.Count.EqualTo(1));
      var endpoint = results[0];
      Assert.That(endpoint.Status, Is.EqualTo(EndpointTestStatus.PassedValidation));
      Assert.That(endpoint.TestResults[0].ResponseBody, Is.Null);
    }
  }

  private void SetupService(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
  {
    _httpClient?.Dispose();
    _httpClient = TestHttpClientFactory.CreateClient(new DelegateHttpMessageHandler(responder));
    _service = new EndpointTestingService(
        _loggerMock.Object,
        CreateFactory(_httpClient),
        _jsonValidatorServiceMock.Object,
        _hsdsComplianceServiceMock.Object);
  }

  private static IHttpClientFactory CreateFactory(HttpClient httpClient)
  {
    var mock = new Mock<IHttpClientFactory>();
    mock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
    return mock.Object;
  }

  private static bool HasInformationLogContaining<T>(Mock<ILogger<T>> loggerMock, string expectedText)
  {
    return loggerMock.Invocations.Any(invocation =>
      invocation.Method.Name == "Log"
      && invocation.Arguments.Count >= 3
      && invocation.Arguments[0] is LogLevel logLevel
      && logLevel == LogLevel.Information
      && invocation.Arguments[2]?.ToString()?.Contains(expectedText, StringComparison.Ordinal) == true);
  }

  private static JsonObject CreateRequiredEndpointSpec()
  {
    return JsonNode.Parse("""
    {
      "openapi": "3.0.0",
      "info": { "title": "Test API", "version": "1.0.0" },
      "paths": {
        "/required": {
          "get": {
            "responses": {
              "200": {
                "description": "ok"
              }
            }
          }
        }
      }
    }
    """)!.AsObject();
  }

  private static JsonObject CreateOptionalEndpointSpec()
  {
    return JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "info": { "title": "Test API", "version": "1.0.0" },
          "paths": {
            "/optional": {
              "get": {
                "tags": ["Optional"],
                "responses": {
                  "200": {
                    "description": "ok"
                  }
                }
              }
            }
          }
        }
        """)!.AsObject();
  }

  private static JsonObject CreatePaginatedCollectionSpec()
  {
    return JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "info": { "title": "Test API", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "parameters": [
                  { "name": "page", "in": "query", "required": false, "schema": { "type": "integer" } }
                ],
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "data": {
                              "type": "array",
                              "items": {
                                "type": "object",
                                "properties": {
                                  "id": { "type": "string" }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """)!.AsObject();
  }

  private static JsonObject CreateParameterizedOnlySpec()
  {
    return JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "info": { "title": "Test API", "version": "1.0.0" },
          "paths": {
            "/services/{id}": {
              "get": {
                "parameters": [
                  { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": {
                    "description": "ok"
                  }
                }
              }
            }
          }
        }
        """)!.AsObject();
  }

  private static JsonObject CreateCollectionAndParameterizedSpec()
  {
    return JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "info": { "title": "Test API", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "data": {
                              "type": "array",
                              "items": {
                                "type": "object",
                                "properties": {
                                  "id": { "type": "string" }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            },
            "/services/{id}": {
              "get": {
                "parameters": [
                  { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": {
                    "description": "ok"
                  }
                }
              }
            }
          }
        }
        """)!.AsObject();
  }

  private static JsonObject CreateCollectionWithRefAndParameterizedSpec()
  {
    return JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "info": { "title": "Test API", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": {
                          "properties": {
                            "content": {
                              "type": "array",
                              "items": {
                                "$ref": "#/components/schemas/Service"
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            },
            "/services/{id}": {
              "get": {
                "parameters": [
                  { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": {
                    "description": "ok"
                  }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "Service": {
                "type": "object",
                "properties": {
                  "id": { "type": "string" }
                }
              }
            }
          }
        }
        """)!.AsObject();
  }

  private static JsonObject CreateCollectionWithRefAndCustomPropAndParameterizedSpec()
  {
    return JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "info": { "title": "Test API", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": {
                          "$ref": "#/components/schemas/ServicesResponse"
                        }
                      }
                    }
                  }
                }
              }
            },
            "/services/{id}": {
              "get": {
                "parameters": [
                  { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": {
                    "description": "ok"
                  }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "ServicesResponse": {
                "type": "object",
                "properties": {
                  "services": {
                    "type": "array",
                    "items": {
                      "$ref": "#/components/schemas/Service"
                    }
                  }
                }
              },
              "Service": {
                "type": "object",
                "properties": {
                  "service_id": {
                    "type": "string"
                  }
                }
              }
            }
          }
        }
        """)!.AsObject();
  }



  private static JsonObject CreateCollectionAndOptionalParameterizedSpec()
  {
    return JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "info": { "title": "Test API", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "data": {
                              "type": "array",
                              "items": {
                                "type": "object",
                                "properties": {
                                  "id": { "type": "string" }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            },
            "/services/{id}": {
              "get": {
                "tags": ["Optional"],
                "parameters": [
                  { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": {
                    "description": "ok"
                  }
                }
              }
            }
          }
        }
        """)!.AsObject();
  }

  private static JsonObject CreateSpecWithComponentRefResponseSchema()
  {
    return JsonNode.Parse("""
    {
      "openapi": "3.0.0",
      "info": { "title": "Test API", "version": "1.0.0" },
      "paths": {
        "/services": {
          "get": {
            "responses": {
              "200": {
                "description": "ok",
                "content": {
                  "application/json": {
                    "schema": {
                      "$ref": "#/components/schemas/Service"
                    }
                  }
                }
              }
            }
          }
        }
      },
      "components": {
        "schemas": {
          "Service": {
            "type": "object",
            "properties": {
              "id": { "type": "string" },
              "contact": { "$ref": "#/components/schemas/Contact" }
            }
          },
          "Contact": {
            "type": "object",
            "properties": {
              "name": { "type": "string" }
            }
          }
        }
      }
    }
    """)!.AsObject();
  }

  private static JsonObject CreateCqcLikeSpec()
  {
    return JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "info": { "title": "Test CQC API", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": {
                          "$ref": "#/components/schemas/PageService_ServiceBasicView"
                        }
                      }
                    }
                  }
                }
              }
            },
            "/services/{id}": {
              "get": {
                "parameters": [
                  { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": {
                    "description": "ok"
                  }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "PageService_ServiceBasicView": {
                "type": "object",
                "properties": {
                  "totalElements": { "type": "integer" },
                  "totalPages": { "type": "integer" },
                  "size": { "type": "integer" },
                  "content": {
                    "type": "array",
                    "items": {
                      "$ref": "#/components/schemas/Service_ServiceBasicView"
                    }
                  }
                }
              },
              "Service_ServiceBasicView": {
                "required": ["id", "name", "status"],
                "type": "object",
                "properties": {
                  "id": { "type": "string" },
                  "name": { "type": "string" },
                  "status": { "type": "string" }
                }
              }
            }
          }
        }
        """)!.AsObject();
  }

  private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder) : HttpMessageHandler
  {
    private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      return Task.FromResult(_responder(request, cancellationToken));
    }
  }
}
