using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OpenReferralApi.Models;

public class Field
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonIgnore]
    public string? Id { get; set; }
    [BsonElement("label")]
    public string? Label { get; set; }
    [BsonElement("dataType")]
    [JsonPropertyName("dataType")]
    public string? Datatype { get; set; }
    [BsonElement("value")]
    public object? Value { get; set; }
    [BsonElement("url")]
    public string? Url { get; set; }
    [JsonIgnore]
    [BsonElement("name")]
    public string? Name { get; set; }
}