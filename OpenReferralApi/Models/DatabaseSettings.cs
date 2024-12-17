namespace OpenReferralApi.Models;

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string ServicesCollection { get; set; } = null!;
    public string ColumnsCollection { get; set; } = null!;
    public string ViewsCollection { get; set; } = null!;
}