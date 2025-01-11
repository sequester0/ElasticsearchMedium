namespace ElasticsearchMedium.Core.Models;

public class ElasticsearchRequestModel
{
    public string IndexTag { get; set; } = string.Empty;
    public string? SavedQueryId { get; set; }
    public string? LuceneQueryLanguage { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<string>? To { get; set; }
    public List<string>? Cc { get; set; }
    public List<TableHeader> TableHeaders { get; set; } = new();
    public List<string> TableValues { get; set; } = new();
    public Dictionary<string, List<string>>? ExprExists { get; set; }
    public Dictionary<string, List<string>>? ExprNotExists { get; set; }
    public string? Func { get; set; }
    public int? MinQty { get; set; }
    public string? Sort { get; set; }
    public string? SortField { get; set; }
    public int QuerySize { get; set; } = 1000;
}

public class TableHeader
{
    public string Label { get; set; } = string.Empty;
    public bool Visibility { get; set; } = true;
}