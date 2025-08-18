namespace OpenReferralApi.Models;

public class TestProfile
{
    public required string Profile { get; set; }
    public required List<TestCaseGroup> TestGroups { get; set; }
}