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
    [BsonElement("active")]
    public bool Active { get; set; }
    [BsonElement("email")]
    public Field? Email { get; set; }
    
    public ServiceData(DashboardSubmission submission)
    {
        Active = false;
        Name = new Field()
        {
            Value = submission.Name,
            Label = "Title", 
            Datatype = "oruk:dataType:string", 
            Description = "Name/Title of the service"
        };
        Developer = new Field()
        {
            Value = submission.Developer,
            Url = submission.DeveloperUrl,
            Label = "Developed by",
            Datatype = "oruk:dataType:string"
        };
        Publisher = new Field()
        {
            Value = submission.Publisher,
            Url = submission.PublisherUrl,
            Label = "Published by",
            Datatype = "oruk:dataType:string"
        };
        Description = new Field()
        {
            Value = submission.Description,
            Label = "Description",
            Description = "A full description of the service, the data it holds, and what it aims to provide/achieve",
            Datatype = "oruk:dataType:string"
        };
        ServiceUrl = new Field()
        {
            Value = submission.ServiceUrl,
            Url = submission.ServiceUrl,
            Label = "URL",
            Datatype = "oruk:dataType:anyURI"
        };
        Email = new Field()
        {
            Value = submission.ContactEmail,
            Label = "Contact Email",
            Datatype = "oruk:dataType:string"
        };
        SchemaVersion = new Field()
        {
            Value = submission.Version,
            Label = "Schema version",
            Datatype = "oruk:dataType:string"
        };
        StatusIsUp = new Field()
        {
            Value = 0,
            Label = "Service is available?",
            Datatype = "oruk:dataType.success"
        };
        StatusIsValid = new Field()
        {
            Value = 0,
            Label = "Service is valid?",
            Datatype = "oruk:dataType.success"
        };
        StatusOverall = new Field()
        {
            Value = 0,
            Label = "Feed passes?",
            Datatype = "oruk:dataType.success"
        };
        TestDate = new Field() 
        {
            Label = "Last tested",
            Datatype = "oruk:dataType:dateTime",
            Value = "2025-01-01T00:00:00.000Z",
            Url = "/developers/dashboard/"
        };
        LastTested = new Field() 
        {
            Label = "Validation status",
            Datatype = "oruk:dataType:dateTime",
            Value = "2025-01-01T00:00:00.000Z"
        };
        Comment = new Field()
        {
            Value = submission.Description,
            Label = "Comment",
            Description = "Short one line comment adding some context about the service",
            Datatype = "oruk:dataType:string"
        };
        OtherDetails = new List<Field>();
    }
}