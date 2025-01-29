namespace OpenReferralApi.Models;

public class Test
{
    public string Name { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public string? Id { get; set; }
    public string Description { get; set; } = null!;
    public bool Success { get; set; }
    public List<Issue> Messages { get; set; } = null!;
}