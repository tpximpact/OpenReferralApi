using System.Text.Json;
using System.Text.Json.Nodes;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class OpenReferralUKValidationResponseMapperTests
{
    private OpenReferralUKValidationResponseMapper _mapper;

    [SetUp]
    public void Setup()
    {
        _mapper = new OpenReferralUKValidationResponseMapper();
    }

    [Test]
    public void MapToOpenReferralUKValidationResponse_WithValidResult_ReturnsCorrectStructure()
    {
        // Arrange
        var result = new OpenApiValidationResult
        {
            IsValid = true,
            Metadata = new CommonValidationMetadata
            {
                BaseUrl = "https://api.example.com"
            },
            SpecificationValidation = new OpenApiSpecificationValidation
            {
                IsValid = true,
                Version = "3.0",
                Errors = []
            },
            Summary = new OpenApiValidationSummary
            {
                TotalEndpoints = 5,
                SuccessfulTests = 5,
                FailedTests = 0
            },
            EndpointTests = []
        };

        // Act
        var response = _mapper.MapToOpenReferralUKValidationResponse(result);

        // Assert
        Assert.That(response, Is.Not.Null);
        var json = JsonSerializer.SerializeToNode(response)!.AsObject();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(json["service"], Is.Not.Null);
            Assert.That(json["testSuites"], Is.Not.Null);
            Assert.That(json["specificationValidation"], Is.Not.Null);
        }
    }

    [Test]
    public void MapToValidationResponse_WithEmptyEndpointTests_ReturnsEmptyTestSuites()
    {
        // Arrange
        var result = new OpenApiValidationResult
        {
            IsValid = true,
            Metadata = new CommonValidationMetadata
            {
                BaseUrl = "https://api.example.com"
            },
            SpecificationValidation = new OpenApiSpecificationValidation
            {
                IsValid = true,
                Version = "3.0"
            },
            Summary = new OpenApiValidationSummary
            {
                TotalEndpoints = 0,
                SuccessfulTests = 0,
                FailedTests = 0
            },
            EndpointTests = []
        };

        // Act
        var response = _mapper.MapToOpenReferralUKValidationResponse(result);

        // Assert
        var json = JsonSerializer.SerializeToNode(response)!.AsObject();
        var testSuites = json["testSuites"] as JsonArray;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(testSuites, Is.Not.Null);
            Assert.That(testSuites, Is.Empty);
        }
    }

    [Test]
    public void MapToValidationResponse_WithNullMetadata_HandlesGracefully()
    {
        // Arrange
        var result = new OpenApiValidationResult
        {
            IsValid = true,
            Metadata = null,
            SpecificationValidation = new OpenApiSpecificationValidation
            {
                IsValid = true
            },
            Summary = new OpenApiValidationSummary
            {
                FailedTests = 0
            },
            EndpointTests = []
        };

        // Act
        var response = _mapper.MapToOpenReferralUKValidationResponse(result);

        // Assert
        Assert.That(response, Is.Not.Null);
        var json = JsonSerializer.SerializeToNode(response)!.AsObject();
        Assert.That(json["service"]!["url"]!.ToString(), Is.EqualTo(""));
    }

    [Test]
    public void MapToValidationResponse_WithNotifications_MapsNotifications()
    {
        // Arrange
        var result = new OpenApiValidationResult
        {
            IsValid = false,
            Notifications =
            [
                "Unable to get or resolve the OpenAPI specification from https://example.com/openapi.json. 404"
            ]
        };

        // Act
        var response = _mapper.MapToOpenReferralUKValidationResponse(result);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Notifications, Has.Count.EqualTo(1));
            Assert.That(response.Notifications[0], Does.Contain("Unable to get or resolve the OpenAPI specification"));
        }
    }

    [Test]
    public void MapToOpenReferralUKValidationResponse_UsesMetadataProfileWhenAvailable()
    {
        // Arrange
        var result = new OpenApiValidationResult
        {
            Metadata = new CommonValidationMetadata
            {
                BaseUrl = "https://api.example.com",
                Profile = "3.0",
                ProfileReason = "Standard version [user: 3.0] read from '/' endpoint"
            },
            SpecificationValidation = new OpenApiSpecificationValidation
            {
                Version = "1.0.0"
            },
            EndpointTests = []
        };

        // Act
        var response = _mapper.MapToOpenReferralUKValidationResponse(result);

        // Assert
        Assert.That(response.Service.Profile, Is.EqualTo("3.0"));
    }

    [Test]
    public void MapToOpenReferralUKValidationResponse_MapsSpecificationValidationErrors()
    {
        // Arrange
        var result = new OpenApiValidationResult
        {
            Metadata = new CommonValidationMetadata
            {
                BaseUrl = "https://api.example.com"
            },
            SpecificationValidation = new OpenApiSpecificationValidation
            {
                IsValid = false,
                Version = "3.0.0",
                Errors =
                [
                    new()
                    {
                        ErrorCode = "HSDS_MISSING_ENDPOINT",
                        Severity = "Error",
                        Message = "Missing required HSDS endpoint: GET /organizations",
                        Path = "paths.GET /organizations"
                    }
                ]
            },
            EndpointTests = []
        };

        // Act
        var response = _mapper.MapToOpenReferralUKValidationResponse(result);

        // Assert
        var json = JsonSerializer.SerializeToNode(response)!.AsObject();
        var specValidation = json["specificationValidation"];
        var errors = specValidation?["errors"] as JsonArray;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(specValidation, Is.Not.Null);
            Assert.That(specValidation!["isValid"]!.GetValue<bool>(), Is.False);
            Assert.That(specValidation["version"]!.GetValue<string>(), Is.EqualTo("3.0.0"));
            Assert.That(errors, Is.Not.Null);
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors![0]!["name"]!.GetValue<string>(), Is.EqualTo("HSDS_MISSING_ENDPOINT"));
            Assert.That(errors[0]!["errorIn"]!.GetValue<string>(), Is.EqualTo("paths.GET /organizations"));
        }
    }

    [Test]
    public void MapToOpenReferralUKValidationResponse_WithRequiredAndOptionalEndpoints_MapsTwoComplianceSuites()
    {
        // Arrange
        var result = new OpenApiValidationResult
        {
            Metadata = new CommonValidationMetadata
            {
                BaseUrl = "https://api.example.com"
            },
            EndpointTests =
            [
                new()
                {
                    Name = "Get Organizations",
                    Method = "GET",
                    Path = "/organizations",
                    IsOptional = false,
                    Status = EndpointTestStatus.PassedValidation,
                    TestResults = []
                },
                new()
                {
                    Name = "Get Services",
                    Method = "GET",
                    Path = "/services",
                    IsOptional = true,
                    Status = EndpointTestStatus.PassedWithWarnings,
                    TestResults = []
                }
            ]
        };

        // Act
        var response = _mapper.MapToOpenReferralUKValidationResponse(result);

        // Assert
        var json = JsonSerializer.SerializeToNode(response)!.AsObject();
        var suites = json["testSuites"] as JsonArray;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(suites, Is.Not.Null);
            Assert.That(suites, Has.Count.EqualTo(2));
            Assert.That(suites![0]!["name"]!.GetValue<string>(), Is.EqualTo("Level 1 Compliance - Basic checks"));
            Assert.That(suites[0]!["required"]!.GetValue<bool>(), Is.True);
            Assert.That(suites[0]!["messageLevel"]!.GetValue<string>(), Is.EqualTo("error"));
            Assert.That(suites[1]!["name"]!.GetValue<string>(), Is.EqualTo("Level 2 Compliance - Extended checks"));
            Assert.That(suites[1]!["required"]!.GetValue<bool>(), Is.False);
            Assert.That(suites[1]!["messageLevel"]!.GetValue<string>(), Is.EqualTo("warning"));
        }
    }

    [Test]
    public void MapToOpenReferralUKValidationResponse_WithFailedValidationEndpoint_SetsServiceInvalid()
    {
        // Arrange
        var result = new OpenApiValidationResult
        {
            Metadata = new CommonValidationMetadata
            {
                BaseUrl = "https://api.example.com"
            },
            EndpointTests =
            [
                new()
                {
                    Method = "GET",
                    Path = "/organizations",
                    IsOptional = false,
                    Status = EndpointTestStatus.FailedValidation,
                    TestResults = []
                }
            ]
        };

        // Act
        var response = _mapper.MapToOpenReferralUKValidationResponse(result);

        // Assert
        Assert.That(response.Service.IsValid, Is.False);
    }

    [Test]
    public void MapToOpenReferralUKValidationResponse_UsesSpecificationVersionWhenMetadataProfileMissing()
    {
        // Arrange
        var result = new OpenApiValidationResult
        {
            Metadata = new CommonValidationMetadata
            {
                BaseUrl = "https://api.example.com",
                Profile = null,
                ProfileReason = null
            },
            SpecificationValidation = new OpenApiSpecificationValidation
            {
                Version = "3.0.1"
            },
            EndpointTests = []
        };

        // Act
        var response = _mapper.MapToOpenReferralUKValidationResponse(result);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Service.Profile, Is.EqualTo("3.0.1"));
            Assert.That(response.Service.ProfileReason, Is.EqualTo("Unknown"));
        }
    }

    [Test]
    public void MapToOpenReferralUKValidationResponse_DeduplicatesMessagesByErrorPathAcrossTestResults()
    {
        // Arrange
        var duplicatePath = "organization.name";
        var endpoint = new EndpointTestResult
        {
            Name = "Get Organizations",
            Method = "GET",
            Path = "/organizations",
            IsOptional = false,
            Status = EndpointTestStatus.FailedValidation,
            TestResults =
            [
                new()
                {
                    ValidationResult = new ValidationResult
                    {
                        IsValid = false,
                        Errors =
                        [
                            new()
                            {
                                ErrorCode = "ERR_ONE",
                                Severity = "Error",
                                Message = "First error",
                                Path = duplicatePath
                            }
                        ]
                    }
                },
                new()
                {
                    ValidationResult = new ValidationResult
                    {
                        IsValid = false,
                        Errors =
                        [
                            new()
                            {
                                ErrorCode = "ERR_TWO",
                                Severity = "Error",
                                Message = "Second error same path",
                                Path = duplicatePath
                            }
                        ]
                    }
                }
            ]
        };

        var result = new OpenApiValidationResult
        {
            Metadata = new CommonValidationMetadata
            {
                BaseUrl = "https://api.example.com"
            },
            EndpointTests = [endpoint]
        };

        // Act
        var response = _mapper.MapToOpenReferralUKValidationResponse(result);

        // Assert
        var json = JsonSerializer.SerializeToNode(response)!.AsObject();
        var messages = json["testSuites"]![0]!["tests"]![0]!["messages"] as JsonArray;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(messages, Is.Not.Null);
            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages![0]!["errorIn"]!.GetValue<string>(), Is.EqualTo(duplicatePath));
        }
    }

    [Test]
    public void MapToOpenReferralUKValidationResponse_AddsPerformanceWarningForSlowPassedEndpoint()
    {
        // Arrange
        var endpoint = new EndpointTestResult
        {
            Name = "Get Locations",
            Method = "GET",
            Path = "/locations",
            IsOptional = false,
            Status = EndpointTestStatus.PassedValidation,
            TestResults =
            [
                new() { ResponseTime = TimeSpan.FromMilliseconds(6000) },
                new() { ResponseTime = TimeSpan.FromMilliseconds(7000) }
            ]
        };

        var result = new OpenApiValidationResult
        {
            Metadata = new CommonValidationMetadata
            {
                BaseUrl = "https://api.example.com"
            },
            EndpointTests = [endpoint]
        };

        // Act
        var response = _mapper.MapToOpenReferralUKValidationResponse(result);

        // Assert
        var json = JsonSerializer.SerializeToNode(response)!.AsObject();
        var messages = json["testSuites"]![0]!["tests"]![0]!["messages"] as JsonArray;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(messages, Is.Not.Null);
            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages![0]!["name"]!.GetValue<string>(), Is.EqualTo("Performance"));
            Assert.That(messages[0]!["description"]!.GetValue<string>(), Is.EqualTo("Warning"));
            Assert.That(messages[0]!["message"]!.GetValue<string>(), Does.Contain("Average response time is 6500ms"));
        }
    }

    [Test]
    public void MapToOpenReferralUKValidationResponse_WhenRunRepeatedly_DoesNotLeakMemory()
    {
        // Arrange: Create a large, complex validation payload to stress the mapper
        var result = new OpenApiValidationResult
        {
            Metadata = new CommonValidationMetadata { BaseUrl = "https://api.example.com" },
            SpecificationValidation = new OpenApiSpecificationValidation
            {
                IsValid = false,
                Version = "3.0",
                Errors = []
            },
            EndpointTests = []
        };

        for (int i = 0; i < 50; i++)
        {
            result.SpecificationValidation.Errors.Add(new ValidationError
            {
                ErrorCode = "SPEC_ERR",
                Severity = "Error",
                Message = "A specification error occurred",
                Path = $"paths.test{i}"
            });
        }

        for (int i = 0; i < 20; i++)
        {
            var testResult = new HttpTestResult { ValidationResult = new ValidationResult { IsValid = false, Errors = [] } };
            
            for (int j = 0; j < 10; j++)
            {
                testResult.ValidationResult.Errors.Add(new ValidationError
                {
                    ErrorCode = "VAL_ERR",
                    Severity = "Error",
                    Message = "A validation error occurred",
                    Path = $"data.items[{j}].field"
                });
            }

            result.EndpointTests.Add(new EndpointTestResult
            {
                Name = $"Endpoint {i}",
                Method = "GET",
                Path = $"/endpoint{i}",
                IsOptional = i % 2 == 0,
                Status = EndpointTestStatus.FailedValidation,
                TestResults = [testResult]
            });
        }

        // Warmup: Ensure JIT compilation, static allocations, and capacity expansions are complete
        _ = _mapper.MapToOpenReferralUKValidationResponse(result);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(true);

        // Act: Run mapping extensively (mapping 250,000 total errors)
        for (int i = 0; i < 1000; i++)
        {
            _ = _mapper.MapToOpenReferralUKValidationResponse(result);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryAfter = GC.GetTotalMemory(true);
        var memoryGrowth = memoryAfter - memoryBefore;

        // Assert: Allow for .NET GC segment fragmentation and ArrayPool retention, but fail if unbounded leaks occur
        Assert.That(memoryGrowth, Is.LessThan(5000 * 1024), $"Memory grew by {memoryGrowth} bytes, indicating a potential leak or degraded pooling optimizations.");
    }
}
