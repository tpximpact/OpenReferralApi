using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class OpenApiValidationServiceTests
{
    private Mock<ILogger<OpenApiValidationService>> _loggerMock;
    private Mock<IJsonValidatorService> _jsonValidatorServiceMock;
    private Mock<ISchemaResolverService> _schemaResolverServiceMock;
    private Mock<IProfileDiscoveryService> _openApiBootstrapServiceMock;
    private IOpenApiSpecificationService _openApiSpecificationService;
    private IOptions<OpenApiValidationServerOptions> _openApiValidationServerOptions;
    private HttpClient _httpClient;
    private OpenApiValidationService _service;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<OpenApiValidationService>>();
        _jsonValidatorServiceMock = new Mock<IJsonValidatorService>();
        _schemaResolverServiceMock = new Mock<ISchemaResolverService>();
        _openApiBootstrapServiceMock = new Mock<IProfileDiscoveryService>();
        _openApiBootstrapServiceMock
            .Setup(s => s.DiscoverFromBaseUrlAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string?, string?, DataSourceAuthentication?, CancellationToken>(
                (ownSchemaUrl, baseUrl, _, ct) =>
                {
                    string? profileVersion = null;

                    if (string.IsNullOrWhiteSpace(profileVersion) && !string.IsNullOrWhiteSpace(ownSchemaUrl))
                    {
                        if (ownSchemaUrl.Contains("/specifications/3.0/", StringComparison.OrdinalIgnoreCase))
                        {
                            profileVersion = "HSDS-UK-3.0";
                        }
                        else if (ownSchemaUrl.Contains("/specifications/1.0/", StringComparison.OrdinalIgnoreCase))
                        {
                            profileVersion = "HSDS-UK-1.0";
                        }
                        else if (ownSchemaUrl.Contains("/3.2/", StringComparison.OrdinalIgnoreCase))
                        {
                            profileVersion = "HSDS-3.2";
                        }
                    }

                    // Fallback: For standard test URLs, default to HSDS-UK-3.0
                    // But only if not using baseUrl that explicitly expects no profile discovery
                    if (string.IsNullOrWhiteSpace(profileVersion) && !string.IsNullOrWhiteSpace(baseUrl))
                    {
                        if (baseUrl.Contains("feed.example.com", StringComparison.OrdinalIgnoreCase))
                        {
                            profileVersion = "HSDS-UK-3.0";
                        }
                    }

                    var schemaUrl = profileVersion switch
                    {
                        "HSDS-UK-3.0" => "https://openreferraluk.org/specifications/3.0/openapi.json",
                        "HSDS-UK-1.0" => "https://openreferraluk.org/specifications/1.0/openapi.json",
                        "HSDS-3.2" => ownSchemaUrl, // Return the ownSchemaUrl for custom profiles
                        _ => null
                    };

                    return Task.FromResult(new ProfileDiscoveryResult
                    {
                        HsdsProfileVersion = profileVersion,
                        HsdsProfileSchemaUrl = schemaUrl,
                        HsdsProfileSchemaContent = schemaUrl == null ? null : CreateHsdsProfileSpecWithRequestBody(),
                        OpenApiSchemaContent = null,
                        HsdsProfileReason = schemaUrl == null
                            ? "No version or openapi_url found in '/' response"
                            : $"Standard version [user: {profileVersion}] discovered from profile context"
                    });
                });

        _openApiSpecificationService = new OpenApiSpecificationService(
            NullLogger<OpenApiSpecificationService>.Instance,
            _jsonValidatorServiceMock.Object);

        _jsonValidatorServiceMock
            .Setup(service => service.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = true,
                Errors = [],
                SchemaVersion = "test",
                Duration = TimeSpan.Zero
            });

        _schemaResolverServiceMock
            .Setup(service => service.CreateSchemaFromJsonAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DataSourceAuthentication?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string schemaJson, string? documentUri, DataSourceAuthentication? auth, CancellationToken ct) => JsonSchema.FromText(schemaJson));

        _schemaResolverServiceMock
            .Setup(service => service.CreateSchemaFromJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string schemaJson, CancellationToken ct) => JsonSchema.FromText(schemaJson));

        // Mock ResolveAsync method for OpenAPI document resolution
        _schemaResolverServiceMock
            .Setup(service => service.ResolveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DataSourceAuthentication?>()))
            .ReturnsAsync((string schema, string? baseUri, DataSourceAuthentication? auth) => schema);

        _schemaResolverServiceMock
            .Setup(service => service.GetResolutionIssues())
            .Returns([]);

        var mockHandler = new MockHttpMessageHandler();
        _httpClient = TestHttpClientFactory.CreateClient(mockHandler);

        _openApiValidationServerOptions = Options.Create(new OpenApiValidationServerOptions
        {
            HsdsValidationMode = HsdsValidationMode.Fast,
            AllowUserSuppliedAuth = true,
            ValidateSpecification = true,
            TestEndpoints = true,
            TestOptionalEndpoints = true,
            TreatOptionalEndpointsAsWarnings = true
        });

        _service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
             null!,
            _openApiBootstrapServiceMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-1.0"] = "https://openreferraluk.org/specifications/1.0/openapi.json",
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: _openApiValidationServerOptions);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    #region Basic Response Handling

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_ReturnsValidationResult()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com"
        };
        SetupHttpMock(json);

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_LogsUnifiedMemoryCheckpointPayload()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com"
        };
        SetupHttpMock(json);

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(HasInformationLogContaining(_loggerMock, "Memory checkpoint OpenApiValidationService/start."), Is.True);
            Assert.That(HasInformationLogContaining(_loggerMock, "ManagedHeapBytes:"), Is.True);
            Assert.That(HasInformationLogContaining(_loggerMock, "GcHeapSizeBytes:"), Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_UsesDiscoveredCachedHsdsSchemaContent_WithoutResolvingProfileUrl()
    {
        // Arrange
        var strictSchemaResolver = new Mock<ISchemaResolverService>(MockBehavior.Strict);
        strictSchemaResolver
            .Setup(service => service.GetResolutionIssues())
            .Returns([]);

        var discoveryMock = new Mock<IProfileDiscoveryService>();
        discoveryMock
            .Setup(s => s.DiscoverFromBaseUrlAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileDiscoveryResult
            {
                HsdsProfileVersion = "HSDS-UK-3.0",
                HsdsProfileReason = "Standard version [user: HSDS-UK-3.0] discovered from base URL",
                OpenApiSchemaContent = CreateOpenApi30Spec(),
                HsdsProfileSchemaContent = CreateOpenApi30Spec()
            });

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            strictSchemaResolver.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
            discoveryMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                ValidateSpecification = false,
                TestEndpoints = false,
                OwnSchemaValidation = OwnSchemaValidationMode.Strict
            }));

        var request = new OpenApiValidationRequest
        {
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        // Act
        var result = await service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result.IsValid, Is.True);
        strictSchemaResolver.Verify(
            service => service.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DataSourceAuthentication>()),
            Times.Never,
            "HSDS profile schema should be consumed from discovery result content rather than resolved again by URL.");
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_IncludesMetadata()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://api.example.com/openapi.json",
            BaseUrl = "https://api.example.com"
        };
        SetupHttpMock(json);

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result.Metadata, Is.Not.Null);
        Assert.That(result.Metadata!.BaseUrl, Is.EqualTo("https://api.example.com"));
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_MetadataProfile_UsesConfiguredNonHsdsUkProfileKey()
    {
        // Arrange
        var customProfileSpecUrl = "https://raw.githubusercontent.com/openreferral/specification/refs/heads/3.2/schema/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = customProfileSpecUrl,
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString();
            if (string.Equals(requestUrl, customProfileSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateOpenApi30Spec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        var serviceWithCustomProfile = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
            _openApiBootstrapServiceMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-3.2"] = customProfileSpecUrl,
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                ValidateSpecification = false,
                TestEndpoints = false
            }));

        // Act
        var result = await serviceWithCustomProfile.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Metadata?.Profile, Is.EqualTo("HSDS-3.2"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_MeasuresDuration()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com"
        };
        SetupHttpMock(json);

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result.Duration, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_IncludesSummary()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com"
        };
        SetupHttpMock(json);

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result.Summary, Is.Not.Null);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenCircularRefDetected_AddsSpecificationErrorButNotNotification()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        SetupHttpMock(json);

        _schemaResolverServiceMock
            .Setup(service => service.GetResolutionIssues())
            .Returns([
                new SchemaResolutionIssue
                {
                    ErrorCode = "CIRCULAR_SCHEMA_REFERENCE",
                    Reference = "#/components/schemas/Organization",
                    ReferencePath = "https://example.com/openapi.json#/components/schemas/Organization -> https://example.com/openapi.json#/components/schemas/Service -> https://example.com/openapi.json#/components/schemas/Organization",
                    Message = "Circular schema reference detected"
                }
            ]);

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Notifications.Any(n => n.Contains("Circular schema reference detected", StringComparison.Ordinal)), Is.False);
            Assert.That(result.SpecificationValidation, Is.Not.Null);
            Assert.That(result.SpecificationValidation!.Errors.Any(e => e.ErrorCode == "CIRCULAR_SCHEMA_REFERENCE"), Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenNoProfileDiscoveredAndNoDefault_AddsNotificationAndReturnsResult()
    {
        // Arrange
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        var expectedException = ProfileValidationErrors.NoProfileDiscoveredAndNoDefault();

        var discoveryMock = new Mock<IProfileDiscoveryService>();
        discoveryMock
            .Setup(s => s.DiscoverFromBaseUrlAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
            discoveryMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-1.0"] = "https://openreferraluk.org/specifications/1.0/openapi.json",
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: _openApiValidationServerOptions);

        // Act
        var result = await service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Notifications, Has.Count.EqualTo(1));
            Assert.That(result.Notifications[0], Is.EqualTo(expectedException.Message));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithExplicitProfile_BypassesDiscoveryAndUsesExplicitProfile()
    {
        // Arrange
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Profile = "HSDS-UK-3.0",
            Options = new OpenApiValidationOptions()
        };

        var expectedResult = new ProfileDiscoveryResult
        {
            HsdsProfileVersion = "HSDS-UK-3.0",
            HsdsProfileSchemaUrl = "https://openreferraluk.org/specifications/3.0/openapi.json",
            HsdsProfileSchemaContent = CreateOpenApi30Spec(),
            OpenApiSchemaContent = null,
            HsdsProfileReason = "Explicit profile 'HSDS-UK-3.0' provided in request."
        };

        var discoveryMock = new Mock<IProfileDiscoveryService>();
        discoveryMock
            .Setup(s => s.GetExplicitProfile("HSDS-UK-3.0"))
            .Returns(expectedResult);

        SetupHttpMock(CreateOpenApi30Spec());

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
            discoveryMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-1.0"] = "https://openreferraluk.org/specifications/1.0/openapi.json",
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: _openApiValidationServerOptions);

        // Act
        var result = await service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.True);
            discoveryMock.Verify(s => s.DiscoverFromBaseUrlAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()), Times.Never);
            discoveryMock.Verify(s => s.GetExplicitProfile("HSDS-UK-3.0"), Times.Once);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithExplicitProfileUnsupported_AddsNotificationAndReturnsResult()
    {
        // Arrange
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Profile = "HSDS-UNSUPPORTED",
            Options = new OpenApiValidationOptions()
        };

        var expectedException = ProfileValidationErrors.DiscoveredUnsupported("HSDS-UNSUPPORTED");

        var discoveryMock = new Mock<IProfileDiscoveryService>();
        discoveryMock
            .Setup(s => s.GetExplicitProfile("HSDS-UNSUPPORTED"))
            .Throws(expectedException);

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
            discoveryMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: _openApiValidationServerOptions);

        // Act
        var result = await service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Notifications, Has.Count.EqualTo(1));
            Assert.That(result.Notifications[0], Is.EqualTo(expectedException.Message));
        }
    }

    #endregion

    #region OpenAPI Version Detection

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_DetectsOpenApi30Version()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };
        SetupHttpMock(json);

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result.SpecificationValidation, Is.Not.Null);
        Assert.That(result.SpecificationValidation!.OpenApiVersion, Does.Contain("3.0"));
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_DetectsSwagger20Version()
    {
        // Arrange
        var json = CreateSwagger20Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/swagger.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };
        SetupHttpMock(json);

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result.SpecificationValidation, Is.Not.Null);
        Assert.That(result.SpecificationValidation!.Errors, Is.Not.Null);
    }

    #endregion

    #region Validation Options

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_SkipsValidationWhenDisabled()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };
        SetupHttpMock(json);

        // Create service with ValidateSpecification disabled
        var serviceOptions = Options.Create(new OpenApiValidationServerOptions
        {
            HsdsValidationMode = HsdsValidationMode.Fast,
            AllowUserSuppliedAuth = true,
            ValidateSpecification = false
        });

        var serviceWithDisabledValidation = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
            _openApiBootstrapServiceMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-1.0"] = "https://openreferraluk.org/specifications/1.0/openapi.json",
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: serviceOptions);

        // Act
        var result = await serviceWithDisabledValidation.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result.SpecificationValidation, Is.Null);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_PerformsValidationWhenEnabled()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        _jsonValidatorServiceMock
            .Setup(service => service.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                Errors =
                [
                    new()
                    {
                        Path = "paths./items[0].name",
                        Message = "paths./items[0].name is required",
                        ErrorCode = "VALIDATION_ERROR",
                        Severity = "Error"
                    },
                    new()
                    {
                        Path = "paths./items[1].name",
                        Message = "paths./items[1].name is required",
                        ErrorCode = "VALIDATION_ERROR",
                        Severity = "Error"
                    },
                    new()
                    {
                        Path = "paths./items[0].metadata",
                        Message = "paths./items[0].metadata is not expected",
                        ErrorCode = "VALIDATION_WARNING",
                        Severity = "Warning"
                    },
                    new()
                    {
                        Path = "paths./items[2].metadata",
                        Message = "paths./items[2].metadata is not expected",
                        ErrorCode = "VALIDATION_WARNING",
                        Severity = "Warning"
                    }
                ]
            });

        SetupHttpMock(json);

        // Rebuild service with a mock HsdsComplianceService to isolate deduplication logic from HSDS profile detection
        var hsdsComplianceMock = new Mock<IHsdsComplianceService>();
        hsdsComplianceMock.Setup(s => s.ExtractClaimedProfileVersion(It.IsAny<string>(), It.IsAny<string>())).Returns((string?)"HSDS-30");
        string? unused;
        hsdsComplianceMock.Setup(s => s.TryGetKnownHsdsSchemaUrl(It.IsAny<string>(), out unused)).Returns(false);
        hsdsComplianceMock.Setup(s => s.CompareFeedSpecAgainstHsdsProfile(It.IsAny<JsonNode>(), It.IsAny<JsonNode>())).Returns([]);
        _service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            hsdsComplianceMock.Object,
            null!,
             null!,
            _openApiBootstrapServiceMock.Object,
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                HsdsValidationMode = _openApiValidationServerOptions.Value.HsdsValidationMode,
                AllowUserSuppliedAuth = _openApiValidationServerOptions.Value.AllowUserSuppliedAuth,
                ValidateSpecification = true
            }));

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);
        var errors = result.SpecificationValidation!.Errors;
        var normalizedErrors = errors.Where(e => e.ErrorCode is "VALIDATION_ERROR" or "VALIDATION_WARNING").ToList();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(normalizedErrors, Has.Count.EqualTo(2));
            Assert.That(normalizedErrors.All(e => e.Path.Contains("[]")), Is.True);
            Assert.That(normalizedErrors.All(e => e.Message.Contains("[]")), Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_DeduplicationUsesPathOnlyAndKeepsFirstError()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        _jsonValidatorServiceMock
            .Setup(service => service.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                Errors =
                [
                    new()
                    {
                        Path = "items[0].name",
                        Message = "items[0].name is required",
                        ErrorCode = "VALIDATION_ERROR",
                        Severity = "Error"
                    },
                    new()
                    {
                        Path = "items[1].name",
                        Message = "items[1].name is required",
                        ErrorCode = "VALIDATION_WARNING",
                        Severity = "Warning"
                    }
                ]
            });

        SetupHttpMock(json);

        // Rebuild service with a mock HsdsComplianceService to isolate deduplication logic from HSDS profile detection
        var hsdsComplianceMock = new Mock<IHsdsComplianceService>();
        hsdsComplianceMock.Setup(s => s.ExtractClaimedProfileVersion(It.IsAny<string>(), It.IsAny<string>())).Returns((string?)"HSDS-30");
        string? unused;
        hsdsComplianceMock.Setup(s => s.TryGetKnownHsdsSchemaUrl(It.IsAny<string>(), out unused)).Returns(true);
        hsdsComplianceMock.Setup(s => s.CompareFeedSpecAgainstHsdsProfile(It.IsAny<JsonNode>(), It.IsAny<JsonNode>())).Returns([]);
        _service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            hsdsComplianceMock.Object,
            null!,
            null!,
            _openApiBootstrapServiceMock.Object,
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                HsdsValidationMode = _openApiValidationServerOptions.Value.HsdsValidationMode,
                AllowUserSuppliedAuth = _openApiValidationServerOptions.Value.AllowUserSuppliedAuth,
                ValidateSpecification = true
            }));

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);
        var errors = result.SpecificationValidation!.Errors;
        var normalizedNameErrors = errors.Where(e => e.Path == "items[].name").ToList();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(normalizedNameErrors, Has.Count.EqualTo(1), "Entries with the same normalized path should collapse into one normalized entry");
            Assert.That(normalizedNameErrors[0].Severity, Is.EqualTo("Error"));
            Assert.That(normalizedNameErrors[0].ErrorCode, Is.EqualTo("VALIDATION_ERROR"));
            Assert.That(normalizedNameErrors[0].Message, Does.Contain("required"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_UsesDefaultOptionsWhenNull()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com"
        };
        SetupHttpMock(json);

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenValidateSpecificationTrueAndDeclaredSchemaUnsupported_ReturnsFailureErrors()
    {
        // Arrange
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        var openApiWithUnsupportedDialect = @"{
            ""openapi"": ""3.1.0"",
            ""jsonSchemaDialect"": ""https://example.com/unknown-schema"",
            ""info"": {
                ""title"": ""Test API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/test"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";

        SetupHttpMock(openApiWithUnsupportedDialect);

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.SpecificationValidation, Is.Not.Null);
            Assert.That(result.SpecificationValidation!.IsValid, Is.False);
            Assert.That(result.SpecificationValidation.Errors.Any(e => e.ErrorCode == "UNSUPPORTED_SCHEMA_VERSION" && string.Equals(e.Severity, "Error", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_FailsWhenRequiredHsdsEndpointMissing()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions
            {
                ReportAdditionalFields = true
            },
        };

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString();
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateFeedSpecMissingRequiredHsdsEndpoint())
                };
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.SpecificationValidation, Is.Not.Null);
            Assert.That(result.SpecificationValidation!.Errors.Any(e => e.ErrorCode == "HSDS_MISSING_ENDPOINT"), Is.True);
            Assert.That(result.Metadata?.Profile, Is.EqualTo("HSDS-UK-3.0"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_ReportsAdditionalHsdsEndpointAsInfoOnly()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions
            {
                ReportAdditionalFields = true
            },
        };

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString();
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateFeedSpecWithAdditionalEndpoint())
                };
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SpecificationValidation, Is.Not.Null);
            Assert.That(result.SpecificationValidation!.Errors.Any(e => e.ErrorCode == "HSDS_ADDITIONAL_ENDPOINT"), Is.True);
            Assert.That(result.SpecificationValidation.Errors.Any(e =>
                e.ErrorCode == "HSDS_ADDITIONAL_ENDPOINT" &&
                string.Equals(e.Severity, "Info", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_FailsWhenRequiredHsdsRequestFieldMissing()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions
            {
                ReportAdditionalFields = true
            },
        };

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString();
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateFeedSpecMissingRequiredHsdsRequestField())
                };
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpecWithRequestBody())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.SpecificationValidation, Is.Not.Null);
            Assert.That(result.SpecificationValidation!.Errors.Any(e => e.ErrorCode == "HSDS_MISSING_REQUIRED_REQUEST_FIELD"), Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenReportAdditionalFieldsFalse_OmitsHsdsAdditionalInfoFindings()
    {
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions
            {
                ReportAdditionalFields = false
            },
        };

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString();
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateFeedSpecWithAdditionalEndpoint())
                };
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SpecificationValidation, Is.Not.Null);
            Assert.That(result.SpecificationValidation!.Errors.Any(e => e.ErrorCode == "HSDS_ADDITIONAL_ENDPOINT"), Is.False);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_FallsBackToHsdsProfileSpecWhenFeedSpecFetchFails()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions
            {
                ReportAdditionalFields = true
            },
        };

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString();
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SpecificationValidation, Is.Not.Null);
            Assert.That(result.Notifications, Has.Some.EqualTo("Unable to fetch OpenAPI specification from the feed URL. Falling back to the HSDS profile OpenAPI specification."));
            Assert.That(request.OwnSchemaUrl, Is.EqualTo(hsdsSpecUrl));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenBaseDiscoveryFails_UsesConfiguredDefaultProfileFallback()
    {
        // Arrange
        var defaultProfileSpecUrl = $"https://default-{Guid.NewGuid():N}.example.com/specifications/1.0/openapi.json";
        var request = new OpenApiValidationRequest
        {
            BaseUrl = "https://directory.example.com/api",
            Options = new OpenApiValidationOptions()
        };

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString();
            if (string.Equals(requestUrl, defaultProfileSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        var defaultDiscoveryMock = new Mock<IProfileDiscoveryService>();
        defaultDiscoveryMock
            .Setup(s => s.DiscoverFromBaseUrlAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileDiscoveryResult
            {
                HsdsProfileVersion = "HSDS-UK-1.0",
                HsdsProfileSchemaUrl = defaultProfileSpecUrl,
                HsdsProfileSchemaContent = CreateHsdsProfileSpec(),
                HsdsProfileReason = "Using configured default HSDS profile version: HSDS-UK-1.0",
                UsedDefaultProfile = true
            });

        var serviceWithDefaultFallback = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
            defaultDiscoveryMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                DefaultProfileVersion = "HSDS-UK-1.0",
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-1.0"] = defaultProfileSpecUrl,
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                ValidateSpecification = false,
                TestEndpoints = false
            }));

        // Act
        var result = await serviceWithDefaultFallback.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(request.OwnSchemaUrl, Is.Not.Null);
            Assert.That(request.OwnSchemaUrl, Is.EqualTo(defaultProfileSpecUrl));
            Assert.That(result.Notifications.Any(n => n.Contains("configured default HSDS profile OpenAPI specification", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenFeedOpenApiFetchFailsWithoutProfileContext_UsesConfiguredDefaultProfileFallback()
    {
        // Arrange
        var feedSpecUrl = $"https://feed-{Guid.NewGuid():N}.example.com/openapi.json";
        var defaultProfileSpecUrl = $"https://default-{Guid.NewGuid():N}.example.com/specifications/1.0/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions()
        };

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString();
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }

            if (string.Equals(requestUrl, defaultProfileSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        var defaultDiscoveryMock = new Mock<IProfileDiscoveryService>();
        defaultDiscoveryMock
            .Setup(s => s.DiscoverFromBaseUrlAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileDiscoveryResult
            {
                HsdsProfileVersion = "HSDS-UK-1.0",
                HsdsProfileSchemaUrl = defaultProfileSpecUrl,
                HsdsProfileSchemaContent = CreateHsdsProfileSpec(),
                HsdsProfileReason = "Using configured default HSDS profile version: HSDS-UK-1.0",
                UsedDefaultProfile = true
            });

        var serviceWithDefaultFallback = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
_openApiSpecificationService,
            null!,
            null!,
            null!,

            defaultDiscoveryMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                DefaultProfileVersion = "HSDS-UK-1.0",
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-1.0"] = defaultProfileSpecUrl
                }
            }),
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                ValidateSpecification = false,
                TestEndpoints = false
            }));

        // Act
        var result = await serviceWithDefaultFallback.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(request.OwnSchemaUrl, Is.EqualTo(defaultProfileSpecUrl));
            Assert.That(result.Notifications.Any(n => n.Contains("Falling back to the HSDS profile OpenAPI specification", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(result.Metadata?.Profile, Is.EqualTo("HSDS-UK-1.0"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenConfiguredDefaultProfileIsInvalid_MaintainsExistingFailureFlow()
    {
        // Arrange
        var request = new OpenApiValidationRequest
        {
            BaseUrl = "https://directory.example.com/api"
        };

        var serviceWithInvalidDefault = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object, _openApiSpecificationService,
            null!,
            null!,
            null!,

            _openApiBootstrapServiceMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                DefaultProfileVersion = "HSDS-UK-9.9",
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-1.0"] = "https://openreferraluk.org/specifications/1.0/openapi.json"
                }
            }));

        // Act
        var result = await serviceWithInvalidDefault.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Notifications, Has.Count.EqualTo(1));
            Assert.That(result.Notifications[0], Does.Contain("Unable to get or resolve the OpenAPI specification"));
            Assert.That(result.Notifications[0], Does.Contain("Failed to discover OpenAPI schema URL or schema content from base URL"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenSchemaIsDiscovered_DoesNotRequestSchemaAuth()
    {
        // Arrange
        var discoveredSchemaContent = CreateOpenApi30Spec();
        var auth = new DataSourceAuthentication { BearerToken = "test-token" };
        var request = new OpenApiValidationRequest
        {
            BaseUrl = "https://directory.example.com/api",
            DataSourceAuth = auth,
            Options = new OpenApiValidationOptions()
        };

        var authenticationValidationServiceMock = new Mock<IAuthenticationValidationService>();
        authenticationValidationServiceMock
            .Setup(s => s.TryGetValidatedRequestAuthentication("datasource", It.IsAny<DataSourceAuthentication?>()))
            .Returns(auth);
        authenticationValidationServiceMock
            .Setup(s => s.TryGetValidatedRequestAuthentication("schema", It.IsAny<DataSourceAuthentication?>()))
            .Returns(auth);

        var bootstrapServiceMock = new Mock<IProfileDiscoveryService>();
        bootstrapServiceMock
            .Setup(s => s.DiscoverFromBaseUrlAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DataSourceAuthentication?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileDiscoveryResult
            {
                OpenApiSchemaContent = discoveredSchemaContent
            });

        var httpClient = TestHttpClientFactory.CreateClient(new MockHttpMessageHandler((httpRequest, ct) =>
            new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)));

        using var _ = httpClient;

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object, _openApiSpecificationService,
            null!,
            null!,
            authenticationValidationServiceMock.Object,
            bootstrapServiceMock.Object,
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                ValidateSpecification = false,
                TestEndpoints = false,
                AllowUserSuppliedAuth = true
            }));

        // Act
        var result = await service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result.IsValid, Is.True);
        authenticationValidationServiceMock.Verify(
            s => s.TryGetValidatedRequestAuthentication("datasource", It.IsAny<DataSourceAuthentication?>()),
            Times.Once);
        authenticationValidationServiceMock.Verify(
            s => s.TryGetValidatedRequestAuthentication("schema", It.IsAny<DataSourceAuthentication?>()),
            Times.Never);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenOwnSchemaUrlProvided_RequestsSchemaAuth()
    {
        // Arrange
        var ownSchemaUrl = "https://example.com/openapi.json";
        var auth = new DataSourceAuthentication { BearerToken = "test-token" };
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = ownSchemaUrl,
            BaseUrl = "https://api.example.com",
            DataSourceAuth = auth,
            Options = new OpenApiValidationOptions()
        };

        var authenticationValidationServiceMock = new Mock<IAuthenticationValidationService>();
        authenticationValidationServiceMock
            .Setup(s => s.TryGetValidatedRequestAuthentication("datasource", It.IsAny<DataSourceAuthentication?>()))
            .Returns(auth);
        authenticationValidationServiceMock
            .Setup(s => s.TryGetValidatedRequestAuthentication("schema", It.IsAny<DataSourceAuthentication?>()))
            .Returns(auth);

        var bootstrapServiceMock = new Mock<IProfileDiscoveryService>();

        var httpClient = TestHttpClientFactory.CreateClient(new MockHttpMessageHandler((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString() ?? string.Empty;
            if (string.Equals(requestUrl, ownSchemaUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateOpenApi30Spec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }));

        using var _ = httpClient;

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
_openApiSpecificationService,
            null!,
            null!,
            authenticationValidationServiceMock.Object,
            _openApiBootstrapServiceMock.Object,
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                ValidateSpecification = false,
                TestEndpoints = false,
                AllowUserSuppliedAuth = true
            }));

        // Act
        var result = await service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result.IsValid, Is.True);
        authenticationValidationServiceMock.Verify(
            s => s.TryGetValidatedRequestAuthentication("datasource", It.IsAny<DataSourceAuthentication?>()),
            Times.Once);
        authenticationValidationServiceMock.Verify(
            s => s.TryGetValidatedRequestAuthentication("schema", It.IsAny<DataSourceAuthentication?>()),
            Times.Once);
        bootstrapServiceMock.Verify(
            s => s.DiscoverFromBaseUrlAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DataSourceAuthentication?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_AddsUnknownProfileErrorWhenProfileContextCannotBeMapped()
    {
        // Arrange
        var feedSpecUrl = "https://unknown-profile.example.com/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://unknown-profile.example.com",
            Options = new OpenApiValidationOptions(),
        };

        SetupHttpMock(CreateOpenApi30Spec());

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.SpecificationValidation, Is.Not.Null);
            Assert.That(result.SpecificationValidation!.Errors.Any(e => e.ErrorCode == "HSDS_PROFILE_UNKNOWN"), Is.True);
            Assert.That(result.SpecificationValidation.IsValid, Is.False);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_ExtractsProfileVersionFromOpenApiExtension()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions()
        };

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString();
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateOpenApi30SpecWithHsdsVersionExtension("3.0"))
                };
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Metadata?.Profile, Is.EqualTo("HSDS-UK-3.0"));
            Assert.That(result.Metadata?.ProfileReason, Does.Contain("3.0"));
            Assert.That(result.Notifications, Has.Count.EqualTo(1));
            Assert.That(result.Notifications[0], Does.Contain("Validation failed"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenValidationFails_AddsValidationFailedNotification()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions()
        };

        // Setup mock spec that is invalid (e.g. missing required endpoints/spec errors)
        SetupHttpMock(CreateOpenApi30Spec());

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Notifications.Any(n => n.Contains("Validation failed. One or more specification validation errors or endpoint test failures were encountered.")), Is.True);
        }
    }


    [Test]
    public async Task ValidateOpenApiSpecificationAsync_ExtractsProfileVersionFromOpenApiFieldAndWarns()
    {
        // Arrange
        var feedSpecUrl = "https://unknown-version.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://unknown-version.example.com",
            Options = new OpenApiValidationOptions()
        };

        // ProfileDiscoveryService returns the warning in HsdsProfileReason when version is in the 'openapi' field
        _openApiBootstrapServiceMock
            .Setup(s => s.DiscoverFromBaseUrlAsync(
                It.IsAny<string?>(),
                It.Is<string?>(u => u != null && u.Contains("unknown-version.example.com", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileDiscoveryResult
            {
                HsdsProfileVersion = "HSDS-UK-3.0",
                HsdsProfileSchemaUrl = hsdsSpecUrl,
                HsdsProfileSchemaContent = CreateHsdsProfileSpec(),
                HsdsProfileReason = "Warning: The HSDS schema version was incorrectly defined in the 'openapi' field. Detected HSDS version HSDS-UK-3.0 from this field as a fallback. Please use an 'x-hsds-version' field in your OpenAPI spec to declare the HSDS version.",
                UsedDefaultProfile = false
            });

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString();
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateOpenApiSpecWithMisusedOpenApiField("HSDS-UK-3.0"))
                };
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Metadata?.Profile, Is.EqualTo("HSDS-UK-3.0"));
            Assert.That(result.SpecificationValidation, Is.Not.Null);
            Assert.That(result.SpecificationValidation!.Errors.Any(e => e.ErrorCode == "HSDS_SCHEMA_VERSION_MISPLACED"), Is.True);
            Assert.That(result.SpecificationValidation.Errors.Any(e =>
                e.Message.Contains("incorrectly defined in the 'openapi' field", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_ReportsAdditionalHsdsRequestFieldAsInfoOnly()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions
            {
                ReportAdditionalFields = true
            },
        };

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString();
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateFeedSpecWithAdditionalHsdsRequestField())
                };
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpecWithRequestBody())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SpecificationValidation, Is.Not.Null);
            Assert.That(result.SpecificationValidation!.Errors.Any(e => e.ErrorCode == "HSDS_ADDITIONAL_REQUEST_FIELD"), Is.True);
            Assert.That(result.SpecificationValidation.Errors.Any(e =>
                e.ErrorCode == "HSDS_ADDITIONAL_REQUEST_FIELD" &&
                string.Equals(e.Severity, "Info", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_UsesWarmupCachedProfileBeforeExternalProfileFetch()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions(),
        };

        _schemaResolverServiceMock
            .Setup(service => service.ResolveAsync(It.Is<string>(s => s.Contains("\"$ref\"", StringComparison.Ordinal) && s.Contains(hsdsSpecUrl, StringComparison.OrdinalIgnoreCase)), hsdsSpecUrl, It.IsAny<DataSourceAuthentication?>()))
            .ReturnsAsync(CreateHsdsProfileSpec());

        var requestCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString() ?? string.Empty;
            requestCounts[requestUrl] = requestCounts.TryGetValue(requestUrl, out var count) ? count + 1 : 1;

            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateFeedSpecMissingRequiredHsdsEndpoint())
                };
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.SpecificationValidation, Is.Not.Null);
            Assert.That(result.SpecificationValidation!.Errors.Any(e => e.ErrorCode == "HSDS_MISSING_ENDPOINT"), Is.True);

            Assert.That(requestCounts.TryGetValue(feedSpecUrl, out var feedFetchCount), Is.True);
            Assert.That(feedFetchCount, Is.EqualTo(1));
            Assert.That(requestCounts.ContainsKey(hsdsSpecUrl), Is.False, "HSDS profile URL should not be fetched when warmup-path resolver returns it.");
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_FastMode_DoesNotRunFullValidationPass()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";

        _jsonValidatorServiceMock.Invocations.Clear();
        _jsonValidatorServiceMock
            .Setup(service => service.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = true,
                Errors = [],
                SchemaVersion = "test",
                Duration = TimeSpan.Zero
            });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions()
        };

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString() ?? string.Empty;
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateFeedSpecPermissiveOrganisationResponse())
                };
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"id\":\"1\"}]")
            };
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result, Is.Not.Null);
        _jsonValidatorServiceMock.Verify(service => service.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_FullMode_RunsHsdsRuntimeValidationAndCanFail()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";

        var hsdsComplianceServiceMock = new Mock<IHsdsComplianceService>();
        hsdsComplianceServiceMock
            .Setup(s => s.ExtractClaimedProfileVersion(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("3.0");

        hsdsComplianceServiceMock
            .Setup(s => s.TryGetKnownHsdsSchemaUrl("3.0", out hsdsSpecUrl))
            .Returns(true);

        hsdsComplianceServiceMock
            .Setup(s => s.CompareFeedSpecAgainstHsdsProfile(It.IsAny<JsonNode>(), It.IsAny<JsonNode>()))
            .Returns([]);

        hsdsComplianceServiceMock
            .Setup(s => s.ValidateEndpointResponsesAgainstHsdsProfileAsync(
                It.IsAny<List<EndpointTestResult>>(),
                It.IsAny<JsonNode>(),
                It.IsAny<OpenApiValidationOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<List<EndpointTestResult>, JsonNode, OpenApiValidationOptions, CancellationToken>((tests, _, _, _) =>
            {
                if (tests.Count == 0)
                {
                    return;
                }

                tests[0].Status = EndpointTestStatus.FailedValidation;
                tests[0].TestResults[0].ValidationResult = new ValidationResult
                {
                    IsValid = false,
                    Errors =
                    [
                        new()
                        {
                            Path = "[0].name",
                            Message = "HSDS runtime validation: Required property 'name' not found",
                            ErrorCode = "HSDS_RUNTIME_VALIDATION_ERROR",
                            Severity = "Error"
                        }
                    ],
                    SchemaVersion = "test",
                    Duration = TimeSpan.Zero
                };
                tests[0].RefreshFlattenedFields();
            })
            .Returns(Task.CompletedTask);

        var endpointTestingServiceMock = new Mock<IEndpointTestingService>();
        endpointTestingServiceMock
            .Setup(s => s.TestEndpointsAsync(
                It.IsAny<JsonObject>(),
                It.IsAny<string>(),
                It.IsAny<OpenApiValidationOptions>(),
                It.IsAny<DataSourceAuthentication>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new()
                {
                    Path = "/organisations",
                    Method = "GET",
                    IsTested = true,
                    Status = EndpointTestStatus.PassedValidation,
                    TestResults =
                    [
                        new()
                        {
                            ResponseStatusCode = 200,
                            ResponseBody = System.Text.Encoding.UTF8.GetBytes("[{\"id\":\"1\"}]"),
                            IsSuccessStatusCode = true,
                            ValidationResult = new ValidationResult
                            {
                                IsValid = true,
                                Errors = [],
                                SchemaVersion = "test",
                                Duration = TimeSpan.Zero
                            }
                        }
                    ]
                }
            ]);

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions()
        };

        var fullModeServerOptions = Options.Create(new OpenApiValidationServerOptions
        {
            HsdsValidationMode = HsdsValidationMode.Full,
            TestEndpoints = true
        });

        var fullModeHttpClient = TestHttpClientFactory.CreateClient(new MockHttpMessageHandler((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString() ?? string.Empty;
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateFeedSpecPermissiveOrganisationResponse())
                };
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"id\":\"1\"}]")
            };
        }));

        using var _ = fullModeHttpClient;

        var serviceWithFullMode = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(fullModeHttpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
_openApiSpecificationService,
            hsdsComplianceServiceMock.Object,
            endpointTestingServiceMock.Object,
            null!,

            _openApiBootstrapServiceMock.Object,

            cacheOptions: null,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-1.0"] = "https://openreferraluk.org/specifications/1.0/openapi.json",
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: fullModeServerOptions);

        // Act
        var result = await serviceWithFullMode.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary!.FailedTests, Is.EqualTo(1));
            Assert.That(result.Summary.SuccessfulTests, Is.Zero);
            Assert.That(result.Summary.TotalEndpoints, Is.EqualTo(1));
            Assert.That(result.EndpointTests, Has.Count.EqualTo(1));
            Assert.That(result.EndpointTests[0].Status, Is.EqualTo(EndpointTestStatus.FailedValidation));
            Assert.That(result.EndpointTests[0].TestResults[0].ValidationResult, Is.Not.Null);
            Assert.That(result.EndpointTests[0].TestResults[0].ValidationResult!.Errors.Any(e =>
                string.Equals(e.ErrorCode, "HSDS_RUNTIME_VALIDATION_ERROR", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(result.EndpointTests[0].ValidationErrors.Any(e =>
                string.Equals(e.ErrorCode, "HSDS_RUNTIME_VALIDATION_ERROR", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
        hsdsComplianceServiceMock.Verify(s => s.ValidateEndpointResponsesAgainstHsdsProfileAsync(
            It.IsAny<List<EndpointTestResult>>(),
            It.IsAny<JsonNode>(),
            It.IsAny<OpenApiValidationOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_FullMode_WhenFeedSpecFetchFails_SkipsSecondHsdsPassAndAddsNotification()
    {
        // Arrange – feed spec fetch will fail; the HSDS profile spec is the fallback.
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";

        var hsdsComplianceServiceMock = new Mock<IHsdsComplianceService>();
        hsdsComplianceServiceMock
            .Setup(s => s.ExtractClaimedProfileVersion(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("3.0");

        hsdsComplianceServiceMock
            .Setup(s => s.TryGetKnownHsdsSchemaUrl("3.0", out hsdsSpecUrl))
            .Returns(true);

        hsdsComplianceServiceMock
            .Setup(s => s.CompareFeedSpecAgainstHsdsProfile(It.IsAny<JsonNode>(), It.IsAny<JsonNode>()))
            .Returns([]);

        var endpointTestingServiceMock = new Mock<IEndpointTestingService>();
        endpointTestingServiceMock
            .Setup(s => s.TestEndpointsAsync(
                It.IsAny<JsonObject>(),
                It.IsAny<string>(),
                It.IsAny<OpenApiValidationOptions>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new()
                {
                    Path = "/organisations",
                    Method = "GET",
                    IsTested = true,
                    Status = EndpointTestStatus.PassedValidation,
                    TestResults =
                    [
                        new()
                        {
                            ResponseStatusCode = 200,
                            ResponseBody = System.Text.Encoding.UTF8.GetBytes("[{\"id\":\"1\"}]"),
                            IsSuccessStatusCode = true,
                            ValidationResult = new ValidationResult
                            {
                                IsValid = true,
                                Errors = [],
                                SchemaVersion = "test",
                                Duration = TimeSpan.Zero
                            }
                        }
                    ]
                }
            ]);

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions()
        };

        var fullModeServerOptions = Options.Create(new OpenApiValidationServerOptions
        {
            HsdsValidationMode = HsdsValidationMode.Full,
            TestEndpoints = true
        });

        // Feed spec URL returns a network error; HSDS profile URL succeeds.
        var fullModeHttpClient = TestHttpClientFactory.CreateClient(new MockHttpMessageHandler((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString() ?? string.Empty;
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException("Simulated network failure fetching feed spec");
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"id\":\"1\"}]")
            };
        }));

        using var _ = fullModeHttpClient;

        var serviceWithFullMode = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(fullModeHttpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
_openApiSpecificationService,
            hsdsComplianceServiceMock.Object,
            endpointTestingServiceMock.Object,
            null!,

            _openApiBootstrapServiceMock.Object,


            cacheOptions: null,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-1.0"] = "https://openreferraluk.org/specifications/1.0/openapi.json",
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: fullModeServerOptions);

        // Act
        var result = await serviceWithFullMode.ValidateOpenApiSpecificationAsync(request);

        // Assert – the fallback notification is present.
        Assert.That(result.Notifications, Has.Some.Contains("Falling back to the HSDS profile OpenAPI specification"));

        // Assert – the skip notification explains why the second pass was not run.
        Assert.That(result.Notifications, Has.Some.Contains("Full HSDS runtime validation was skipped"));

        // Assert – ValidateEndpointResponsesAgainstHsdsProfileAsync must NOT have been called,
        // because TestEndpointsAsync already ran against the HSDS profile spec (the fallback).
        hsdsComplianceServiceMock.Verify(s => s.ValidateEndpointResponsesAgainstHsdsProfileAsync(
            It.IsAny<List<EndpointTestResult>>(),
            It.IsAny<JsonNode>(),
            It.IsAny<OpenApiValidationOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenOwnSchemaValidationStrict_FailsEndpointValidation()
    {
        // Arrange
        _jsonValidatorServiceMock
            .Setup(service => service.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                Errors =
                [
                    new()
                    {
                        Path = "[].extra",
                        Message = "Field '[].extra' is not defined in the schema",
                        ErrorCode = "ADDITIONAL_FIELD",
                        Severity = "Warning"
                    }
                ],
                SchemaVersion = "test",
                Duration = TimeSpan.Zero
            });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions
            {
                // ValidateSpecification = false
                ReportAdditionalFields = true
            }
        };

        SetupHttpMock(CreateOpenApi30SpecWithResponseSchema(), endpointResponseBody: "[{\"name\":\"ok\",\"extra\":\"x\"}]");

        var strictValidationOptions = Options.Create(new OpenApiValidationServerOptions { OwnSchemaValidation = OwnSchemaValidationMode.Strict, ValidateSpecification = false, TestEndpoints = true });
        var serviceWithStrictPolicy = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
            _openApiBootstrapServiceMock.Object,
            openApiValidationServerOptions: strictValidationOptions);

        // Act — default server setting OwnSchemaValidation = Strict causes errors
        var result = await serviceWithStrictPolicy.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.EndpointTests[0].Status, Is.EqualTo(EndpointTestStatus.FailedValidation));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenOwnSchemaValidationAllowAdditionalProperties_ReportsWarningsWithoutFailure()
    {
        // Arrange
        _jsonValidatorServiceMock
            .Setup(service => service.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                Errors =
                [
                    new()
                    {
                        Path = "[].extra",
                        Message = "Field '[].extra' is not defined in the schema",
                        ErrorCode = "ADDITIONAL_FIELD",
                        Severity = "Warning"
                    }
                ],
                SchemaVersion = "test",
                Duration = TimeSpan.Zero
            });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions
            {
                // ValidateSpecification = false
                ReportAdditionalFields = true
            }
        };

        SetupHttpMock(CreateOpenApi30SpecWithResponseSchema(), endpointResponseBody: "[{\"name\":\"ok\",\"extra\":\"x\"}]");

        var lenientValidationOptions = Options.Create(new OpenApiValidationServerOptions { OwnSchemaValidation = OwnSchemaValidationMode.AllowAdditionalProperties, ValidateSpecification = false, TestEndpoints = true });
        var serviceWithLenientPolicy = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
_openApiSpecificationService,
            null!,
            null!,
            null!,

            _openApiBootstrapServiceMock.Object,
            openApiValidationServerOptions: lenientValidationOptions);

        // Act — server setting OwnSchemaValidation = AllowAdditionalProperties downgrades additional-field errors to warnings
        var result = await serviceWithLenientPolicy.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.EndpointTests[0].Status, Is.EqualTo(EndpointTestStatus.PassedWithWarnings));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenOwnSchemaValidationTrue_UsesOwnFeedSpec()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";
        JsonObject? capturedSpec = null;

        var hsdsComplianceServiceMock = new Mock<IHsdsComplianceService>();
        hsdsComplianceServiceMock
            .Setup(s => s.ExtractClaimedProfileVersion(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("3.0");
        hsdsComplianceServiceMock
            .Setup(s => s.TryGetKnownHsdsSchemaUrl("3.0", out hsdsSpecUrl))
            .Returns(true);
        hsdsComplianceServiceMock
            .Setup(s => s.CompareFeedSpecAgainstHsdsProfile(It.IsAny<JsonNode>(), It.IsAny<JsonNode>()))
            .Returns([]);

        var endpointTestingServiceMock = new Mock<IEndpointTestingService>();
        endpointTestingServiceMock
            .Setup(s => s.TestEndpointsAsync(
                It.IsAny<JsonObject>(),
                It.IsAny<string>(),
                It.IsAny<OpenApiValidationOptions>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .Callback<JsonObject, string, OpenApiValidationOptions, DataSourceAuthentication?, CancellationToken>(
                (spec, _, _, _, _) => capturedSpec = spec)
            .ReturnsAsync([]);

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions()
        };

        var serverOptions = Options.Create(new OpenApiValidationServerOptions
        {
            OwnSchemaValidation = OwnSchemaValidationMode.Strict,
            ValidateSpecification = false,
            TestEndpoints = true
        });

        var httpClient = TestHttpClientFactory.CreateClient(new MockHttpMessageHandler((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString() ?? string.Empty;
            var body = requestUrl.Equals(feedSpecUrl, StringComparison.OrdinalIgnoreCase)
                ? CreateFeedSpecPermissiveOrganisationResponse()
                : CreateHsdsProfileSpec();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };
        }));
        using var _ = httpClient;

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
_openApiSpecificationService,
            hsdsComplianceServiceMock.Object,
            endpointTestingServiceMock.Object,
            null!,

            _openApiBootstrapServiceMock.Object,

            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: serverOptions);

        // Act
        var result = await service.ValidateOpenApiSpecificationAsync(request);

        // Assert – endpoint testing should have used the feed's own spec (title "Feed API")
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedSpec, Is.Not.Null);
            Assert.That(capturedSpec!["info"]?["title"]?.ToString(), Is.EqualTo("Feed API"));
            Assert.That(result.Notifications, Has.None.Contains("OwnSchemaValidation is set to None"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenOwnSchemaValidationNone_UsesHsdsProfileSpec()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";
        JsonObject? capturedSpec = null;

        var hsdsComplianceServiceMock = new Mock<IHsdsComplianceService>();
        hsdsComplianceServiceMock
            .Setup(s => s.ExtractClaimedProfileVersion(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("3.0");
        hsdsComplianceServiceMock
            .Setup(s => s.TryGetKnownHsdsSchemaUrl("3.0", out hsdsSpecUrl))
            .Returns(true);
        hsdsComplianceServiceMock
            .Setup(s => s.CompareFeedSpecAgainstHsdsProfile(It.IsAny<JsonNode>(), It.IsAny<JsonNode>()))
            .Returns([]);

        var endpointTestingServiceMock = new Mock<IEndpointTestingService>();
        endpointTestingServiceMock
            .Setup(s => s.TestEndpointsAsync(
                It.IsAny<JsonObject>(),
                It.IsAny<string>(),
                It.IsAny<OpenApiValidationOptions>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .Callback<JsonObject, string, OpenApiValidationOptions, DataSourceAuthentication?, CancellationToken>(
                (spec, _, _, _, _) => capturedSpec = spec)
            .ReturnsAsync([]);

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions()
        };

        var serverOptions = Options.Create(new OpenApiValidationServerOptions
        {
            OwnSchemaValidation = OwnSchemaValidationMode.None,
            ValidateSpecification = false,
            TestEndpoints = true
        });

        var httpClient = TestHttpClientFactory.CreateClient(new MockHttpMessageHandler((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString() ?? string.Empty;
            var body = requestUrl.Equals(feedSpecUrl, StringComparison.OrdinalIgnoreCase)
                ? CreateFeedSpecPermissiveOrganisationResponse()
                : CreateHsdsProfileSpec();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };
        }));
        using var _1 = httpClient;

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            hsdsComplianceServiceMock.Object,
            endpointTestingServiceMock.Object,
            null!,
            _openApiBootstrapServiceMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: serverOptions);

        // Act
        var result = await service.ValidateOpenApiSpecificationAsync(request);

        // Assert – endpoint testing should have used the HSDS profile spec (title "HSDS Profile")
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedSpec, Is.Not.Null);
            Assert.That(capturedSpec!["info"]?["title"]?.ToString(), Is.EqualTo("HSDS Profile"));
            Assert.That(result.Notifications, Has.Some.Contains("OwnSchemaValidation is set to None"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenOwnSchemaValidationNone_AndNoHsdsProfileAvailable_FallsBackToFeedSpec()
    {
        // Arrange
        var feedSpecUrl = "https://unknown-version.example.com/openapi.json";
        JsonObject? capturedSpec = null;

        var hsdsComplianceServiceMock = new Mock<IHsdsComplianceService>();
        hsdsComplianceServiceMock
            .Setup(s => s.ExtractClaimedProfileVersion(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(string.Empty);
        string? nullUrl = null;
        hsdsComplianceServiceMock
            .Setup(s => s.TryGetKnownHsdsSchemaUrl(It.IsAny<string>(), out nullUrl))
            .Returns(false);

        var endpointTestingServiceMock = new Mock<IEndpointTestingService>();
        endpointTestingServiceMock
            .Setup(s => s.TestEndpointsAsync(
                It.IsAny<JsonObject>(),
                It.IsAny<string>(),
                It.IsAny<OpenApiValidationOptions>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .Callback<JsonObject, string, OpenApiValidationOptions, DataSourceAuthentication?, CancellationToken>(
                (spec, _, _, _, _) => capturedSpec = spec)
            .ReturnsAsync([]);

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://unknown-version.example.com",
            Options = new OpenApiValidationOptions()
        };

        var serverOptions = Options.Create(new OpenApiValidationServerOptions
        {
            OwnSchemaValidation = OwnSchemaValidationMode.None,
            ValidateSpecification = false,
            TestEndpoints = true
        });

        var httpClient = TestHttpClientFactory.CreateClient(new MockHttpMessageHandler((httpRequest, ct) =>
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(CreateFeedSpecPermissiveOrganisationResponse())
            };
        }));
        using var _ = httpClient;

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            hsdsComplianceServiceMock.Object,
            endpointTestingServiceMock.Object,
            null!,
            _openApiBootstrapServiceMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions()),
            openApiValidationServerOptions: serverOptions);

        // Act
        var result = await service.ValidateOpenApiSpecificationAsync(request);

        // Assert – no HSDS profile available, so must fall back to the feed's own spec
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedSpec, Is.Not.Null);
            Assert.That(capturedSpec!["info"]?["title"]?.ToString(), Is.EqualTo("Feed API"));
            Assert.That(result.Notifications, Has.Some.Contains("falling back to the feed's own schema"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenOwnSchemaValidationNone_WithFull_SkipsSecondPass()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";

        var hsdsComplianceServiceMock = new Mock<IHsdsComplianceService>();
        hsdsComplianceServiceMock
            .Setup(s => s.ExtractClaimedProfileVersion(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("3.0");
        hsdsComplianceServiceMock
            .Setup(s => s.TryGetKnownHsdsSchemaUrl("3.0", out hsdsSpecUrl))
            .Returns(true);
        hsdsComplianceServiceMock
            .Setup(s => s.CompareFeedSpecAgainstHsdsProfile(It.IsAny<JsonNode>(), It.IsAny<JsonNode>()))
            .Returns([]);

        var endpointTestingServiceMock = new Mock<IEndpointTestingService>();
        endpointTestingServiceMock
            .Setup(s => s.TestEndpointsAsync(
                It.IsAny<JsonObject>(),
                It.IsAny<string>(),
                It.IsAny<OpenApiValidationOptions>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions()
        };

        var serverOptions = Options.Create(new OpenApiValidationServerOptions
        {
            OwnSchemaValidation = OwnSchemaValidationMode.None,
            HsdsValidationMode = HsdsValidationMode.Full,
            ValidateSpecification = false,
            TestEndpoints = true
        });

        var httpClient = TestHttpClientFactory.CreateClient(new MockHttpMessageHandler((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString() ?? string.Empty;
            var body = requestUrl.Equals(feedSpecUrl, StringComparison.OrdinalIgnoreCase)
                ? CreateFeedSpecPermissiveOrganisationResponse()
                : CreateHsdsProfileSpec();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };
        }));
        using var _ = httpClient;

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            hsdsComplianceServiceMock.Object,
            endpointTestingServiceMock.Object,
            null!,
            _openApiBootstrapServiceMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: serverOptions);

        // Act
        var result = await service.ValidateOpenApiSpecificationAsync(request);

        // Assert – second pass must not be invoked when OwnSchemaValidation is None
        hsdsComplianceServiceMock.Verify(s => s.ValidateEndpointResponsesAgainstHsdsProfileAsync(
            It.IsAny<List<EndpointTestResult>>(),
            It.IsAny<JsonNode>(),
            It.IsAny<OpenApiValidationOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(result.Notifications, Has.Some.Contains("Full HSDS runtime validation was skipped"));
    }

    #endregion

    #region HTTP Response Handling

    [Test]
    public void ValidateOpenApiSpecificationAsync_ReturnsFailureForHttpNotFound()
    {
        // Arrange
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/notfound.json",
            BaseUrl = "https://api.example.com"
        };

        var mockHandler = new MockHttpMessageHandler((req, ct) =>
            new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));

        var httpClient = TestHttpClientFactory.CreateClient(mockHandler);
        var service = new OpenApiValidationService(
            _loggerMock.Object, CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
             _openApiBootstrapServiceMock.Object);

        try
        {
            // Act
            var result = service.ValidateOpenApiSpecificationAsync(request).GetAwaiter().GetResult();

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Summary, Is.Not.Null);
                Assert.That(result.Metadata, Is.Null);
                Assert.That(result.Notifications, Has.Count.EqualTo(1));
                Assert.That(result.Notifications[0], Does.Contain("Unable to get or resolve the OpenAPI specification"));
                Assert.That(result.Notifications[0], Does.Contain("https://example.com/notfound.json"));
            }
        }
        finally
        {
            httpClient?.Dispose();
        }
    }

    [Test]
    public void ValidateOpenApiSpecificationAsync_ReturnsFailureForNetworkError()
    {
        // Arrange
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://invalid.example.com/openapi.json",
            BaseUrl = "https://api.example.com"
        };

        var mockHandler = new MockHttpMessageHandler((req, ct) =>
            throw new HttpRequestException("Network failed"));

        var httpClient = TestHttpClientFactory.CreateClient(mockHandler);
        var service = new OpenApiValidationService(
            _loggerMock.Object, CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
             _openApiBootstrapServiceMock.Object);

        try
        {
            // Act
            var result = service.ValidateOpenApiSpecificationAsync(request).GetAwaiter().GetResult();

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Summary, Is.Not.Null);
                Assert.That(result.Metadata, Is.Null);
                Assert.That(result.Notifications, Has.Count.EqualTo(1));
                Assert.That(result.Notifications[0], Does.Contain("Unable to get or resolve the OpenAPI specification"));
                Assert.That(result.Notifications[0], Does.Contain("https://invalid.example.com/openapi.json"));
            }
        }
        finally
        {
            httpClient?.Dispose();
        }
    }

    [Test]
    public void ValidateOpenApiSpecificationAsync_AddsNotificationWhenBaseUrlDiscoveryFails()
    {
        // Arrange
        var request = new OpenApiValidationRequest
        {
            BaseUrl = "https://directory.southampton.gov.uk/api"
        };

        var httpClient = TestHttpClientFactory.CreateClient(new MockHttpMessageHandler((req, ct) =>
            new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)));
        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
            _openApiBootstrapServiceMock.Object);

        try
        {
            // Act
            var result = service.ValidateOpenApiSpecificationAsync(request).GetAwaiter().GetResult();

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Notifications, Has.Count.EqualTo(1));
                Assert.That(result.Notifications[0], Does.Contain("Unable to get or resolve the OpenAPI specification"));
                Assert.That(result.Notifications[0], Does.Contain("Failed to discover OpenAPI schema URL or schema content from base URL"));
            }
        }
        finally
        {
            httpClient?.Dispose();
        }
    }

    [Test]
    public void ValidateOpenApiSpecificationAsync_ReturnsFailureForInvalidJson()
    {
        // Arrange
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/invalid.json",
            BaseUrl = "https://api.example.com"
        };

        var mockHandler = new MockHttpMessageHandler((req, ct) =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("Not valid JSON at all {{{")
            });

        var httpClient = TestHttpClientFactory.CreateClient(mockHandler);
        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
            _openApiBootstrapServiceMock.Object);

        try
        {
            // Act
            var result = service.ValidateOpenApiSpecificationAsync(request).GetAwaiter().GetResult();

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Summary, Is.Not.Null);
                Assert.That(result.Metadata, Is.Null);
                Assert.That(result.Notifications, Has.Count.EqualTo(1));
                Assert.That(result.Notifications[0], Does.Contain("Unable to get or resolve the OpenAPI specification"));
                Assert.That(result.Notifications[0], Does.Contain("https://example.com/invalid.json"));
            }
        }
        finally
        {
            httpClient?.Dispose();
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_FallsBackToHsdsProfileWhenFeedOpenApiFetchFails()
    {
        // Arrange
        var feedSpecUrl = "https://feed.example.com/openapi-missing.json";
        var hsdsSpecUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions()
        };

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString() ?? string.Empty;

            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }

            if (string.Equals(requestUrl, hsdsSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Notifications.Any(n => n.Contains("Falling back to the HSDS profile OpenAPI specification", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(request.OwnSchemaUrl, Is.EqualTo(hsdsSpecUrl));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_UsesCachedResolvedFeedSpecBeforeRefetching()
    {
        // Arrange
        var uniqueFeedSpecUrl = $"https://cache-test.example.com/{Guid.NewGuid():N}/openapi.json";
        var feedSpec = @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Cache Test API"",
                ""version"": ""feed""
            },
            ""paths"": {
                ""/test"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";

        var requestCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        SetupHttpMock((httpRequest, ct) =>
        {
            var requestUrl = httpRequest.RequestUri?.ToString() ?? string.Empty;
            requestCounts.TryGetValue(requestUrl, out var currentCount);
            requestCounts[requestUrl] = currentCount + 1;

            if (string.Equals(requestUrl, uniqueFeedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(feedSpec)
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        var serviceWithCache = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
_openApiSpecificationService,
            null!,
            null!,
            null!,
            _openApiBootstrapServiceMock.Object,
            cacheOptions: Options.Create(new CacheOptions
            {
                Enabled = true,
                ExpirationMinutes = 30
            }),
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions { ValidateSpecification = false }));

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = uniqueFeedSpecUrl,
            BaseUrl = "https://cache-test.example.com",
            Options = new OpenApiValidationOptions()
        };

        // Act
        var firstResult = await serviceWithCache.ValidateOpenApiSpecificationAsync(request);
        var secondResult = await serviceWithCache.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstResult, Is.Not.Null);
            Assert.That(secondResult, Is.Not.Null);
            Assert.That(requestCounts.TryGetValue(uniqueFeedSpecUrl, out var feedFetchCount), Is.True);
            Assert.That(feedFetchCount, Is.EqualTo(1));
        }
    }

    #endregion

    #region Cancellation Support

    [Test]
    public void ValidateOpenApiSpecificationAsync_RespectsCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com"
        };
        var mockHandler = new MockHttpMessageHandler((req, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
        });
        _httpClient?.Dispose();
        _httpClient = TestHttpClientFactory.CreateClient(mockHandler);
        _service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
            _openApiBootstrapServiceMock.Object);

        // Act
        var result = _service.ValidateOpenApiSpecificationAsync(request, cts.Token).GetAwaiter().GetResult();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Summary, Is.Not.Null);
    }

    #endregion

    #region Options Processing

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_RespondsToResponseBodyOption()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions { IncludeResponseBody = false }
        };
        SetupHttpMock(json, endpointResponseBody: "{\"data\":[{\"id\":\"1\"}]}");

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.EndpointTests, Is.Not.Empty);
        Assert.That(result.EndpointTests.SelectMany(e => e.TestResults), Is.Not.Empty);
        Assert.That(result.EndpointTests.SelectMany(e => e.TestResults).All(tr => tr.ResponseBody == null), Is.True);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenServerResponseBodyCapConfigured_TruncatesRetainedBodies()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions { IncludeResponseBody = true }
        };

        SetupHttpMock(json, endpointResponseBody: "{\"data\":[{\"id\":\"123456789012345\"}]}");

        var serviceWithCap = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            _openApiSpecificationService,
            null!,
            null!,
            null!,
            _openApiBootstrapServiceMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-1.0"] = "https://openreferraluk.org/specifications/1.0/openapi.json",
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                HsdsValidationMode = HsdsValidationMode.Fast,
                AllowUserSuppliedAuth = true,
                ValidateSpecification = true,
                TestEndpoints = true,
                TestOptionalEndpoints = true,
                TreatOptionalEndpointsAsWarnings = true,
                MaxRetainedResponseBodyCharacters = 10
            }));

        // Act
        var result = await serviceWithCap.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result.EndpointTests, Is.Not.Empty);
        var retainedBodies = result.EndpointTests
            .SelectMany(e => e.TestResults)
            .Select(tr => tr.ResponseBody)
            .Where(body => body != null)
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(retainedBodies, Is.Not.Empty);
            Assert.That(retainedBodies.All(body => body!.Length == OpenApiValidationService.TruncatedPlaceholder.Length), Is.True);
            Assert.That(result.Notifications.Any(n => n.Contains("Response bodies were omitted", StringComparison.Ordinal)), Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_RespondsToTestResultsOption()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions { IncludeTestResults = false }
        };
        SetupHttpMock(json, endpointResponseBody: "{\"data\":[{\"id\":\"1\"}]}");

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.EndpointTests, Is.Not.Empty);
            Assert.That(result.EndpointTests.All(e => e.TestResults.Count == 0), Is.True);
            Assert.That(result.EndpointTests.All(e => e.ValidationErrors != null), Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithIncludeTestResultsFalse_PreservesFlattenedValidationErrors()
    {
        // Arrange
        var json = CreateOpenApi30SpecWithResponseSchema();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions
            {
                IncludeTestResults = false
                // ValidateSpecification = false
            }
        };

        _jsonValidatorServiceMock
            .Setup(service => service.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                Errors =
                [
                    new()
                    {
                        Path = "data[0].name",
                        Message = "data[0].name is required",
                        ErrorCode = "VALIDATION_ERROR",
                        Severity = "Error"
                    }
                ]
            });

        SetupHttpMock(json, endpointResponseBody: "[{\"name\":\"a\"}]");

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.EndpointTests, Is.Not.Empty);
            Assert.That(result.EndpointTests.All(e => e.TestResults.Count == 0), Is.True);
            Assert.That(result.EndpointTests.Any(e => e.ValidationErrors.Count > 0), Is.True);
        }
    }

    #endregion

    #region Endpoint Testing

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithPaginatedEndpoint_RequestsMultiplePages()
    {
        // Arrange
        var json = CreateOpenApi30PaginatedSpec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                };
            }

            var responseBody = requestUri.Contains("page=3", StringComparison.OrdinalIgnoreCase)
                ? "{\"total_pages\":3,\"data\":[{\"id\":\"3\"}]}"
                : requestUri.Contains("page=2", StringComparison.OrdinalIgnoreCase)
                    ? "{\"total_pages\":3,\"data\":[{\"id\":\"2\"}]}"
                    : "{\"total_pages\":3,\"data\":[{\"id\":\"1\"}]}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result.EndpointTests, Has.Count.EqualTo(1));
        Assert.That(result.EndpointTests[0].IsTested, Is.True);
        Assert.That(result.EndpointTests[0].Status, Is.EqualTo(EndpointTestStatus.PassedValidation));
        Assert.That(result.EndpointTests[0].TestResults, Has.Count.EqualTo(3));
        Assert.That(result.EndpointTests[0].TestResults.All(tr => tr.IsSuccessStatusCode), Is.True);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_PerformsFullSuiteAfterSpecAndHsdsChecks()
    {
        // Arrange
        var callOrder = new List<string>();
        var feedSpecUrl = "https://feed.example.com/openapi.json";
        var hsdsProfileUrl = "https://openreferraluk.org/specifications/3.0/openapi.json";

        var specServiceMock = new Mock<IOpenApiSpecificationService>();
        specServiceMock
            .Setup(s => s.ValidateAsync(It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("spec"))
            .ReturnsAsync(new OpenApiSpecificationValidation
            {
                IsValid = false,
                Errors =
                [
                    new()
                    {
                        Path = "openapi",
                        Message = "Declared spec validation failed",
                        ErrorCode = "SPEC_ERROR",
                        Severity = "Error"
                    }
                ]
            });

        var hsdsServiceMock = new Mock<IHsdsComplianceService>();
        hsdsServiceMock
            .Setup(s => s.ExtractClaimedProfileVersion(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("3.0");

        hsdsServiceMock
            .Setup(s => s.TryGetKnownHsdsSchemaUrl(It.IsAny<string>(), out hsdsProfileUrl))
            .Returns(true);

        hsdsServiceMock
            .Setup(s => s.CompareFeedSpecAgainstHsdsProfile(It.IsAny<JsonNode>(), It.IsAny<JsonNode>()))
            .Callback(() => callOrder.Add("hsds"))
            .Returns(
            [
                new()
                {
                    Path = "paths.GET /required",
                    Message = "Missing required HSDS endpoint",
                    ErrorCode = "HSDS_MISSING_ENDPOINT",
                    Severity = "Error"
                }
            ]);

        hsdsServiceMock
            .Setup(s => s.ValidateEndpointResponsesAgainstHsdsProfileAsync(
                It.IsAny<List<EndpointTestResult>>(),
                It.IsAny<JsonNode>(),
                It.IsAny<OpenApiValidationOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var endpointTestingMock = new Mock<IEndpointTestingService>();
        endpointTestingMock
            .Setup(s => s.TestEndpointsAsync(
                It.IsAny<JsonObject>(),
                It.IsAny<string>(),
                It.IsAny<OpenApiValidationOptions>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("endpoints"))
            .ReturnsAsync(
            [
                new()
                {
                    Path = "/services",
                    Method = "GET",
                    IsTested = true,
                    Status = EndpointTestStatus.PassedValidation,
                    TestResults = []
                }
            ]);

        var httpClient = TestHttpClientFactory.CreateClient(new MockHttpMessageHandler((req, ct) =>
        {
            var requestUrl = req.RequestUri?.ToString() ?? string.Empty;
            if (string.Equals(requestUrl, feedSpecUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateOpenApi30Spec())
                };
            }

            if (string.Equals(requestUrl, hsdsProfileUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateHsdsProfileSpec())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }));

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
            specServiceMock.Object,
            hsdsServiceMock.Object,
            endpointTestingMock.Object,
            null!,
            _openApiBootstrapServiceMock.Object,
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions { ValidateSpecification = true, TestEndpoints = true }));

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = feedSpecUrl,
            BaseUrl = "https://feed.example.com",
            Options = new OpenApiValidationOptions()
        };

        try
        {
            // Act
            var result = await service.ValidateOpenApiSpecificationAsync(request);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(callOrder, Is.EqualTo(["spec", "hsds", "endpoints"]));
                Assert.That(result.EndpointTests, Has.Count.EqualTo(1));
                Assert.That(result.SpecificationValidation, Is.Not.Null);
                Assert.That(result.SpecificationValidation!.Errors.Any(e => e.ErrorCode == "SPEC_ERROR"), Is.True);
                Assert.That(result.SpecificationValidation.Errors.Any(e => e.ErrorCode == "HSDS_MISSING_ENDPOINT"), Is.True);
            }
        }
        finally
        {
            httpClient.Dispose();
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithEmptyPaginatedFeed_AddsWarning()
    {
        // Arrange
        var json = CreateOpenApi30PaginatedSpec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{\"total_pages\":1,\"data\":[]}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.EndpointTests, Has.Count.EqualTo(1));
            Assert.That(result.EndpointTests[0].IsTested, Is.True);
            Assert.That(result.EndpointTests[0].Status, Is.EqualTo(EndpointTestStatus.PassedWithWarnings));
            Assert.That(result.EndpointTests[0].TestResults, Has.Count.EqualTo(1));
            Assert.That(result.EndpointTests[0].TestResults[0].ValidationResult, Is.Not.Null);
            Assert.That(result.EndpointTests[0].TestResults[0].ValidationResult!.Errors,
                Has.Some.Matches<Core.Models.Validation.ValidationError>(e => e.ErrorCode == "EMPTY_FEED_WARNING"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithPaginatedEndpoint_TestedEndpointsDoNotRemainNotTested()
    {
        // Arrange
        var json = CreateOpenApi30PaginatedSpec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"total_pages\":3,\"data\":[{\"id\":\"1\"}]}")
            };
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(result.EndpointTests, Is.Not.Empty);
        Assert.That(result.EndpointTests.Where(e => e.IsTested), Is.Not.Empty);
        Assert.That(result.EndpointTests.Where(e => e.IsTested).All(e => e.Status != EndpointTestStatus.NotTested), Is.True);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenPathsDuplicateBaseUrlPrefix_DeduplicatesBeforeEndpointTestingAndAddsWarning()
    {
        // Arrange
        const string specUrl = "https://example.com/api/v1/openapi.json";
        const string baseUrl = "https://example.com/api/v1";
        JsonObject? capturedOpenApi = null;

        var endpointTestingServiceMock = new Mock<IEndpointTestingService>();
        endpointTestingServiceMock
            .Setup(s => s.TestEndpointsAsync(
                It.IsAny<JsonObject>(),
                It.IsAny<string>(),
                It.IsAny<OpenApiValidationOptions>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .Callback<JsonObject, string, OpenApiValidationOptions, DataSourceAuthentication?, CancellationToken>((spec, _, _, _, _) =>
            {
                capturedOpenApi = (JsonObject)spec.DeepClone();
            })
            .ReturnsAsync([]);

        var httpClient = TestHttpClientFactory.CreateClient(new MockHttpMessageHandler((req, ct) =>
        {
            if (string.Equals(req.RequestUri?.ToString(), specUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateOpenApi30SpecWithPrefixedPaths())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }));

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
             _openApiSpecificationService,
            null!,
            endpointTestingServiceMock.Object,
            null!,
            _openApiBootstrapServiceMock.Object,

            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                HsdsValidationMode = _openApiValidationServerOptions.Value.HsdsValidationMode,
                AllowUserSuppliedAuth = _openApiValidationServerOptions.Value.AllowUserSuppliedAuth,
                ValidateSpecification = false,
                TestEndpoints = true
            }));

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = specUrl,
            BaseUrl = baseUrl,
            Options = new OpenApiValidationOptions()
        };

        try
        {
            // Act
            var result = await service.ValidateOpenApiSpecificationAsync(request);

            // Assert
            foreach (var n in result.Notifications)
            {
                Console.WriteLine("NOTIFICATION: " + n);
            }
            using (Assert.EnterMultipleScope())
            {
                Assert.That(capturedOpenApi, Is.Not.Null);
                var paths = capturedOpenApi!["paths"] as JsonObject;
                Assert.That(paths, Is.Not.Null);
                Assert.That(paths!.ContainsKey("/health"), Is.True);
                Assert.That(paths.ContainsKey("/services"), Is.True);
                Assert.That(paths.ContainsKey("/api/v1/health"), Is.False);
                Assert.That(paths.ContainsKey("/api/v1/services"), Is.False);
                Assert.That(result.Notifications.Any(n => n.Contains("Removed duplicated base URL prefix '/api/v1'", StringComparison.OrdinalIgnoreCase)), Is.True);
            }
        }
        finally
        {
            httpClient.Dispose();
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenDeduplicationWouldCollide_AddsWarningAndKeepsOriginalPath()
    {
        // Arrange
        const string specUrl = "https://example.com/api/v1/openapi.json";
        const string baseUrl = "https://example.com/api/v1";
        JsonObject? capturedOpenApi = null;

        var endpointTestingServiceMock = new Mock<IEndpointTestingService>();
        endpointTestingServiceMock
            .Setup(s => s.TestEndpointsAsync(
                It.IsAny<JsonObject>(),
                It.IsAny<string>(),
                It.IsAny<OpenApiValidationOptions>(),
                It.IsAny<DataSourceAuthentication?>(),
                It.IsAny<CancellationToken>()))
            .Callback<JsonObject, string, OpenApiValidationOptions, DataSourceAuthentication?, CancellationToken>((spec, _, _, _, _) =>
            {
                capturedOpenApi = (JsonObject)spec.DeepClone();
            })
            .ReturnsAsync([]);

        var httpClient = TestHttpClientFactory.CreateClient(new MockHttpMessageHandler((req, ct) =>
        {
            if (string.Equals(req.RequestUri?.ToString(), specUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateOpenApi30SpecWithPrefixedCollisionPaths())
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }));

        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
             _openApiSpecificationService,
            null!,
            endpointTestingServiceMock.Object,
            null!,
            _openApiBootstrapServiceMock.Object,
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                HsdsValidationMode = _openApiValidationServerOptions.Value.HsdsValidationMode,
                AllowUserSuppliedAuth = _openApiValidationServerOptions.Value.AllowUserSuppliedAuth,
                ValidateSpecification = false,
                TestEndpoints = true
            }));

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = specUrl,
            BaseUrl = baseUrl,
            Options = new OpenApiValidationOptions()
        };

        try
        {
            // Act
            var result = await service.ValidateOpenApiSpecificationAsync(request);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(capturedOpenApi, Is.Not.Null);
                var paths = capturedOpenApi!["paths"] as JsonObject;
                Assert.That(paths, Is.Not.Null);
                Assert.That(paths!.ContainsKey("/health"), Is.True);
                Assert.That(paths.ContainsKey("/api/v1/health"), Is.True);
                Assert.That(result.Notifications.Any(n => n.Contains("automatic de-duplication was skipped for colliding paths", StringComparison.OrdinalIgnoreCase)), Is.True);
            }
        }
        finally
        {
            httpClient.Dispose();
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_NormalizesAndDeduplicatesEndpointValidationErrorsAndWarnings()
    {
        // Arrange
        var json = CreateOpenApi30SpecWithResponseSchema();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions
            {
                //ValidateSpecification = false
            }
        };

        _jsonValidatorServiceMock
            .Setup(service => service.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                Errors =
                [
                    new()
                    {
                        Path = "data[0].name",
                        Message = "data[0].name is required",
                        ErrorCode = "VALIDATION_ERROR",
                        Severity = "Error"
                    },
                    new()
                    {
                        Path = "data[1].name",
                        Message = "data[1].name is required",
                        ErrorCode = "VALIDATION_ERROR",
                        Severity = "Error"
                    },
                    new()
                    {
                        Path = "data[0].extra",
                        Message = "data[0].extra is not expected",
                        ErrorCode = "VALIDATION_WARNING",
                        Severity = "Warning"
                    },
                    new()
                    {
                        Path = "data[4].extra",
                        Message = "data[4].extra is not expected",
                        ErrorCode = "VALIDATION_WARNING",
                        Severity = "Warning"
                    }
                ]
            });

        SetupHttpMock(json, endpointResponseBody: "[{\"name\":\"a\"},{\"name\":\"b\"}]");

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);
        var errors = result.EndpointTests[0].TestResults[0].ValidationResult!.Errors;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors, Has.Count.EqualTo(2));
            Assert.That(errors.All(e => e.Path.Contains("[]")), Is.True);
            Assert.That(errors.All(e => e.Message.Contains("[]")), Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_EndpointValidation_DeduplicatesByPathNotMessage()
    {
        // Arrange
        var json = CreateOpenApi30SpecWithResponseSchema();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions
            {
                //ValidateSpecification = false
            }
        };

        _jsonValidatorServiceMock
            .Setup(service => service.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                Errors =
                [
                    new()
                    {
                        Path = "data[0]",
                        Message = "data[0] should be object",
                        ErrorCode = "VALIDATION_ERROR",
                        Severity = "Error"
                    },
                    new()
                    {
                        Path = "data[0]",
                        Message = "data[0] missing required property 'name'",
                        ErrorCode = "VALIDATION_ERROR",
                        Severity = "Error"
                    }
                ]
            });

        SetupHttpMock(json, endpointResponseBody: "[{\"name\":\"a\"}]");

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);
        var errors = result.EndpointTests[0].TestResults[0].ValidationResult!.Errors;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0].Path, Is.EqualTo("data[]"));
            Assert.That(errors[0].Message, Is.EqualTo("data[] should be object"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_NoIdsAvailableWarning_IsNormalized()
    {
        // Arrange
        var json = CreateOpenApi30ParameterizedOnlySpecWithIndexedPath();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions
            {
                //ValidateSpecification = false
            }
        };

        SetupHttpMock(json);

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);
        var warning = result.EndpointTests[0].TestResults[0].ValidationResult!.Errors
            .First(e => e.ErrorCode == "NO_IDS_AVAILABLE");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.EndpointTests[0].Status, Is.EqualTo(EndpointTestStatus.NotTested));
            Assert.That(warning.Path, Does.Contain("[]"), "Path should retain array level markers");
            Assert.That(warning.Message, Does.Not.Contain("[0]"), "Message should not include concrete array indexes");
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithOptionalEndpointAnd404_ReturnsWarning()
    {
        // Arrange
        var json = CreateOpenApi30OptionalEndpointSpec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.EndpointTests, Has.Count.EqualTo(1));
            Assert.That(result.EndpointTests[0].Status, Is.EqualTo(EndpointTestStatus.PassedWithWarnings));
            Assert.That(result.EndpointTests[0].TestResults, Has.Count.EqualTo(1));
            Assert.That(result.EndpointTests[0].TestResults[0].ValidationResult, Is.Not.Null);
            Assert.That(result.EndpointTests[0].TestResults[0].ValidationResult!.Errors,
                Has.Some.Matches<Core.Models.Validation.ValidationError>(e => e.ErrorCode == "OPTIONAL_ENDPOINT_NON_SUCCESS" && e.Severity == "Warning"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithOptionalEndpointSchemaFailureAndWarningMode_DoesNotFailValidation()
    {
        // Arrange
        var json = CreateOpenApi30OptionalEndpointSpec();
        _jsonValidatorServiceMock
            .Setup(service => service.ValidateAsync(It.IsAny<ValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                Errors =
                [
                    new()
                    {
                        Path = "data",
                        Message = "Validation failed for optional endpoint payload",
                        ErrorCode = "SCHEMA_MISMATCH",
                        Severity = "Error"
                    }
                ],
                SchemaVersion = "test",
                Duration = TimeSpan.Zero
            });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions
            {
                //ValidateSpecification = false
            }
        };

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        });

        // Rebuild service with a mock HsdsComplianceService so spec validation does not add unexpected HSDS errors
        var hsdsComplianceMock = new Mock<IHsdsComplianceService>();
        hsdsComplianceMock.Setup(s => s.ExtractClaimedProfileVersion(It.IsAny<string>(), It.IsAny<string>())).Returns((string?)"HSDS-30");
        string? unused;
        hsdsComplianceMock.Setup(s => s.TryGetKnownHsdsSchemaUrl(It.IsAny<string>(), out unused)).Returns(true);
        hsdsComplianceMock.Setup(s => s.CompareFeedSpecAgainstHsdsProfile(It.IsAny<JsonNode>(), It.IsAny<JsonNode>())).Returns([]);
        _service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
             _openApiSpecificationService,
             hsdsComplianceMock.Object,
             null!,
             null!,
            _openApiBootstrapServiceMock.Object,
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                HsdsValidationMode = _openApiValidationServerOptions.Value.HsdsValidationMode,
                AllowUserSuppliedAuth = _openApiValidationServerOptions.Value.AllowUserSuppliedAuth,
                ValidateSpecification = false,
                TestEndpoints = true
            }));

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.EndpointTests, Has.Count.EqualTo(1));
            Assert.That(result.EndpointTests[0].IsOptional, Is.True);
            Assert.That(result.EndpointTests[0].Status, Is.EqualTo(EndpointTestStatus.PassedWithWarnings));
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary!.FailedTests, Is.Zero);
            Assert.That(result.IsValid, Is.True);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithOptionalEndpointAndTestingDisabled_SkipsEndpoint()
    {
        // Arrange
        var json = CreateOpenApi30OptionalEndpointSpec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        var mockHandler = new MockHttpMessageHandler((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });
        _httpClient?.Dispose();
        _httpClient = TestHttpClientFactory.CreateClient(mockHandler);

        // TestOptionalEndpoints is now server-configurable; create a service with it disabled
        var serviceWithOptionalEndpointsDisabled = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
             _openApiSpecificationService,
             null!,
             null!,
             null!,
            _openApiBootstrapServiceMock.Object,
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions
            {
                HsdsValidationMode = _openApiValidationServerOptions.Value.HsdsValidationMode,
                AllowUserSuppliedAuth = _openApiValidationServerOptions.Value.AllowUserSuppliedAuth,
                ValidateSpecification = _openApiValidationServerOptions.Value.ValidateSpecification,
                TestEndpoints = true,
                TestOptionalEndpoints = false,
                TreatOptionalEndpointsAsWarnings = _openApiValidationServerOptions.Value.TreatOptionalEndpointsAsWarnings
            }));

        // Act
        var result = await serviceWithOptionalEndpointsDisabled.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.EndpointTests, Has.Count.EqualTo(1));
            Assert.That(result.EndpointTests[0].Status, Is.EqualTo(EndpointTestStatus.Skipped));
            Assert.That(result.EndpointTests[0].TestResults, Is.Empty);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithMixedRequiredAndOptionalEndpoints_TestedEndpointsNeverRemainNotTested()
    {
        // Arrange
        var json = CreateOpenApi30MixedRequiredAndOptionalSpec();
        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            Options = new OpenApiValidationOptions()
        };

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                };
            }

            if (requestUri.Contains("/optional", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        });

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.EndpointTests, Has.Count.EqualTo(2));
            Assert.That(result.EndpointTests.All(e => e.IsTested), Is.True);
            Assert.That(result.EndpointTests.All(e => e.Status != EndpointTestStatus.NotTested), Is.True);
            Assert.That(result.EndpointTests.Any(e => e.Status == EndpointTestStatus.PassedValidation), Is.True);
            Assert.That(result.EndpointTests.Any(e => e.Status == EndpointTestStatus.PassedWithWarnings), Is.True);
        }
    }

    #endregion

    #region Authentication Tests

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithApiKeyAuth_AddsCorrectHeader()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        HttpRequestMessage? capturedRequest = null;

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (!requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                capturedRequest = req;
            }

            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            DataSourceAuth = new DataSourceAuthentication
            {
                ApiKey = "test-api-key-12345"
            },
            Options = new OpenApiValidationOptions()
        };

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedRequest, Is.Not.Null, "Expected endpoint request to be captured");
            Assert.That(capturedRequest!.Headers.Contains("X-API-Key"), Is.True, "Expected X-API-Key header to be present");
            Assert.That(capturedRequest.Headers.GetValues("X-API-Key").First(), Is.EqualTo("test-api-key-12345"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithCustomApiKeyHeader_AddsCorrectHeader()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        HttpRequestMessage? capturedRequest = null;

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (!requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                capturedRequest = req;
            }

            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            DataSourceAuth = new DataSourceAuthentication
            {
                ApiKey = "custom-key-value",
                ApiKeyHeader = "X-Custom-Auth-Key"
            },
            Options = new OpenApiValidationOptions()
        };

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.Headers.Contains("X-Custom-Auth-Key"), Is.True);
            Assert.That(capturedRequest.Headers.GetValues("X-Custom-Auth-Key").First(), Is.EqualTo("custom-key-value"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithBearerToken_AddsAuthorizationHeader()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        HttpRequestMessage? capturedRequest = null;

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (!requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                capturedRequest = req;
            }

            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            DataSourceAuth = new DataSourceAuthentication
            {
                BearerToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test"
            },
            Options = new OpenApiValidationOptions()
        };

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.Headers.Authorization, Is.Not.Null);
            Assert.That(capturedRequest.Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
            Assert.That(capturedRequest.Headers.Authorization.Parameter, Is.EqualTo("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithBasicAuth_AddsAuthorizationHeader()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        HttpRequestMessage? capturedRequest = null;

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (!requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                capturedRequest = req;
            }

            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            DataSourceAuth = new DataSourceAuthentication
            {
                BasicAuth = new BasicAuthentication
                {
                    Username = "testuser",
                    Password = "testpass123"
                }
            },
            Options = new OpenApiValidationOptions()
        };

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.Headers.Authorization, Is.Not.Null);
            Assert.That(capturedRequest.Headers.Authorization!.Scheme, Is.EqualTo("Basic"));

            // Decode and verify credentials
            var credentials = System.Text.Encoding.ASCII.GetString(
                Convert.FromBase64String(capturedRequest.Headers.Authorization.Parameter!));
            Assert.That(credentials, Is.EqualTo("testuser:testpass123"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithCustomHeaders_AddsAllHeaders()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        HttpRequestMessage? capturedRequest = null;

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (!requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                capturedRequest = req;
            }

            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            DataSourceAuth = new DataSourceAuthentication
            {
                CustomHeaders = new Dictionary<string, string>
                {
                    { "X-Client-Id", "client-123" },
                    { "X-Request-Id", "req-456" },
                    { "X-Tenant-Id", "tenant-789" }
                }
            },
            Options = new OpenApiValidationOptions()
        };

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.Headers.Contains("X-Client-Id"), Is.True);
            Assert.That(capturedRequest.Headers.GetValues("X-Client-Id").First(), Is.EqualTo("client-123"));
            Assert.That(capturedRequest.Headers.Contains("X-Request-Id"), Is.True);
            Assert.That(capturedRequest.Headers.GetValues("X-Request-Id").First(), Is.EqualTo("req-456"));
            Assert.That(capturedRequest.Headers.Contains("X-Tenant-Id"), Is.True);
            Assert.That(capturedRequest.Headers.GetValues("X-Tenant-Id").First(), Is.EqualTo("tenant-789"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithMultipleAuthMethods_AddsAllHeaders()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        HttpRequestMessage? capturedRequest = null;

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (!requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                capturedRequest = req;
            }

            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            DataSourceAuth = new DataSourceAuthentication
            {
                CustomHeaders = new Dictionary<string, string>
                {
                    { "X-Client-Id", "multi-auth-client" },
                    { "X-Request-Id", "req-12345" }
                }
            },
            Options = new OpenApiValidationOptions()
        };

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.Headers.Contains("X-Client-Id"), Is.True);
            Assert.That(capturedRequest.Headers.GetValues("X-Client-Id").First(), Is.EqualTo("multi-auth-client"));
            Assert.That(capturedRequest.Headers.Contains("X-Request-Id"), Is.True);
            Assert.That(capturedRequest.Headers.GetValues("X-Request-Id").First(), Is.EqualTo("req-12345"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithoutDataSourceAuth_DoesNotAddHeaders()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        HttpRequestMessage? capturedRequest = null;

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (!requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                capturedRequest = req;
            }

            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            DataSourceAuth = null,  // No authentication provided
            Options = new OpenApiValidationOptions()
        };

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.Headers.Contains("X-API-Key"), Is.False);
            Assert.That(capturedRequest.Headers.Authorization, Is.Null);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithEmptyAuthData_DoesNotAddHeaders()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        HttpRequestMessage? capturedRequest = null;

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (!requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                capturedRequest = req;
            }

            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            DataSourceAuth = new DataSourceAuthentication(),  // Empty auth data
            Options = new OpenApiValidationOptions()
        };

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.Headers.Authorization, Is.Null);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithBasicAuthEmptyPassword_RejectsAuth()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        HttpRequestMessage? capturedRequest = null;

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (!requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                capturedRequest = req;
            }

            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            DataSourceAuth = new DataSourceAuthentication
            {
                BasicAuth = new BasicAuthentication
                {
                    Username = "testuser",
                    Password = string.Empty  // Empty password - should be rejected
                }
            },
            Options = new OpenApiValidationOptions()
        };

        // Act
        var result = await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        // Empty passwords are not allowed, so Authorization header should not be applied
        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.Headers.Authorization, Is.Null);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithHttpBaseUrl_DoesNotApplyDataSourceAuthHeaders()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        HttpRequestMessage? capturedDataSourceRequest = null;

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (!requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                capturedDataSourceRequest = req;
            }

            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "http://api.example.com",
            DataSourceAuth = new DataSourceAuthentication
            {
                ApiKey = "do-not-send-over-http"
            },
            Options = new OpenApiValidationOptions()
        };

        // Act
        await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedDataSourceRequest, Is.Not.Null);
            Assert.That(capturedDataSourceRequest!.RequestUri, Is.Not.Null);
            Assert.That(capturedDataSourceRequest.RequestUri!.Scheme, Is.EqualTo(Uri.UriSchemeHttp));
            Assert.That(capturedDataSourceRequest.Headers.Contains("X-API-Key"), Is.False);
            Assert.That(capturedDataSourceRequest.Headers.Authorization, Is.Null);
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenUserSuppliedAuthEnabled_AppliesAuthToSchemaAndDatasourceRequests()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        HttpRequestMessage? capturedSchemaRequest = null;
        HttpRequestMessage? capturedDataSourceRequest = null;

        SetupHttpMock((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                capturedSchemaRequest = req;
            }
            else
            {
                capturedDataSourceRequest = req;
            }

            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            DataSourceAuth = new DataSourceAuthentication
            {
                ApiKey = "data-source-api-key"
            },
            Options = new OpenApiValidationOptions()
        };

        // Act
        await _service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedSchemaRequest, Is.Not.Null);
            Assert.That(capturedSchemaRequest!.RequestUri, Is.Not.Null);
            Assert.That(capturedSchemaRequest.RequestUri!.AbsoluteUri, Does.Contain("openapi"));

            Assert.That(capturedDataSourceRequest, Is.Not.Null);
            Assert.That(capturedDataSourceRequest!.Headers.Contains("X-API-Key"), Is.True);
            Assert.That(capturedDataSourceRequest.Headers.GetValues("X-API-Key").First(), Is.EqualTo("data-source-api-key"));
        }
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WhenUserSuppliedAuthDisabled_DoesNotApplyAuthToSchemaOrDatasourceRequests()
    {
        // Arrange
        var json = CreateOpenApi30Spec();
        HttpRequestMessage? capturedSchemaRequest = null;
        HttpRequestMessage? capturedDataSourceRequest = null;

        var mockHandler = new MockHttpMessageHandler((req, ct) =>
        {
            var requestUri = req.RequestUri?.ToString() ?? string.Empty;
            if (requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase))
            {
                capturedSchemaRequest = req;
            }
            else
            {
                capturedDataSourceRequest = req;
            }

            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? json
                : "{}";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        _httpClient?.Dispose();
        _httpClient = TestHttpClientFactory.CreateClient(mockHandler);
        var service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
             _openApiSpecificationService,
             null!,
             null!,
             null!,
            _openApiBootstrapServiceMock.Object,
            openApiValidationServerOptions: Options.Create(new OpenApiValidationServerOptions { AllowUserSuppliedAuth = false, TestEndpoints = true }));

        var request = new OpenApiValidationRequest
        {
            OwnSchemaUrl = "https://example.com/openapi.json",
            BaseUrl = "https://api.example.com",
            DataSourceAuth = new DataSourceAuthentication
            {
                ApiKey = "data-source-api-key"
            },
            Options = new OpenApiValidationOptions()
        };

        // Act
        await service.ValidateOpenApiSpecificationAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedSchemaRequest, Is.Not.Null);
            Assert.That(capturedSchemaRequest!.Headers.Authorization, Is.Null);
            Assert.That(capturedSchemaRequest.Headers.Contains("X-API-Key"), Is.False);

            Assert.That(capturedDataSourceRequest, Is.Not.Null);
            Assert.That(capturedDataSourceRequest!.Headers.Authorization, Is.Null);
            Assert.That(capturedDataSourceRequest.Headers.Contains("X-API-Key"), Is.False);
        }
    }

    #endregion

    #region Helper Methods

    private static bool HasInformationLogContaining<T>(Mock<ILogger<T>> loggerMock, string expectedText)
    {
        return loggerMock.Invocations.Any(invocation =>
            invocation.Method.Name == "Log"
            && invocation.Arguments.Count >= 3
            && invocation.Arguments[0] is LogLevel logLevel
            && logLevel == LogLevel.Information
            && invocation.Arguments[2]?.ToString()?.Contains(expectedText, StringComparison.Ordinal) == true);
    }

    private void SetupHttpMock(string responseJson, string endpointResponseBody = "{}")
    {
        var mockHandler = new MockHttpMessageHandler((request, ct) =>
        {
            var requestUri = request.RequestUri?.ToString() ?? string.Empty;
            var responseBody = requestUri.Contains("openapi", StringComparison.OrdinalIgnoreCase)
                ? responseJson
                : endpointResponseBody;

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        _httpClient?.Dispose();
        _httpClient = TestHttpClientFactory.CreateClient(mockHandler);

        _service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
             _openApiSpecificationService,
             null!,
             null!,
             null!,
            _openApiBootstrapServiceMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-1.0"] = "https://openreferraluk.org/specifications/1.0/openapi.json",
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: _openApiValidationServerOptions);
    }

    private void SetupHttpMock(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
    {
        var mockHandler = new MockHttpMessageHandler(handler);
        _httpClient?.Dispose();
        _httpClient = TestHttpClientFactory.CreateClient(mockHandler);

        _service = new OpenApiValidationService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _jsonValidatorServiceMock.Object,
            _schemaResolverServiceMock.Object,
                _openApiSpecificationService,
                null!,
                null!,
                null!,
            _openApiBootstrapServiceMock.Object,
            specificationOptions: Options.Create(new SpecificationOptions
            {
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-1.0"] = "https://openreferraluk.org/specifications/1.0/openapi.json",
                    ["HSDS-UK-3.0"] = "https://openreferraluk.org/specifications/3.0/openapi.json"
                }
            }),
            openApiValidationServerOptions: _openApiValidationServerOptions);
    }

    private static IHttpClientFactory CreateFactory(HttpClient httpClient)
    {
        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        return mock.Object;
    }

    private static string CreateOpenApi30Spec()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Test API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/test"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateOpenApi30SpecWithResponseSchema()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Test API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/test"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": {
                                ""description"": ""OK"",
                                ""content"": {
                                    ""application/json"": {
                                        ""schema"": {
                                            ""type"": ""array"",
                                            ""items"": {
                                                ""type"": ""object"",
                                                ""properties"": {
                                                    ""name"": { ""type"": ""string"" }
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
        }";
    }

    private static string CreateOpenApi30ParameterizedOnlySpecWithIndexedPath()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Test API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/items[0]/{id}"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateOpenApi30PaginatedSpec()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Test API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/items"": {
                    ""get"": {
                        ""parameters"": [
                            {
                                ""name"": ""page"",
                                ""in"": ""query"",
                                ""schema"": { ""type"": ""integer"" }
                            }
                        ],
                        ""responses"": {
                            ""200"": {
                                ""description"": ""OK"",
                                ""content"": {
                                    ""application/json"": {
                                        ""schema"": {
                                            ""type"": ""object"",
                                            ""properties"": {
                                                ""total_pages"": { ""type"": ""integer"" },
                                                ""data"": {
                                                    ""type"": ""array"",
                                                    ""items"": { ""type"": ""object"" }
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
        }";
    }

    private static string CreateOpenApi30OptionalEndpointSpec()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Test API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/optional"": {
                    ""get"": {
                        ""tags"": [""Optional""],
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateOpenApi30MixedRequiredAndOptionalSpec()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Test API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/required"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                },
                ""/optional"": {
                    ""get"": {
                        ""tags"": [""Optional""],
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateOpenApi30SpecWithPrefixedPaths()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Prefixed Paths API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/api/v1/health"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                },
                ""/api/v1/services"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateOpenApi30SpecWithPrefixedCollisionPaths()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Collision Paths API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/health"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                },
                ""/api/v1/health"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateSwagger20Spec()
    {
        return @"{
            ""swagger"": ""2.0"",
            ""info"": {
                ""title"": ""Test API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/test"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateHsdsProfileSpec()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""HSDS Profile"",
                ""version"": ""3.0""
            },
            ""paths"": {
                ""/organisations"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": {
                                ""description"": ""OK"",
                                ""content"": {
                                    ""application/json"": {
                                        ""schema"": {
                                            ""type"": ""array"",
                                            ""items"": {
                                                ""type"": ""object"",
                                                ""required"": [""id"", ""name""],
                                                ""properties"": {
                                                    ""id"": { ""type"": ""string"" },
                                                    ""name"": { ""type"": ""string"" }
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
        }";
    }

    private static string CreateFeedSpecMissingRequiredHsdsEndpoint()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Feed API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/services"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateFeedSpecWithAdditionalEndpoint()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Feed API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/organisations"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": {
                                ""description"": ""OK"",
                                ""content"": {
                                    ""application/json"": {
                                        ""schema"": {
                                            ""type"": ""array"",
                                            ""items"": {
                                                ""type"": ""object"",
                                                ""required"": [""id"", ""name""],
                                                ""properties"": {
                                                    ""id"": { ""type"": ""string"" },
                                                    ""name"": { ""type"": ""string"" }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                ""/custom"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateOpenApi30SpecWithHsdsVersionExtension(string hsdsVersion)
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""x-hsds-version"": """ + hsdsVersion + @""",
            ""info"": { ""title"": ""Test API"", ""version"": ""1.0.0"" },
            ""paths"": {
                ""/organisations"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateFeedSpecPermissiveOrganisationResponse()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Feed API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/organisations"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": {
                                ""description"": ""OK"",
                                ""content"": {
                                    ""application/json"": {
                                        ""schema"": {
                                            ""type"": ""array"",
                                            ""items"": {
                                                ""type"": ""object"",
                                                ""properties"": {
                                                    ""id"": { ""type"": ""string"" },
                                                    ""name"": { ""type"": ""string"" }
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
        }";
    }

    private static string CreateHsdsProfileSpecWithRequestBody()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""HSDS Profile"",
                ""version"": ""3.0""
            },
            ""paths"": {
                ""/organisations"": {
                    ""post"": {
                        ""requestBody"": {
                            ""required"": true,
                            ""content"": {
                                ""application/json"": {
                                    ""schema"": {
                                        ""type"": ""object"",
                                        ""required"": [""name""],
                                        ""properties"": {
                                            ""name"": { ""type"": ""string"" },
                                            ""description"": { ""type"": ""string"" }
                                        }
                                    }
                                }
                            }
                        },
                        ""responses"": {
                            ""201"": { ""description"": ""Created"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateFeedSpecMissingRequiredHsdsRequestField()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Feed API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/organisations"": {
                    ""post"": {
                        ""requestBody"": {
                            ""required"": true,
                            ""content"": {
                                ""application/json"": {
                                    ""schema"": {
                                        ""type"": ""object"",
                                        ""properties"": {
                                            ""description"": { ""type"": ""string"" }
                                        }
                                    }
                                }
                            }
                        },
                        ""responses"": {
                            ""201"": { ""description"": ""Created"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateFeedSpecWithAdditionalHsdsRequestField()
    {
        return @"{
            ""openapi"": ""3.0.0"",
            ""info"": {
                ""title"": ""Feed API"",
                ""version"": ""1.0.0""
            },
            ""paths"": {
                ""/organisations"": {
                    ""post"": {
                        ""requestBody"": {
                            ""required"": true,
                            ""content"": {
                                ""application/json"": {
                                    ""schema"": {
                                        ""type"": ""object"",
                                        ""required"": [""name""],
                                        ""properties"": {
                                            ""name"": { ""type"": ""string"" },
                                            ""description"": { ""type"": ""string"" },
                                            ""customAttribute"": { ""type"": ""string"" }
                                        }
                                    }
                                }
                            }
                        },
                        ""responses"": {
                            ""201"": { ""description"": ""Created"" }
                        }
                    }
                }
            }
        }";
    }

    private static string CreateOpenApiSpecWithMisusedOpenApiField(string openApiValue)
    {
        return @"{
            ""openapi"": """ + openApiValue + @""",
            ""info"": { ""title"": ""Test API"", ""version"": ""1.0.0"" },
            ""paths"": {
                ""/organisations"": {
                    ""get"": {
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
    }

    private class MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler = handler;

        public MockHttpMessageHandler()
            : this((req, ct) => new HttpResponseMessage(System.Net.HttpStatusCode.OK))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                var response = _handler(request, cancellationToken);
                return Task.FromResult(response);
            }
            catch (HttpRequestException ex)
            {
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }

    #endregion
}
