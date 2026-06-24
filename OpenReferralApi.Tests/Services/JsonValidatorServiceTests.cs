using Microsoft.Extensions.Logging;
using Moq;
using Json.Schema;
using OpenReferralApi.Core.Services;
using System.Text.Json.Nodes;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class JsonValidatorServiceTests
{
    private Mock<ILogger<JsonValidatorService>> _loggerMock;
    private Mock<IPathParsingService> _pathParsingServiceMock;
    private Mock<IRequestProcessingService> _requestProcessingServiceMock;
    private Mock<ISchemaResolverService> _schemaResolverServiceMock;
    private HttpClient _httpClient;
    private JsonValidatorService _service;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<JsonValidatorService>>();
        _pathParsingServiceMock = new Mock<IPathParsingService>();
        _requestProcessingServiceMock = new Mock<IRequestProcessingService>();
        _schemaResolverServiceMock = new Mock<ISchemaResolverService>();

        _requestProcessingServiceMock
            .Setup(service => service.ExecuteWithConcurrencyControlAsync(
                It.IsAny<Func<CancellationToken, Task<ValidationResult>>>(),
                It.IsAny<ValidationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<ValidationResult>> func, ValidationOptions? options, CancellationToken ct) => func(ct));

        _requestProcessingServiceMock
            .Setup(service => service.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task<JsonSchema>>>(),
                It.IsAny<ValidationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<JsonSchema>> func, ValidationOptions? options, CancellationToken ct) => func(ct));

        _requestProcessingServiceMock
            .Setup(service => service.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task<object>>>(),
                It.IsAny<ValidationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<object>> func, ValidationOptions? options, CancellationToken ct) => func(ct));

        _requestProcessingServiceMock
            .Setup(service => service.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task<System.Text.Json.JsonDocument>>>(),
                It.IsAny<ValidationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<System.Text.Json.JsonDocument>> func, ValidationOptions? options, CancellationToken ct) => func(ct));

        _requestProcessingServiceMock
            .Setup(service => service.CreateTimeoutToken(It.IsAny<ValidationOptions?>(), It.IsAny<CancellationToken>()))
            .Returns((ValidationOptions? options, CancellationToken ct) => CancellationTokenSource.CreateLinkedTokenSource(ct));

        _schemaResolverServiceMock
            .Setup(service => service.CreateSchemaFromJsonAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DataSourceAuthentication?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string schemaJson, string? documentUri, DataSourceAuthentication? auth, CancellationToken ct) => JsonSchema.FromText(schemaJson));

        _schemaResolverServiceMock
            .Setup(service => service.CreateSchemaFromJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string schemaJson, CancellationToken ct) => JsonSchema.FromText(schemaJson));

        var mockHandler = new MockHttpMessageHandler("{}", "{}");
        _httpClient = TestHttpClientFactory.CreateClient(mockHandler);

        _service = new JsonValidatorService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _pathParsingServiceMock.Object,
            _requestProcessingServiceMock.Object,
            _schemaResolverServiceMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    public async Task ValidateAsync_WithDirectJsonAndSchema_ReturnsValidResult()
    {
        // Arrange
        var schema = new
        {
            title = "Person",
            description = "A person record",
            type = "object",
            properties = new { name = new { type = "string" } },
            required = new[] { "name" }
        };

        var request = new ValidationRequest
        {
            JsonData = new { name = "Ada" },
            Schema = schema
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata!.SchemaTitle, Is.EqualTo("Person"));
            Assert.That(result.Metadata.DataSource, Is.EqualTo("direct"));
        }
    }

    [Test]
    public async Task ValidateAsync_WithInvalidData_ReturnsValidationErrors()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new { name = new { type = "string" } },
            required = new[] { "name" }
        };

        var request = new ValidationRequest
        {
            JsonData = new { },
            Schema = schema
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(e => e.ErrorCode == "MISSING_REQUIRED_PROPERTY"));
        }
    }

    [Test]
    public async Task ValidateAsync_WithDataUrl_FetchesAndValidates()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new { name = new { type = "string" } },
            required = new[] { "name" }
        };


        var dataUrl = "https://example.com/data.json";
        _pathParsingServiceMock
            .Setup(service => service.ValidateAndParseDataUrlAsync(dataUrl, It.IsAny<ValidationOptions?>()))
            .ReturnsAsync(new Uri(dataUrl));

        var schemaJson = "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}},\"required\":[\"name\"]}";
        var dataJson = "{\"name\":\"Ada\"}";

        // SetupHttpMock expects the dataUrl as the third argument for the data fetch
        SetupHttpMock(schemaJson, dataJson);

        var request = new ValidationRequest
        {
            DataUrl = dataUrl,
            Schema = schema
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata!.DataSource, Is.EqualTo(dataUrl));
        }
    }

    [Test]
    public async Task ValidateWithSchemaUriAsync_LoadsSchemaAndValidates()
    {
        // Arrange
        var schemaUri = "https://example.com/schema.json";
        _pathParsingServiceMock
            .Setup(service => service.ValidateAndParseSchemaUriAsync(schemaUri, It.IsAny<ValidationOptions?>()))
            .ReturnsAsync(new Uri(schemaUri));

        var schemaJson = @"{""type"":""object"",""properties"":{ ""name"": {""type"":""string""}},""required"": [""name""] }";
        SetupHttpMock(schemaJson, "{\"name\":\"Ada\"}");

        // Act
        var result = await _service.ValidateWithSchemaUriAsync(new { name = "Ada" }, schemaUri);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }
    }

    [Test]
    public async Task ValidateWithSchemaUriAsync_ChecksCacheBeforeRequestingExternalSchema()
    {
        // Arrange
        var schemaUri = $"https://example.com/{Guid.NewGuid():N}/schema.json";
        _pathParsingServiceMock
            .Setup(service => service.ValidateAndParseSchemaUriAsync(schemaUri, It.IsAny<ValidationOptions?>()))
            .ReturnsAsync(new Uri(schemaUri));

        var schemaJson = @"{""type"":""object"",""properties"":{ ""name"": {""type"":""string""}},""required"": [""name""] }";
        var schemaRequestCount = 0;

        var countingHandler = new CountingHttpMessageHandler(request =>
        {
            var requestUri = request.RequestUri?.ToString() ?? string.Empty;
            if (string.Equals(requestUri, schemaUri, StringComparison.OrdinalIgnoreCase))
            {
                schemaRequestCount++;
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(schemaJson)
            };
        });

        _httpClient?.Dispose();
        _httpClient = TestHttpClientFactory.CreateClient(countingHandler);
        _service = new JsonValidatorService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _pathParsingServiceMock.Object,
            _requestProcessingServiceMock.Object,
            _schemaResolverServiceMock.Object);

        // Act
        var firstResult = await _service.ValidateWithSchemaUriAsync(new { name = "Ada" }, schemaUri);
        var secondResult = await _service.ValidateWithSchemaUriAsync(new { name = "Ada" }, schemaUri);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstResult.IsValid, Is.True);
            Assert.That(secondResult.IsValid, Is.True);
            Assert.That(schemaRequestCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task ValidateSchemaAsync_WithMissingType_ReturnsWarning()
    {
        // Arrange
        var schema = new { title = "Schema" };

        // Act
        var result = await _service.ValidateSchemaAsync(schema);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Exactly(1).Matches<Core.Models.Validation.ValidationError>(error => error.ErrorCode == "MISSING_TYPE"));
        }
    }

    [Test]
    public async Task IsValidAsync_WithValidRequest_ReturnsTrue()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new { name = new { type = "string" } },
            required = new[] { "name" }
        };

        var request = new ValidationRequest
        {
            JsonData = new { name = "Ada" },
            Schema = schema
        };

        // Act
        var result = await _service.IsValidAsync(request);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void ValidateAsync_WithMissingJsonDataAndUrl_ThrowsArgumentException()
    {
        // Arrange
        var request = new ValidationRequest
        {
            Schema = new { type = "object" }
        };

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.ValidateAsync(request));
    }

    [Test]
    public void ValidateAsync_WithMissingSchema_ThrowsArgumentException()
    {
        // Arrange
        var request = new ValidationRequest
        {
            JsonData = new { name = "Ada" }
        };

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.ValidateAsync(request));
    }

    [Test]
    public void ValidateWithSchemaUriAsync_WhenSchemaLoadFails_ThrowsInvalidOperation()
    {
        // Arrange
        var schemaUri = "https://example.com/schema.json";
        _pathParsingServiceMock
            .Setup(service => service.ValidateAndParseSchemaUriAsync(schemaUri, It.IsAny<ValidationOptions?>()))
            .ThrowsAsync(new ArgumentException("Invalid schema URI"));

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.ValidateWithSchemaUriAsync(new { name = "Ada" }, schemaUri));
    }

    [Test]
    public async Task ValidateSchemaAsync_WhenSchemaResolverThrows_ReturnsError()
    {
        // Arrange
        _schemaResolverServiceMock
            .Setup(service => service.CreateSchemaFromJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Schema parse failed"));

        // Act
        var result = await _service.ValidateSchemaAsync(new { type = "object" });

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Exactly(1).Matches<Core.Models.Validation.ValidationError>(error => error.ErrorCode == "SCHEMA_VALIDATION_ERROR"));
        }
    }

    [Test]
    public async Task ValidateAsync_WithReportAdditionalFields_ReportsFieldsNotInSchema()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                age = new { type = "number" }
            },
            required = new[] { "name" },
            additionalProperties = true
        };

        var request = new ValidationRequest
        {
            JsonData = new
            {
                name = "Ada Lovelace",
                age = 36,
                email = "ada@example.com",  // Not in schema
                address = new  // Not in schema
                {
                    city = "London",
                    country = "UK"
                }
            },
            Schema = schema,
            Options = new ValidationOptions
            {
                ReportAdditionalFields = true
            }
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.True, "Data should be valid even with additional fields");
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "ADDITIONAL_FIELD" && e.Path == "email"),
                "Should report 'email' as an additional field");
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "ADDITIONAL_FIELD" && e.Path == "address"),
                "Should report 'address' as an additional field");
            Assert.That(result.Errors.Where(e => e.ErrorCode == "ADDITIONAL_FIELD").All(e => e.Severity == "Info"),
                "Additional field warnings should have 'Info' severity");
        }
    }

    [Test]
    public async Task ValidateAsync_WhenSchemaForbidsAdditionalProperties_TagsExtraFieldsAsAdditionalField()
    {
        // Arrange — schema explicitly disallows additional properties
        var schema = new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" }
            },
            required = new[] { "name" },
            additionalProperties = false
        };

        var request = new ValidationRequest
        {
            JsonData = new
            {
                name = "Ada Lovelace",
                email = "ada@example.com"  // Not in schema, schema forbids it
            },
            Schema = schema,
            Options = new ValidationOptions()
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert — extra field should be ADDITIONAL_FIELD (not VALIDATION_ERROR) so that
        // OwnSchemaValidation mode can control its severity
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "ADDITIONAL_FIELD"),
                "Extra field from additionalProperties:false schema should be tagged ADDITIONAL_FIELD");
            Assert.That(result.Errors, Has.None.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "VALIDATION_ERROR" && e.Path.Contains("email")),
                "Extra field should not be reported as a generic VALIDATION_ERROR");
        }
    }

    [Test]
    public async Task ValidateAsync_WithReportAdditionalFieldsFalse_DoesNotReportAdditionalFields()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" }
            },
            required = new[] { "name" },
            additionalProperties = true
        };

        var request = new ValidationRequest
        {
            JsonData = new
            {
                name = "Ada Lovelace",
                email = "ada@example.com"  // Not in schema
            },
            Schema = schema,
            Options = new ValidationOptions
            {
                ReportAdditionalFields = false  // Explicitly disabled
            }
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Has.None.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "ADDITIONAL_FIELD"),
                "Should not report additional fields when option is disabled");
        }
    }

    [Test]
    public async Task ValidateAsync_WithReportAdditionalFields_HandlesNestedObjects()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                address = new
                {
                    type = "object",
                    properties = new
                    {
                        city = new { type = "string" }
                    },
                    additionalProperties = true
                }
            },
            additionalProperties = true
        };

        var request = new ValidationRequest
        {
            JsonData = new
            {
                name = "Ada Lovelace",
                address = new
                {
                    city = "London",
                    postcode = "SW1A 1AA"  // Not in schema
                }
            },
            Schema = schema,
            Options = new ValidationOptions
            {
                ReportAdditionalFields = true
            }
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "ADDITIONAL_FIELD" && e.Path == "address.postcode"),
                "Should report nested additional fields");
        }
    }

    [Test]
    public async Task ValidateAsync_WithReportAdditionalFields_HandlesArrays()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new
            {
                users = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string" }
                        },
                        additionalProperties = true
                    }
                }
            },
            additionalProperties = true
        };

        var request = new ValidationRequest
        {
            JsonData = new
            {
                users = new object[]
                {
                    new { name = "Ada", age = 36 },  // 'age' not in schema
                    new { name = "Charles", role = "Professor" }  // 'role' not in schema
                }
            },
            Schema = schema,
            Options = new ValidationOptions
            {
                ReportAdditionalFields = true
            }
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "ADDITIONAL_FIELD" && e.Path == "users[].age"),
                "Should report additional fields in array items");
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "ADDITIONAL_FIELD" && e.Path == "users[].role"),
                "Should report additional fields in array items");
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "ADDITIONAL_FIELD" && e.Message == "Field 'users[].age' is not defined in the schema"),
                "Should normalize array indices in additional-field message for users.age");
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "ADDITIONAL_FIELD" && e.Message == "Field 'users[].role' is not defined in the schema"),
                "Should normalize array indices in additional-field message for users.role");
        }
    }

    [Test]
    public async Task ValidateAsync_WithReportAdditionalFields_ArrayMessages_DoNotContainIndexValues()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new
            {
                service_at_locations = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            id = new { type = "string" }
                        },
                        additionalProperties = true
                    }
                }
            },
            additionalProperties = true
        };

        var request = new ValidationRequest
        {
            JsonData = new
            {
                service_at_locations = new object[]
                {
                    new { id = "1", regular_schedule = "weekdays" }
                }
            },
            Schema = schema,
            Options = new ValidationOptions
            {
                ReportAdditionalFields = true
            }
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        var additionalField = result.Errors.Single(e => e.ErrorCode == "ADDITIONAL_FIELD");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(additionalField.Path, Is.EqualTo("service_at_locations[].regular_schedule"));
            Assert.That(additionalField.Message, Is.EqualTo("Field 'service_at_locations[].regular_schedule' is not defined in the schema"));
            Assert.That(additionalField.Message, Does.Not.Contain("[0]"), "Message should not include array indices");
        }
    }

    [Test]
    public async Task ValidateAsync_WithReportAdditionalFields_ArrayDuplicates_UsesSingleNormalizedMessage()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new
            {
                users = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string" }
                        },
                        additionalProperties = true
                    }
                }
            },
            additionalProperties = true
        };

        var request = new ValidationRequest
        {
            JsonData = new
            {
                users = new object[]
                {
                    new { name = "Ada", age = 36 },
                    new { name = "Charles", age = 42 }
                }
            },
            Schema = schema,
            Options = new ValidationOptions
            {
                ReportAdditionalFields = true
            }
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        var ageWarnings = result.Errors
            .Where(e => e.ErrorCode == "ADDITIONAL_FIELD" && e.Path == "users[].age")
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ageWarnings, Has.Count.EqualTo(1), "Expected one deduplicated warning for users[].age");
            Assert.That(ageWarnings[0].Message, Is.EqualTo("Field 'users[].age' is not defined in the schema"));
            Assert.That(ageWarnings[0].Message, Does.Not.Contain("[0]"), "Deduplicated message should not include concrete indexes");
        }
    }

    [Test]
    public async Task ValidateAsync_WithCircularUserJson_ReturnsSpecificStructureViolationError()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            additionalProperties = true
        };

        var cyclic = new Dictionary<string, object?>();
        cyclic["self"] = cyclic;

        var request = new ValidationRequest
        {
            JsonData = cyclic,
            Schema = schema
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "JSON_STRUCTURE_VIOLATION"));
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "JSON_STRUCTURE_VIOLATION" && e.Path.Contains("$.self", StringComparison.Ordinal)));
        }
    }

    [Test]
    public async Task ValidateAsync_WithJsonNodePayload_DoesNotTriggerCycleStructureViolation()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new
            {
                data = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            id = new { type = "string" }
                        }
                    }
                }
            }
        };

        var jsonNodePayload = JsonNode.Parse("""
        {
          "data": [
            { "id": "abc" }
          ]
        }
        """);

        var request = new ValidationRequest
        {
            JsonData = jsonNodePayload,
            Schema = schema
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        Assert.That(result.Errors.Any(e => e.ErrorCode == "JSON_STRUCTURE_VIOLATION"), Is.False);
    }

    [Test]
    public async Task ValidateAsync_WithTooDeepUserJsonString_ReturnsSpecificStructureViolationError()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            additionalProperties = true
        };

        var deepJson = BuildDeepJson(70);

        var request = new ValidationRequest
        {
            JsonData = deepJson,
            Schema = schema
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "JSON_STRUCTURE_VIOLATION"));
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.Message.Contains("maximum depth of 64", StringComparison.OrdinalIgnoreCase)));
        }
    }

    [Test]
    public async Task ValidateAsync_WithCircularUserSchema_ReturnsSpecificStructureViolationErrorWithPath()
    {
        // Arrange
        var circularSchema = new Dictionary<string, object?>
        {
            ["type"] = "object"
        };
        circularSchema["self"] = circularSchema;

        var request = new ValidationRequest
        {
            JsonData = new { name = "Ada" },
            Schema = circularSchema
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "SCHEMA_STRUCTURE_VIOLATION"));
            Assert.That(result.Errors, Has.Some.Matches<Core.Models.Validation.ValidationError>(
                e => e.ErrorCode == "SCHEMA_STRUCTURE_VIOLATION" && e.Path.Contains("$.self", StringComparison.Ordinal)));
        }
    }

    [Test]
    public async Task ValidateAsync_WithFormatMismatch_IncludesFailedValueInErrorMessage()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", format = "uri" }
            }
        };

        var request = new ValidationRequest
        {
            JsonData = new { url = "not-a-valid-uri" },
            Schema = schema
        };

        // Act
        var result = await _service.ValidateAsync(request);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            var formatError = result.Errors.FirstOrDefault(e => e.Path == "url");
            Assert.That(formatError, Is.Not.Null);
            Assert.That(formatError!.Message, Contains.Substring("does not match format"));
            Assert.That(formatError.Message, Contains.Substring("(failed value: \"not-a-valid-uri\")"));
        }
    }

    private static string BuildDeepJson(int depth)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < depth; i++)
        {
            sb.Append("{\"a\":");
        }

        sb.Append("\"value\"");

        for (var i = 0; i < depth; i++)
        {
            sb.Append('}');
        }

        return sb.ToString();
    }

    private void SetupHttpMock(string schemaJson, string dataJson)
    {
        var mockHandler = new MockHttpMessageHandler(schemaJson, dataJson);
        _httpClient?.Dispose();
        _httpClient = TestHttpClientFactory.CreateClient(mockHandler);
        _service = new JsonValidatorService(
            _loggerMock.Object,
            CreateFactory(_httpClient),
            _pathParsingServiceMock.Object,
            _requestProcessingServiceMock.Object,
            _schemaResolverServiceMock.Object);
    }

    private static IHttpClientFactory CreateFactory(HttpClient httpClient)
    {
        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        return mock.Object;
    }

    private sealed class MockHttpMessageHandler(string schemaJson, string dataJson) : HttpMessageHandler
    {
        private readonly string _schemaJson = schemaJson;
        private readonly string _dataJson = dataJson;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestUri = request.RequestUri?.ToString() ?? string.Empty;
            // Debug: Output the requested URI for troubleshooting
            System.Diagnostics.Debug.WriteLine($"MockHttpMessageHandler received request: {requestUri}");
            var responseBody = requestUri.Contains("schema", StringComparison.OrdinalIgnoreCase)
                ? _schemaJson
                : _dataJson;

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });
        }
    }

    private sealed class CountingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
