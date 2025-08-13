using System;
using Newtonsoft.Json;

namespace OpenReferralApi.Models;

public class ObjectWithId
{
    [JsonProperty("id")]
    public string? Id { get; set; }
}
