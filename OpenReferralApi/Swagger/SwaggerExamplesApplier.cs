using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace OpenReferralApi.Swagger;

internal static class SwaggerExamplesApplier
{
    public static void Apply(OpenApiDocument document)
    {
        var registrations = ValidationOperationExamples.GetRegistrations()
            .Concat(FeedValidationOperationExamples.GetRegistrations())
            .Concat(MockOperationExamples.GetRegistrations());

        foreach (var registration in registrations)
        {
            ApplyExamplesToOperation(
                GetOperation(document, registration.Path, registration.Method),
                registration.RequestExample,
                registration.ResponseExamples);
        }
    }

    private static OpenApiOperation? GetOperation(OpenApiDocument document, string path, HttpMethod method)
    {
        if (!TryGetPathItem(document, path, out var pathItem) || pathItem?.Operations == null)
        {
            return null;
        }

        return pathItem.Operations.TryGetValue(method, out var operation) ? operation : null;
    }

    private static bool TryGetPathItem(OpenApiDocument document, string path, out IOpenApiPathItem? pathItem)
    {
        if (document.Paths.TryGetValue(path, out var exactPathItem))
        {
            pathItem = exactPathItem;
            return true;
        }

        var match = document.Paths.FirstOrDefault(kvp =>
            string.Equals(kvp.Key, path, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(match.Key))
        {
            pathItem = match.Value;
            return true;
        }

        pathItem = null;
        return false;
    }

    private static void ApplyExamplesToOperation(
        OpenApiOperation? operation,
        string? requestExample,
        IReadOnlyDictionary<string, string> responseExamples)
    {
        if (operation?.Responses == null)
        {
            return;
        }

        JsonNode? requestExampleNode = null;
        if (!string.IsNullOrWhiteSpace(requestExample))
        {
            requestExampleNode = JsonNode.Parse(requestExample);
        }

        if (requestExampleNode != null && operation.RequestBody?.Content != null)
        {
            foreach (var mediaType in operation.RequestBody.Content.Values)
            {
                mediaType.Example = requestExampleNode;
            }
        }

        foreach (var (statusCode, responseExample) in responseExamples)
        {
            if (!operation.Responses.TryGetValue(statusCode, out var response) || response?.Content == null)
            {
                continue;
            }

            var responseExampleNode = JsonNode.Parse(responseExample);
            if (responseExampleNode == null)
            {
                continue;
            }

            foreach (var mediaType in response.Content.Values)
            {
                mediaType.Example = responseExampleNode;
            }
        }
    }
}
