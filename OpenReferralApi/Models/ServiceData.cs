using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OpenReferralApi.Models;

public class ServiceData
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    [BsonElement("name")]
    public Field? Name { get; set; }
    [BsonElement("comment")]
    public Field? Comment { get; set; }
    [BsonElement("description")]
    public Field? Description { get; set; }
    [BsonElement("service")]
    [JsonPropertyName("service")]
    public Field? ServiceUrl { get; set; }
    [BsonElement("developer")]
    public Field? Developer { get; set; }
    [BsonElement("publisher")]
    public Field? Publisher { get; set; }
    [BsonElement("schemaVersion")]
    public Field? SchemaVersion { get; set; }
    [BsonElement("statusIsUp")]
    public Field? StatusIsUp { get; set; }
    [BsonElement("statusIsValid")]
    public Field? StatusIsValid { get; set; }
    [BsonElement("statusOverall")]
    public Field? StatusOverall { get; set; }
    [BsonElement("testDate")]
    public Field? TestDate { get; set; }
    [BsonElement("lastTested")]
    public Field? LastTested { get; set; }
    [BsonElement("otherDetails")]
    public List<Field>? OtherDetails { get; set; }
}