using Microsoft.Extensions.Options;
using Moq;
using OpenReferralApi.Core.Services;
using System.Text.Json.Nodes;
using ValidationError = OpenReferralApi.Core.Models.Validation.ValidationError;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class HsdsComplianceServiceTests
{
  private Mock<IJsonValidatorService> _jsonValidatorServiceMock = null!;
  private HsdsComplianceService _service = null!;

  [SetUp]
  public void Setup()
  {
    _jsonValidatorServiceMock = new Mock<IJsonValidatorService>();
    _jsonValidatorServiceMock
        .Setup(x => x.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ValidationResult
        {
          IsValid = true,
          Errors = []
        });

    _service = new HsdsComplianceService(_jsonValidatorServiceMock.Object);
  }

  [Test]
  public void ExtractClaimedProfileVersion_FromProfileReason_ReturnsRawExtractedValue()
  {
    var version = _service.ExtractClaimedProfileVersion("Standard version [user: HSDS-UK-V3]", null);

    Assert.That(version, Is.EqualTo("HSDS-UK-V3"));
  }

  [Test]
  public void ExtractClaimedProfileVersion_FromSchemaUrl_ReturnsRawUrlSegment()
  {
    var version = _service.ExtractClaimedProfileVersion(null, "https://openreferraluk.org/specifications/3.1/openapi.json");

    Assert.That(version, Is.EqualTo("3.1"));
  }

  [Test]
  public void TryGetKnownHsdsSchemaUrl_ReturnsTrueForKnownVersion()
  {
    var service = new HsdsComplianceService(
        _jsonValidatorServiceMock.Object,
        Options.Create(new SpecificationOptions
        {
          Urls = new Dictionary<string, string>
          {
            ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
          }
        }));

    var found = service.TryGetKnownHsdsSchemaUrl("3.0", out var schemaUrl);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(found, Is.True);
      Assert.That(schemaUrl, Is.EqualTo("https://openreferraluk.org/specifications/3.0/openapi.json"));
    }
  }

  [Test]
  public void TryGetKnownHsdsSchemaUrl_ReturnsFalseForUnknownVersion()
  {
    var found = _service.TryGetKnownHsdsSchemaUrl("9.9", out var schemaUrl);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(found, Is.False);
      Assert.That(schemaUrl, Is.EqualTo(string.Empty));
    }
  }

  [Test]
  public void TryGetKnownHsdsSchemaUrl_UsesConfiguredMappingsFromOptions()
  {
    var service = new HsdsComplianceService(
        _jsonValidatorServiceMock.Object,
        Options.Create(new SpecificationOptions
        {
          Urls = new Dictionary<string, string>
          {
            ["4.0"] = "https://profiles.example.org/4.0/openapi.json"
          }
        }));

    var found = service.TryGetKnownHsdsSchemaUrl("4.0", out var schemaUrl);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(found, Is.True);
      Assert.That(schemaUrl, Is.EqualTo("https://profiles.example.org/4.0/openapi.json"));
    }
  }

  [Test]
  public void CompareFeedSpecAgainstHsdsProfile_FindsMissingRequiredAndAdditionalEndpoints()
  {
    var hsdsSpec = JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "paths": {
            "/required": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "id": { "type": "string" }
                          },
                          "required": ["id"]
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """)!;

      var feedSpec = JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "paths": {
            "/extra": {
              "get": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """)!;

    var findings = _service.CompareFeedSpecAgainstHsdsProfile(feedSpec, hsdsSpec);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(findings, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "HSDS_MISSING_ENDPOINT"));
      Assert.That(findings, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "HSDS_ADDITIONAL_ENDPOINT"));
      Assert.That(findings, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "HSDS_ADDITIONAL_ENDPOINT" && e.Severity == "Info"));
    }
  }

  [Test]
  public void CompareFeedSpecAgainstHsdsProfile_FindsMissingAndAdditionalResponseFields()
  {
    var hsdsSpec = JsonNode.Parse("""
        {
          "openapi": "3.0.0",
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
                            "id": { "type": "string" },
                            "name": { "type": "string" }
                          },
                          "required": ["id", "name"]
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """)!;

      var feedSpec = JsonNode.Parse("""
        {
          "openapi": "3.0.0",
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
                            "id": { "type": "string" },
                            "extra": { "type": "string" }
                          },
                          "required": ["id"]
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """)!;

    var findings = _service.CompareFeedSpecAgainstHsdsProfile(feedSpec, hsdsSpec);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(findings, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "HSDS_MISSING_REQUIRED_FIELD"));
      Assert.That(findings, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "HSDS_ADDITIONAL_FIELD"));
      Assert.That(findings, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "HSDS_ADDITIONAL_FIELD" && e.Severity == "Info"));
    }
  }

  [Test]
  public void CompareFeedSpecAgainstHsdsProfile_WhenRequestBodyMissing_ReportsMissingRequestBody()
  {
    var hsdsSpec = JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "paths": {
            "/services": {
              "post": {
                "requestBody": {
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "properties": {
                          "name": { "type": "string" }
                        },
                        "required": ["name"]
                      }
                    }
                  }
                },
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """)!;

      var feedSpec = JsonNode.Parse("""
        {
          "openapi": "3.0.0",
          "paths": {
            "/services": {
              "post": {
                "responses": {
                  "200": { "description": "ok" }
                }
              }
            }
          }
        }
        """)!;

    var findings = _service.CompareFeedSpecAgainstHsdsProfile(feedSpec, hsdsSpec);

    Assert.That(findings, Has.Some.Matches<ValidationError>(e => e.ErrorCode == "HSDS_MISSING_REQUEST_BODY"));
  }

  [Test]
  public async Task ValidateEndpointResponsesAgainstHsdsProfileAsync_MapsRuntimeValidationErrorsAndUpdatesStatus()
  {
    _jsonValidatorServiceMock
        .Setup(x => x.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ValidationResult
        {
          IsValid = false,
          Errors =
            [
                    new() { Path = "data.extra", Message = "extra", ErrorCode = "ADDITIONAL_FIELD", Severity = "Error" },
                    new() { Path = "data.id", Message = "bad", ErrorCode = "VALIDATION_ERROR", Severity = "Error" }
            ]
        });

    var hsdsSpec = JsonNode.Parse("""
        {
          "openapi": "3.0.0",
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
        """)!;

    var endpointTests = new List<EndpointTestResult>
        {
            new()
            {
                Method = "GET",
                Path = "/services",
                Status = EndpointTestStatus.PassedValidation,
                IsTested = true,
                TestResults =
                [
                    new()
                    {
                        IsSuccessStatusCode = true,
                        ResponseBody = System.Text.Encoding.UTF8.GetBytes("[{\"id\":\"1\",\"extra\":\"x\"}]"),
                        ValidationResult = null
                    }
                ]
            }
        };

    var options = new OpenApiValidationOptions
    {
      ReportAdditionalFields = true
    };

    await _service.ValidateEndpointResponsesAgainstHsdsProfileAsync(endpointTests, hsdsSpec, options, CancellationToken.None);

    var endpoint = endpointTests[0];
    using (Assert.EnterMultipleScope())
    {
      Assert.That(endpoint.Status, Is.EqualTo(EndpointTestStatus.FailedValidation));
      Assert.That(endpoint.TestResults[0].ValidationResult, Is.Not.Null);
      Assert.That(endpoint.TestResults[0].ValidationResult!.Errors,
          Has.Some.Matches<ValidationError>(e => e.ErrorCode == "HSDS_RUNTIME_ADDITIONAL_FIELD"));
      Assert.That(endpoint.TestResults[0].ValidationResult!.Errors,
          Has.Some.Matches<ValidationError>(e => e.ErrorCode == "HSDS_RUNTIME_VALIDATION_ERROR"));
    }
  }

  [Test]
  public async Task ValidateEndpointResponsesAgainstHsdsProfileAsync_WarningOnlySetsPassedWithWarnings()
  {
    _jsonValidatorServiceMock
        .Setup(x => x.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ValidationResult
        {
          IsValid = false,
          Errors =
            [
                    new() { Path = "data.extra", Message = "extra", ErrorCode = "ADDITIONAL_FIELD", Severity = "Error" }
            ]
        });

    var hsdsSpec = JsonNode.Parse("""
        {
          "openapi": "3.0.0",
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
        """)!;

    var endpointTests = new List<EndpointTestResult>
        {
            new()
            {
                Method = "GET",
                Path = "/services",
                Status = EndpointTestStatus.PassedValidation,
                IsTested = true,
                TestResults =
                [
                    new()
                    {
                        IsSuccessStatusCode = true,
                        ResponseBody = System.Text.Encoding.UTF8.GetBytes("[{\"id\":\"1\",\"extra\":\"x\"}]"),
                        ValidationResult = new ValidationResult
                        {
                            IsValid = true,
                            Errors = []
                        }
                    }
                ]
            }
        };

    var options = new OpenApiValidationOptions
    {
      ReportAdditionalFields = true
    };
    var serviceWithLenientPolicy = new HsdsComplianceService(
        _jsonValidatorServiceMock.Object,
      specificationOptions: Options.Create(new SpecificationOptions()),
      openApiValidationOptions: Options.Create(new OpenApiValidationServerOptions { OwnSchemaValidation = OwnSchemaValidationMode.AllowAdditionalProperties }));

    await serviceWithLenientPolicy.ValidateEndpointResponsesAgainstHsdsProfileAsync(endpointTests, hsdsSpec, options, CancellationToken.None);

    var endpoint = endpointTests[0];
    using (Assert.EnterMultipleScope())
    {
      Assert.That(endpoint.Status, Is.EqualTo(EndpointTestStatus.PassedWithWarnings));
      Assert.That(endpoint.TestResults[0].ValidationResult!.Errors,
          Has.Some.Matches<ValidationError>(e => e.Severity == "Warning"));
    }
  }

  [Test]
  public async Task ValidateEndpointResponsesAgainstHsdsProfileAsync_WhenReportAdditionalFieldsFalse_OmitsAdditionalFieldWarnings()
  {
    _jsonValidatorServiceMock
        .Setup(x => x.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ValidationResult
        {
          IsValid = false,
          Errors =
            [
                    new() { Path = "data.extra", Message = "extra", ErrorCode = "ADDITIONAL_FIELD", Severity = "Error" }
            ]
        });

    var hsdsSpec = JsonNode.Parse("""
        {
          "openapi": "3.0.0",
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
        """)!;

    var endpointTests = new List<EndpointTestResult>
        {
            new()
            {
                Method = "GET",
                Path = "/services",
                Status = EndpointTestStatus.PassedValidation,
                IsTested = true,
                TestResults =
                [
                    new()
                    {
                        IsSuccessStatusCode = true,
                        ResponseBody = System.Text.Encoding.UTF8.GetBytes("[{\"id\":\"1\",\"extra\":\"x\"}]"),
                        ValidationResult = new ValidationResult
                        {
                            IsValid = true,
                            Errors = []
                        }
                    }
                ]
            }
        };

    var options = new OpenApiValidationOptions
    {
      ReportAdditionalFields = false
    };

    var serviceWithLenientPolicy = new HsdsComplianceService(
        _jsonValidatorServiceMock.Object,
      specificationOptions: Options.Create(new SpecificationOptions()),
      openApiValidationOptions: Options.Create(new OpenApiValidationServerOptions { OwnSchemaValidation = OwnSchemaValidationMode.AllowAdditionalProperties }));

    await serviceWithLenientPolicy.ValidateEndpointResponsesAgainstHsdsProfileAsync(endpointTests, hsdsSpec, options, CancellationToken.None);

    var endpoint = endpointTests[0];
    using (Assert.EnterMultipleScope())
    {
      Assert.That(endpoint.Status, Is.EqualTo(EndpointTestStatus.PassedValidation));
      Assert.That(endpoint.TestResults[0].ValidationResult!.Errors,
          Has.None.Matches<ValidationError>(e => e.ErrorCode == "HSDS_RUNTIME_ADDITIONAL_FIELD"));
    }
  }

  [Test]
  public void ApplyAdditionalFieldPolicy_SetsWarningsAndUpdatesValidity()
  {
    var result = new ValidationResult
    {
      IsValid = false,
      Errors =
            [
                new() { ErrorCode = "ADDITIONAL_FIELD", Severity = "Error" },
                new() { ErrorCode = "SOME_OTHER", Severity = "Warning" }
            ]
    };

    var serviceWithLenientPolicy = new HsdsComplianceService(
        _jsonValidatorServiceMock.Object,
      specificationOptions: Options.Create(new SpecificationOptions()),
      openApiValidationOptions: Options.Create(new OpenApiValidationServerOptions { OwnSchemaValidation = OwnSchemaValidationMode.AllowAdditionalProperties }));
    serviceWithLenientPolicy.ApplyAdditionalFieldPolicy(result, reportAdditionalFields: true);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(result.Errors.Single(e => e.ErrorCode == "ADDITIONAL_FIELD").Severity, Is.EqualTo("Warning"));
      Assert.That(result.IsValid, Is.True);
    }
  }

  [Test]
  public void ApplyAdditionalFieldPolicy_WhenReportAdditionalFieldsFalse_RemovesWarningEntries()
  {
    var result = new ValidationResult
    {
      IsValid = false,
      Errors =
      [
        new() { ErrorCode = "ADDITIONAL_FIELD", Severity = "Error" },
        new() { ErrorCode = "SOME_OTHER", Severity = "Warning" }
      ]
    };

    var serviceWithLenientPolicy = new HsdsComplianceService(
      _jsonValidatorServiceMock.Object,
      specificationOptions: Options.Create(new SpecificationOptions()),
      openApiValidationOptions: Options.Create(new OpenApiValidationServerOptions { OwnSchemaValidation = OwnSchemaValidationMode.AllowAdditionalProperties }));
    serviceWithLenientPolicy.ApplyAdditionalFieldPolicy(result, reportAdditionalFields: false);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(result.Errors, Has.None.Matches<ValidationError>(e => e.ErrorCode == "ADDITIONAL_FIELD"));
      Assert.That(result.IsValid, Is.True);
    }
  }
}
