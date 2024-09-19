namespace OpenReferralApi.Models;

public class TestProfile
{
    public string Name { get; set; } = null!;
    public List<TestCaseGroup> TestGroups { get; set; } = null!;
}

public class TestCaseGroup
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string IssueLevel { get; set; } = null!;
    public bool Required { get; set; } = false;
    public List<TestCase> Tests { get; set; } = null!;
}

public class TestCase
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public string Schema { get; set; } = null!;
    public bool Pagination { get; set; } = false;
}