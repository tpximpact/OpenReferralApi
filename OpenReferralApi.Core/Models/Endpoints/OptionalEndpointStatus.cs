namespace OpenReferralApi.Core.Models.Endpoints;

/// <summary>
/// Status of an optional endpoint validation
/// </summary>
public enum OptionalEndpointStatus
{
    /// <summary>
    /// Endpoint is required and must be implemented.
    /// </summary>
    Required,

    /// <summary>
    /// Optional endpoint is implemented and reachable.
    /// </summary>
    Implemented,

    /// <summary>
    /// Optional endpoint is not implemented, which is acceptable.
    /// </summary>
    NotImplemented,

    /// <summary>
    /// Optional endpoint returned an error state.
    /// </summary>
    Error
}
