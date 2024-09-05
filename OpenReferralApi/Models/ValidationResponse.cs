namespace OpenReferralApi.Models;

public class ValidationResponse
{
    public string ServiceUrl { get; set; } = null!;
    public bool AllTestsPassed { get; set; }
    public string TestsProfile { get; set; } = null!;
    public List<Test> Tests { get; set; } = null!;
}

public class Test
{
    public string Name { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public string Description { get; set; } = null!;
    public bool Success { get; set; }
    // public List<Issue> Warnings { get; set; } = null!;
    public List<Issue> Issues { get; set; } = null!;
}

public class Issue
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Message { get; set; } = null!;
}

public class TestGroup
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool Success { get; set; }
}