namespace OpenReferralApi.Models;

public class Test
{
    public Test() { }
    
    public Test (TestCase testCase, string baseUrl, bool success, Issue issue)
    {
        Name = testCase.Name;
        Description = testCase.Description;
        Endpoint = baseUrl + testCase.Endpoint;
        Success = success;
        Messages = new List<Issue>{ issue };
    }

    public string Name { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public string? Id { get; set; }
    public List<string>? Ids { get; set; }
    public string Description { get; set; } = null!;
    public bool Success { get; set; }
    public List<Issue> Messages { get; set; } = null!;
}