using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace OpenReferralApi.Tests.Swagger;

[TestFixture]
public class SwaggerExamplesApplierTests
{
    [Test]
    public void Apply_AddsRequestAndResponseExamples_ForOpenReferralValidate()
    {
        var operation = CreateOperation(includeRequestBody: true, "200", "400", "429", "500");
        var document = CreateDocument("/openreferral/validate", HttpMethod.Post, operation);

        ApplyExamples(document);

        var requestExample = GetRequestExample(operation);
        Assert.That(requestExample, Is.Not.Null);
        Assert.That(requestExample!.ToJsonString(), Does.Contain("openApiSchema"));

        var okExample = GetResponseExample(operation, "200");
        Assert.That(okExample, Is.Not.Null);
        Assert.That(okExample!.ToJsonString(), Does.Contain("totalEndpoints"));

        var badRequestExample = GetResponseExample(operation, "400");
        Assert.That(badRequestExample, Is.Not.Null);
        Assert.That(badRequestExample!.ToJsonString(), Does.Contain("validation errors"));
    }

    [Test]
    public void Apply_MatchesPaths_CaseInsensitively()
    {
        var operation = CreateOperation(includeRequestBody: false, "200", "429", "500");
        var document = CreateDocument("/API/FEEDVALIDATION/FEEDS", HttpMethod.Get, operation);

        ApplyExamples(document);

        var okExample = GetResponseExample(operation, "200");
        Assert.That(okExample, Is.Not.Null);
        Assert.That(okExample!.ToJsonString(), Does.Contain("Example Service Feed"));
    }

    [Test]
    public void Apply_AddsMockEndpointExamples_ForV1Validate()
    {
        var operation = CreateOperation(includeRequestBody: false, "200", "404", "500");
        var document = CreateDocument("/api/mock/v1/validate", HttpMethod.Post, operation);

        ApplyExamples(document);

        var notFoundExample = GetResponseExample(operation, "404");
        Assert.That(notFoundExample, Is.Not.Null);
        Assert.That(notFoundExample!.ToJsonString(), Does.Contain("Mock file not found"));

        var serverErrorExample = GetResponseExample(operation, "500");
        Assert.That(serverErrorExample, Is.Not.Null);
        Assert.That(serverErrorExample!.ToJsonString(), Does.Contain("Error reading mock file"));
    }

    private static OpenApiDocument CreateDocument(string path, HttpMethod method, OpenApiOperation operation)
    {
        return new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                [path] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [method] = operation
                    }
                }
            }
        };
    }

    private static OpenApiOperation CreateOperation(bool includeRequestBody, params string[] responseStatusCodes)
    {
        var operation = new OpenApiOperation
        {
            Responses = []
        };

        if (includeRequestBody)
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType()
                }
            };
        }

        foreach (var statusCode in responseStatusCodes)
        {
            operation.Responses[statusCode] = new OpenApiResponse
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType()
                }
            };
        }

        return operation;
    }

    private static void ApplyExamples(OpenApiDocument document)
    {
        var applierType = typeof(Program).Assembly.GetType("OpenReferralApi.Swagger.SwaggerExamplesApplier");
        Assert.That(applierType, Is.Not.Null, "SwaggerExamplesApplier type not found via reflection.");

        var applyMethod = applierType!.GetMethod(
            "Apply",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(OpenApiDocument)],
            modifiers: null);

        Assert.That(applyMethod, Is.Not.Null, "SwaggerExamplesApplier.Apply(OpenApiDocument) method not found.");

        applyMethod!.Invoke(null, [document]);
    }

    private static JsonNode? GetRequestExample(OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content == null)
        {
            return null;
        }

        if (!operation.RequestBody.Content.TryGetValue("application/json", out var mediaType) || mediaType?.Example == null)
        {
            return null;
        }

        return mediaType.Example as JsonNode;
    }

    private static JsonNode? GetResponseExample(OpenApiOperation operation, string statusCode)
    {
        if (operation.Responses == null)
        {
            return null;
        }

        if (!operation.Responses.TryGetValue(statusCode, out var response) || response?.Content == null)
        {
            return null;
        }

        if (!response.Content.TryGetValue("application/json", out var mediaType) || mediaType?.Example == null)
        {
            return null;
        }

        return mediaType.Example as JsonNode;
    }
}
