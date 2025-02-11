namespace OpenReferralApi.Models;

public class DashboardSubmission
{
    public string Name { get; set; } = null!;
    public string Publisher { get; set; } = null!;
    public string PublisherUrl { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Developer { get; set; } = null!;
    public string DeveloperUrl { get; set; } = null!;
    public string ServiceUrl { get; set; } = null!;
    public string ContactEmail { get; set; } = null!;
}