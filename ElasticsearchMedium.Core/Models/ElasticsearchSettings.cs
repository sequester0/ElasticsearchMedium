namespace ElasticsearchMedium.Core.Models;

public class ElasticsearchSettings
{
    public string Url { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ElasticUiEndpoint { get; set; } = string.Empty;
}