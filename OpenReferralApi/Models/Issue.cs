namespace OpenReferralApi.Models;

public class Issue
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string SchemaPath { get; set; } = null!;
    public string Path { get; set; } = null!;
    public string? Parameters { get; set; }
}