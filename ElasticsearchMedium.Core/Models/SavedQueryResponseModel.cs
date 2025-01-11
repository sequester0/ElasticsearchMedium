using System.Text.Json.Serialization;

namespace ElasticsearchMedium.Core.Models;

public class SavedQueryResponseModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("attributes")]
    public Attributes Attributes { get; set; } = new();
}

public class Attributes
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("kibanaSavedObjectMeta")]
    public KibanaSavedObjectMeta KibanaSavedObjectMeta { get; set; } = new();
}

public class KibanaSavedObjectMeta
{
    [JsonPropertyName("searchSourceJSON")]
    public string SearchSourceJSON { get; set; } = string.Empty;
}

public class SearchSourceJson
{
    [JsonPropertyName("query")]
    public Query? Query { get; set; }
}

public class Query
{
    [JsonPropertyName("query")]
    public string? query { get; set; }
    
    [JsonPropertyName("language")]
    public string? Language { get; set; }
}