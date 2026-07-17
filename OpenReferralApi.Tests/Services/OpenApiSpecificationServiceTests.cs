using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenReferralApi.Core.Services;
using System.Text.Json.Nodes;
using ValidationError = OpenReferralApi.Core.Models.Validation.ValidationError;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class OpenApiSpecificationServiceTests
{
  private Mock<ILogger<OpenApiSpecificationService>> _loggerMock = null!;
  private Mock<IJsonValidatorService> _jsonValidatorServiceMock = null!;
  private OpenApiSpecificationService _service = null!;

  [SetUp]
  public void Setup()
  {
    _loggerMock = new Mock<ILogger<OpenApiSpecificationService>>();
    _jsonValidatorServiceMock = new Mock<IJsonValidatorService>();

    _jsonValidatorServiceMock
        .Setup(x => x.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ValidationResult
        {
          IsValid = true,
          Errors = []
        });

    var schemaResolutionOptions = Options.Create(new SchemaResolutionOptions
    {
      KnownJsonSchemaUrls =
            [
                "https://json-schema.org/draft/2020-12/schema",
                "https://json-schema.org/draft/2020-12/meta/core",
                "https://json-schema.org/draft/2020-12/meta/applicator",
                "https://json-schema.org/draft/2020-12/meta/unevaluated",
                "https://json-schema.org/draft/2020-12/meta/validation",
                "https://json-schema.org/draft/2020-12/meta/meta-data",
                "https://json-schema.org/draft/2020-12/meta/format-annotation",
                "https://json-schema.org/draft/2020-12/meta/content"
            ]
    });

    _service = new OpenApiSpecificationService(_loggerMock.Object, _jsonValidatorServiceMock.Object, schemaResolutionOptions);
  }

  [Test]
  public async Task ValidateAsync_WithMissingRequiredFields_ReturnsExpectedErrors()
  {
    var result = await _service.ValidateAsync(JsonNode.Parse("{}")!.AsObject(), CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "MISSING_OPENAPI_VERSION"));
        Assert.That(result.Errors, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "MISSING_INFO"));
        Assert.That(result.Errors, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "MISSING_PATHS"));
    }
  }

  [Test]
  public async Task ValidateAsync_WithKnownJsonSchemaDialect_UsesDialectAsSchemaUri()
  {
    ValidationRequest? capturedRequest = null;

    _jsonValidatorServiceMock
        .Setup(x => x.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
        .Callback<ValidationRequest, CancellationToken>((req, _) => capturedRequest = req)
        .ReturnsAsync(new ValidationResult { IsValid = true, Errors = [] });

    var spec = JsonNode.Parse("""
        {
          "openapi": "3.1.0",
          "jsonSchemaDialect": "https://json-schema.org/draft/2020-12/schema",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """)!.AsObject();

    var result = await _service.ValidateAsync(spec, CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
        Assert.That(result.IsValid, Is.True);
        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.SchemaUri, Is.EqualTo("https://json-schema.org/draft/2020-12/schema"));
    }
  }

  [Test]
  public async Task ValidateAsync_WithOpenApi30Version_UsesDeclaredOpenApi30SchemaUri()
  {
    ValidationRequest? capturedRequest = null;

    _jsonValidatorServiceMock
        .Setup(x => x.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
        .Callback<ValidationRequest, CancellationToken>((req, _) => capturedRequest = req)
        .ReturnsAsync(new ValidationResult { IsValid = true, Errors = [] });

    var spec = JsonNode.Parse("""
        {
          "openapi": "3.0.3",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """)!.AsObject();

    var result = await _service.ValidateAsync(spec, CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
        Assert.That(result.IsValid, Is.True);
        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.SchemaUri, Is.EqualTo("https://json-schema.org/draft/2020-12/schema"));
    }
  }

  [Test]
  public async Task ValidateAsync_WhenJsonValidatorThrows_AddsSchemaValidationFailedWarning()
  {
    _jsonValidatorServiceMock
        .Setup(x => x.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("schema boom"));

    var spec = JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """)!.AsObject();

    var result = await _service.ValidateAsync(spec, CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
        Assert.That(result.Errors, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "SCHEMA_VALIDATION_FAILED"));
        Assert.That(result.Errors, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "SCHEMA_VALIDATION_FAILED" && e.Severity == "Error"));
        Assert.That(result.IsValid, Is.False);
    }
  }

  [Test]
  public async Task ValidateAsync_WithUnsupportedJsonSchemaDialect_AddsUnsupportedSchemaVersionError()
  {
    var spec = JsonNode.Parse("""
        {
          "openapi": "3.1.0",
          "jsonSchemaDialect": "https://example.com/unknown-schema",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """)!.AsObject();

    var result = await _service.ValidateAsync(spec, CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
        Assert.That(result.Errors, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "UNSUPPORTED_SCHEMA_VERSION" && e.Severity == "Error"));
        Assert.That(result.IsValid, Is.False);
    }
  }

  [Test]
  public async Task ValidateAsync_NormalizesAndDeduplicatesIndexedErrors()
  {
    _jsonValidatorServiceMock
        .Setup(x => x.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ValidationResult
        {
          IsValid = false,
          Errors =
            [
                    new() { Path = "paths[/services].get.responses[0].content", Message = "a[0]", ErrorCode = "V", Severity = "Error" },
                    new() { Path = "paths[/services].get.responses[1].content", Message = "a[1]", ErrorCode = "V", Severity = "Error" }
            ]
        });

    var spec = JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """)!.AsObject();

    var result = await _service.ValidateAsync(spec, CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
        Assert.That(result.Errors.Count(e => e.ErrorCode == "V"), Is.EqualTo(1));
        Assert.That(result.Errors.First(e => e.ErrorCode == "V").Path, Is.EqualTo("paths[/services].get.responses[].content"));
    }
  }

  [Test]
  public async Task ValidateAsync_GeneratesInfoRecommendations_WhenDescriptionContactLicenseMissing()
  {
    var spec = JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """)!.AsObject();

    var result = await _service.ValidateAsync(spec, CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
        Assert.That(result.Recommendations, Has.Some.Matches<Recommendation>(r => r.Path == "info.description"));
        Assert.That(result.Recommendations, Has.Some.Matches<Recommendation>(r => r.Path == "info.contact"));
        Assert.That(result.Recommendations, Has.Some.Matches<Recommendation>(r => r.Path == "info.license"));
    }
  }

  [Test]
  public async Task ValidateAsync_SwaggerDefinitions_AppliesSchemaAnalysisCounts()
  {
    var spec = JsonNode.Parse("""
        {
          "swagger": "2.0",
          "info": { "title": "Test", "version": "1.0.0" },
          "paths": {
            "/services": {
              "get": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          },
          "definitions": {
            "Service": { "type": "object", "description": "service" },
            "Location": { "type": "object", "description": "location" }
          }
        }
        """)!.AsObject();

    var result = await _service.ValidateAsync(spec, CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
        Assert.That(result.SchemaAnalysis, Is.Not.Null);
        Assert.That(result.SchemaAnalysis!.SchemaCount, Is.EqualTo(2));
    }
  }

  [Test]
  public async Task ValidateAsync_ComponentsAndExamples_ComputesQualityAndStructureMetrics()
  {
    var spec = JsonNode.Parse("""
        {
          "openapi": "3.1.0",
          "info": {
            "title": "Test",
            "version": "1.0.0",
            "description": "desc",
            "contact": { "name": "owner" },
            "license": { "name": "MIT" }
          },
          "components": {
            "schemas": {
              "Service": { "type": "object", "description": "service" }
            },
            "examples": {
              "ServiceExample": { "value": { "id": "1" } }
            }
          },
          "paths": {
            "/services": {
              "get": {
                "summary": "List",
                "description": "List services",
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "example": { "data": [] }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """)!.AsObject();

    var result = await _service.ValidateAsync(spec, CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
        Assert.That(result.SchemaAnalysis, Is.Not.Null);
        Assert.That(result.SchemaAnalysis!.ComponentCount, Is.EqualTo(1));
        Assert.That(result.SchemaAnalysis.SchemaCount, Is.EqualTo(1));
        Assert.That(result.SchemaAnalysis.ExampleCount, Is.GreaterThanOrEqualTo(1));

        Assert.That(result.QualityMetrics, Is.Not.Null);
        Assert.That(result.QualityMetrics!.DocumentationCoverage, Is.GreaterThan(0));
        Assert.That(result.QualityMetrics.QualityScore, Is.GreaterThan(0));
    }
  }

  [Test]
  public async Task ValidateAsync_AddsEndpointTestingRecommendations_WhenOperationMetadataIsIncomplete()
  {
    var spec = JsonNode.Parse("""
        {
          "openapi": "3.1.0",
          "info": {
            "title": "Test",
            "version": "1.0.0",
            "description": "desc",
            "contact": { "name": "owner" },
            "license": { "name": "MIT" }
          },
          "paths": {
            "/services": {
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

    var result = await _service.ValidateAsync(spec, CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
        Assert.That(result.Recommendations, Has.Some.Matches<Recommendation>(r => r.Path == "servers"));
        Assert.That(result.Recommendations, Has.Some.Matches<Recommendation>(r => r.Path == "paths./services.get.operationId"));
        Assert.That(result.Recommendations, Has.Some.Matches<Recommendation>(r => r.Path == "paths./services.get.responses" && r.Message.Contains("error response codes", StringComparison.OrdinalIgnoreCase)));
        Assert.That(result.Recommendations, Has.Some.Matches<Recommendation>(r => r.Path == "paths./services.get.responses" && r.Message.Contains("missing a response schema", StringComparison.OrdinalIgnoreCase)));
    }
  }

  [Test]
  public async Task ValidateAsync_DoesNotAddEndpointTestingRecommendations_WhenOperationMetadataIsComplete()
  {
    var spec = JsonNode.Parse("""
        {
          "openapi": "3.1.0",
          "servers": [
            { "url": "https://api.example.org" }
          ],
          "info": {
            "title": "Test",
            "version": "1.0.0",
            "description": "desc",
            "contact": { "name": "owner" },
            "license": { "name": "MIT" }
          },
          "paths": {
            "/services": {
              "get": {
                "operationId": "listServices",
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object"
                        }
                      }
                    }
                  },
                  "400": {
                    "description": "bad request"
                  },
                  "500": {
                    "description": "server error"
                  }
                }
              }
            }
          }
        }
        """)!.AsObject();

    var result = await _service.ValidateAsync(spec, CancellationToken.None);

    using (Assert.EnterMultipleScope())
    {
        Assert.That(result.Recommendations.Any(r => r.Path == "servers"), Is.False);
        Assert.That(result.Recommendations.Any(r => r.Path == "paths./services.get.operationId"), Is.False);
        Assert.That(result.Recommendations.Any(r => r.Path == "paths./services.get.responses" && r.Message.Contains("error response codes", StringComparison.OrdinalIgnoreCase)), Is.False);
        Assert.That(result.Recommendations.Any(r => r.Path == "paths./services.get.responses" && r.Message.Contains("missing a response schema", StringComparison.OrdinalIgnoreCase)), Is.False);
    }
  }
}
