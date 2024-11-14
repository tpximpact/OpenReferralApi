using System.Text.Json.Serialization;

namespace OpenReferralApi.Models;

public class Service
{
    public Service(ServiceData serviceData )
    {
        Id = serviceData.Id;
        Name = serviceData.Name;
        Comment = serviceData.Comment;
        ServiceUrl = serviceData.ServiceUrl;
        Developer = serviceData.Developer;
        Publisher = serviceData.Publisher;
        SchemaVersion = serviceData.SchemaVersion;
        StatusIsUp = serviceData.StatusIsUp;
        StatusIsValid = serviceData.StatusIsValid;
        StatusOverall = serviceData.StatusOverall;
        TestDate = serviceData.TestDate;
        TestDate.Url += Id;
    }
    
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("name")]
    public Field? Name { get; set; }
    [JsonPropertyName("comment")]
    public Field? Comment { get; set; }
    [JsonPropertyName("service")]
    public Field? ServiceUrl { get; set; }
    [JsonPropertyName("developer")]
    public Field? Developer { get; set; }
    [JsonPropertyName("publisher")]
    public Field? Publisher { get; set; }
    [JsonPropertyName("schemaVersion")]
    public Field? SchemaVersion { get; set; }
    [JsonPropertyName("statusIsUp")]
    public Field? StatusIsUp { get; set; }
    [JsonPropertyName("statusIsValid")]
    public Field? StatusIsValid { get; set; }
    [JsonPropertyName("statusOverall")]
    public Field? StatusOverall { get; set; }
    [JsonPropertyName("testDate")]
    public Field? TestDate { get; set; }
}