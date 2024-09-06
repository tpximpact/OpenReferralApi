using System.Text.Json.Serialization;

namespace OpenReferralApi.Models;

public class Page
{
    [JsonPropertyName("total_items")]
    public int TotalItems { get; set; }
    
    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }
    
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }
    
    [JsonPropertyName("size")]
    public int Size { get; set; }
    
    [JsonPropertyName("first_page")]
    public bool FirstPage { get; set; }
    
    [JsonPropertyName("last_page")]
    public bool LastPage { get; set; }
    
    [JsonPropertyName("empty")]
    public bool Empty { get; set; }
    
    [JsonPropertyName("contents")]
    public List<Object> Contents { get; set; } = null!;
}