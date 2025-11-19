using Newtonsoft.Json;

namespace OpenReferralApi.Models;

public interface IPage
{
    int TotalItems { get; set; }
    int TotalPages { get; set; }
    int PageNumber { get; set; }
    int Size { get; set; }
    bool FirstPage { get; set; }
    bool LastPage { get; set; }
    bool Empty { get; set; }
    List<ObjectWithId> Contents { get; set; }
}
public class PageV3 : IPage
{
    [JsonProperty("total_items")]
    public int TotalItems { get; set; }

    [JsonProperty("total_pages")]
    public int TotalPages { get; set; }

    [JsonProperty("page_number")]
    public int PageNumber { get; set; }

    [JsonProperty("size")]
    public int Size { get; set; }

    [JsonProperty("first_page")]
    public bool FirstPage { get; set; }

    [JsonProperty("last_page")]
    public bool LastPage { get; set; }

    [JsonProperty("empty")]
    public bool Empty { get; set; }

    [JsonProperty("contents")]
    public List<ObjectWithId> Contents { get; set; } = null!;
}

public class PageV1 : IPage
{
    [JsonProperty("totalElements")]
    public int TotalItems { get; set; }

    [JsonProperty("totalPages")]
    public int TotalPages { get; set; }

    [JsonProperty("number")]
    public int PageNumber { get; set; }

    [JsonProperty("size")]
    public int Size { get; set; }

    [JsonProperty("first")]
    public bool FirstPage { get; set; }

    [JsonProperty("last")]
    public bool LastPage { get; set; }

    public bool Empty { get; set; } = false;

    [JsonProperty("content")]
    public List<ObjectWithId> Contents { get; set; } = null!;
}