namespace OpenReferralApi.Models;

public class TestCaseGroup
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string MessageLevel { get; set; } = null!;
    public bool Required { get; set; } = false;
    public List<TestCase> Tests { get; set; } = null!;
}