using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OpenReferralApi.Models;

public class DashboardOutput
{
    public Definitions Definitions { get; set; }
    public List<Service> Data { get; set; }
}

public class Definitions
{
    public Dictionary<string, Field> Columns { get; set; }
    public Dictionary<string, View> Views { get; set; }
}

public class View
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonIgnore]
    public string? Id { get; set; }
    [BsonElement("name")]
    [JsonIgnore]
    public string? Name { get; set; }
    [BsonElement("columns")]
    public List<string> Columns { get; set; }
    [BsonElement("sortBy")]
    [JsonPropertyName("sortBy")]
    public List<string> SortBy { get; set; }
    [BsonElement("defaultSortDirection")]
    [JsonPropertyName("defaultSortDirection")]
    public string DefaultSortDirection { get; set; }
    [BsonElement("defaultSortBy")]
    [JsonPropertyName("defaultSortBy")]
    public string DefaultSortBy { get; set; }
    [BsonElement("showPassingRowsOnly")]
    [JsonPropertyName("showPassingRowsOnly")]
    public bool ShowPassingRowsOnly { get; set; }
    [BsonElement("rowsPerPage")]
    [JsonPropertyName("rowsPerPage")]
    public int RowsPerPage { get; set; }
}