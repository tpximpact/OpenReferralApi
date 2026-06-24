namespace OpenReferralApi.Core.Models.Endpoints;

/// <summary>
/// Represents an endpoint group with collection and parameterized endpoints
/// </summary>
public class EndpointGroup
{
    public string RootPath { get; set; } = string.Empty;
    public List<EndpointInfo> CollectionEndpoints { get; set; } = [];
    public List<EndpointInfo> ParameterizedEndpoints { get; set; } = [];
    public List<EndpointInfo> Endpoints => [.. CollectionEndpoints, .. ParameterizedEndpoints];
}
