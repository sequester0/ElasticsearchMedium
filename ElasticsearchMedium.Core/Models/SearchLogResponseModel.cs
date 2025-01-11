namespace ElasticsearchMedium.Core.Models;

public sealed class SearchLogResponseModel
{
    public int took { get; set; }
    public bool timed_out { get; set; }
    public _shards _shards { get; set; }
    public Hits hits { get; set; }
}

public sealed class _shards
{
    public int total { get; set; }
    public int successful { get; set; }
    public int skipped { get; set; }
    public int failed { get; set; }
}

public sealed class Hits
{
    public Total total { get; set; }
    public double? max_score { get; set; }
    public List<Hits1> hits { get; set; }
}

public sealed class Total
{
    public int value { get; set; }
    public string relation { get; set; }
}

public sealed class Hits1
{
    public string _index { get; set; }
    public string _id { get; set; }
    public double? _score { get; set; }
    public string[] _ignored { get; set; }
    public Dictionary<string, object> _source { get; set; }
    public List<long>? sort { get; set; }
}