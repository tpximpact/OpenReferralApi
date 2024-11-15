namespace OpenReferralApi.Models;

public class TestProfile
{
    public string Profile { get; set; } = null!;
    public List<TestCaseGroup> TestGroups { get; set; } = null!;
}