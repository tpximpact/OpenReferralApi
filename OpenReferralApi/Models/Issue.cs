namespace OpenReferralApi.Models;

public class Issue
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string ErrorIn { get; set; } = null!;
    public string ErrorAt { get; set; } = null!;
    public Dictionary<string, string>? Parameters { get; set; }
    public string? Endpoint { get; set; }
}