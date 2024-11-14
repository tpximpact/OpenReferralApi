namespace OpenReferralApi.Models;

public class TestProfile
{
    public string Name { get; set; } = null!;
    public List<TestCaseGroup> TestGroups { get; set; } = null!;
}