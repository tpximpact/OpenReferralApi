namespace OpenReferralApi.Models;

public class ValidationResponse
{
    public ServiceDetails Service { get; set; }
    public List<MetaData> Metadata { get; set; }
    public List<TestGroup> TestSuites { get; set; } = null!;
}

public class MetaData
{
    public string Label { get; set; } = null!;
    public string Value { get; set; } = null!;
}

public class ServiceDetails
{
    public string Url { get; set; } = null!;
    public bool IsValid { get; set; }
    public string Profile { get; set; } = null!;
}

public class Test
{
    public string Name { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public string Description { get; set; } = null!;
    public bool Success { get; set; }
    public List<Issue> Issues { get; set; } = null!;
}

public class Issue
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? Parameters { get; set; }
}

public class TestGroup
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string IssueLevel { get; set; } = null!;
    public bool Required { get; set; }
    public bool Success { get; set; }
    public List<Test> Tests { get; set; } = null!;
}