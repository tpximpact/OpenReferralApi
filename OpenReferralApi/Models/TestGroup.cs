namespace OpenReferralApi.Models;

public class TestGroup
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string MessageLevel { get; set; } = null!;
    public bool Required { get; set; }
    public bool Success { get; set; }
    public List<Test> Tests { get; set; } = null!;
}