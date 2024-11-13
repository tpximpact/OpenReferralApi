namespace OpenReferralApi.Models;

public class ValidationResponse
{
    public ServiceDetails Service { get; set; }
    public List<MetaData> Metadata { get; set; }
    public List<TestGroup> TestSuites { get; set; } = null!;
}