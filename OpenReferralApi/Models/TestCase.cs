namespace OpenReferralApi.Models;

public class TestCase
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public string Schema { get; set; } = null!;
    public bool Pagination { get; set; } = false;
    public string? UseIdFrom { get; set; }
}