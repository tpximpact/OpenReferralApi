namespace OpenReferralApi.Models.Responses;

public class ValidationResponse
{
    public ServiceResponse Service { get; set; }
    public List<MetaData> Metadata { get; set; }
    public List<TestGroup> TestSuites { get; set; } = null!;
}