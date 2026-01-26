namespace OpenReferralApi.Core.Models;

public class SpecificationOptions
{
    public const string SectionName = "Specification";
    
    public string BaseUrl { get; set; } = "https://raw.githubusercontent.com/tpximpact/OpenReferralApi/refs/heads/staging/OpenReferralApi/Schemas/";
}
