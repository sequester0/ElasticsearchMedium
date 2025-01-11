using ElasticsearchMedium.Core.Models;

namespace ElasticsearchMedium.Core.Interfaces;

public interface IElasticsearchService
{
    Task<string?> GetSavedQueryDefinition(string queryId);
    Task<SearchLogResponseModel> GetLogWithQuery(string query, ElasticsearchRequestModel model, List<long>? searchAfterValue = null);
    Task<LogProcessResult> ProcessLogs(ElasticsearchRequestModel config);
}