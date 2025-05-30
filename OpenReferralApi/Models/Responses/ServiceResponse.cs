namespace OpenReferralApi.Models.Responses;

public class ServiceResponse
{
    public string Url { get; set; } = null!;
    public bool IsValid { get; set; }
    public string Profile { get; set; } = null!;
    public string ProfileReason { get; set; } = null!;
}