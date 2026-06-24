namespace OpenReferralApi.Core.Models.Schema;

/// <summary>
/// Represents a non-fatal issue discovered while resolving schema references.
/// </summary>
public sealed class SchemaResolutionIssue
{
    public string ErrorCode { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public string ReferencePath { get; init; } = string.Empty;
}