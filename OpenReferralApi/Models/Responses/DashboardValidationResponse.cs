
namespace OpenReferralApi.Models.Responses;

public class DashboardValidationResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool ServiceAvailable { get; set; }
    public bool TestsPassed { get; set; }
    public string Version { get; set; }
    public string Service { get; set; }
    public string? Message { get; set; }
    public List<Test> Results { get; internal set; }
}