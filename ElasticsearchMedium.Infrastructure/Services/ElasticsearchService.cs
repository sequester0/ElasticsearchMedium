using System.Data;
using System.Text;
using ElasticsearchMedium.Core.Interfaces;
using ElasticsearchMedium.Core.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ElasticsearchMedium.Infrastructure.Services;

public class ElasticsearchService : IElasticsearchService
{
    private readonly HttpClient _httpClient;
    private readonly ElasticsearchSettings _settings;
    private readonly string _authHeader;

    public ElasticsearchService(IOptions<ElasticsearchSettings> settings)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };
        _httpClient = new HttpClient(handler);
        _settings = settings.Value;
        _authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_settings.Username}:{_settings.Password}"));
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {_authHeader}");
    }

    public async Task<string?> GetSavedQueryDefinition(string queryId)
    {
        var response = await _httpClient.GetAsync($"{_settings.ElasticUiEndpoint}/saved_objects/search/{queryId}");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var savedQuery = System.Text.Json.JsonSerializer.Deserialize<SavedQueryResponseModel>(content);

        if (savedQuery?.Attributes.KibanaSavedObjectMeta.SearchSourceJSON == null)
            return null;

        var queryJson = JsonConvert.DeserializeObject<SearchSourceJson>(
            savedQuery.Attributes.KibanaSavedObjectMeta.SearchSourceJSON);

        return queryJson?.Query?.query;
    }

    public async Task<SearchLogResponseModel> GetLogWithQuery(string query, ElasticsearchRequestModel model,
        List<long>? searchAfterValue = null)
    {
        var indexName = $"{model.IndexTag}-*";

        var url =
            $"{_settings.Url}/{indexName}/_search?sort={model.SortField}&size={model.QuerySize}&q={Uri.EscapeDataString(query)}";

        HttpRequestMessage request = new(HttpMethod.Get, url);

        if (searchAfterValue != null)
        {
            var payload = new { search_after = searchAfterValue };
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            request.Content = content;
            request.Method = HttpMethod.Post;
        }

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var options = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<SearchLogResponseModel>(responseContent, options)
                   ?? throw new Exception("Failed to deserialize Elasticsearch response");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<LogProcessResult> ProcessLogs(ElasticsearchRequestModel model)
    {
        var dataTable = new DataTable();
        var searchAfterValue = new List<long>();
        var isThereMoreData = true;

        var query = !string.IsNullOrWhiteSpace(model.SavedQueryId)
            ? await GetSavedQueryDefinition(model.SavedQueryId)
            : model.LuceneQueryLanguage;

        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Either SavedQueryId or LuceneQueryLanguage must be provided");

        while (isThereMoreData)
        {
            var searchResponse =
                await GetLogWithQuery(query, model, searchAfterValue?.Count > 0 ? searchAfterValue : null);

            if (searchResponse.hits.hits.Count == 0)
                break;

            var lastHit = searchResponse.hits.hits.LastOrDefault();
            if (lastHit != null)
            {
                searchAfterValue = lastHit.sort;

                isThereMoreData = searchAfterValue != null && searchAfterValue.Count != 0;
            }
            else
            {
                isThereMoreData = false;
            }

            await ProcessSearchResults(searchResponse, model, dataTable);
        }

        var processedTable = ApplyConfigRules(dataTable, model);

        return new LogProcessResult
        {
            Html = ConvertDataTableToHtml(processedTable, model)
        };
    }

    private async Task ProcessSearchResults(SearchLogResponseModel searchResponse, ElasticsearchRequestModel model,
        DataTable dataTable)
    {
        if (dataTable.Columns.Count == 0)
        {
            model.TableHeaders.ForEach(header =>
                dataTable.Columns.Add(header.Label, typeof(string)));
        }

        foreach (var hit in searchResponse.hits.hits)
        {
            var baseRow = dataTable.NewRow();
            var multiValueColumns = new Dictionary<string, List<string>>();

            for (var i = 0; i < model.TableValues.Count; i++)
            {
                var fieldPath = model.TableValues[i];
                var header = model.TableHeaders[i].Label;

                var value = await ExtractValueFromSource(hit._source, fieldPath);
                var values = value.Split('^', StringSplitOptions.RemoveEmptyEntries);

                if (values.Length > 1)
                {
                    multiValueColumns.Add(header, values.ToList());
                    baseRow[header] = values[0];
                }
                else
                {
                    baseRow[header] = value;
                }
            }

            dataTable.Rows.Add(baseRow);

            if (multiValueColumns.Any())
            {
                var maxValueCount = multiValueColumns.Max(x => x.Value.Count);

                for (int i = 1; i < maxValueCount; i++)
                {
                    var newRow = dataTable.NewRow();

                    foreach (DataColumn col in dataTable.Columns)
                    {
                        if (!multiValueColumns.ContainsKey(col.ColumnName))
                        {
                            newRow[col.ColumnName] = baseRow[col.ColumnName];
                        }
                    }

                    foreach (var kvp in multiValueColumns)
                    {
                        newRow[kvp.Key] = i < kvp.Value.Count ? kvp.Value[i] : string.Empty;
                    }

                    dataTable.Rows.Add(newRow);
                }
            }
        }
    }

    private string ConvertDataTableToHtml(DataTable table, ElasticsearchRequestModel model)
    {
        var hiddenCols = model.TableHeaders.Where(e => e.Visibility == false).ToList();

        foreach (var colName in hiddenCols)
        {
            table.Columns.Remove(colName.Label);
        }

        StringBuilder htmlTable = new StringBuilder();
        htmlTable.Append("<table border='1' cellpadding='10' cellspacing='0' style='border-collapse:collapse;'>");

        htmlTable.Append("<thead><tr>");
        foreach (DataColumn column in table.Columns)
        {
            htmlTable.Append("<th>").Append(column.ColumnName).Append("</th>");
        }

        htmlTable.Append("</tr></thead>");

        htmlTable.Append("<tbody>");
        foreach (DataRow row in table.Rows)
        {
            htmlTable.Append("<tr>");
            foreach (var item in row.ItemArray)
            {
                htmlTable.Append("<td>").Append(item?.ToString()).Append("</td>");
            }

            htmlTable.Append("</tr>");
        }

        htmlTable.Append("</tbody></table>");
        return htmlTable.ToString();
    }

    private async Task<string> ExtractValueFromSource(Dictionary<string, object> source, string fieldPath)
    {
        try
        {
            var baseJsonString = JsonConvert.SerializeObject(source);
            var jsonObj = JObject.Parse(baseJsonString);

            var paths = fieldPath.Split('.');
            JToken? current = jsonObj;

            foreach (var path in paths)
            {
                if (path.EndsWith("[*]"))
                {
                    var propertyName = path.Substring(0, path.IndexOf('['));
                    if (current[propertyName]?.ToString() is string jsonString)
                    {
                        try
                        {
                            var array = JArray.Parse(jsonString);
                            var remainingPath = string.Join(".", paths.SkipWhile(p => p != path).Skip(1));
                            if (string.IsNullOrEmpty(remainingPath))
                            {
                                return string.Join("^", array);
                            }

                            var values = new List<string>();
                            foreach (var item in array)
                            {
                                if (item is JObject obj)
                                {
                                    var val = await ExtractValueFromSource(obj.ToObject<Dictionary<string, object>>(),
                                        remainingPath);
                                    if (!string.IsNullOrEmpty(val))
                                    {
                                        values.Add(val);
                                    }
                                }
                            }

                            return string.Join("^", values);
                        }
                        catch (JsonReaderException)
                        {
                            // Eğer JSON parse edilemezse normal JArray kontrolünü dene
                            if (current[propertyName] is JArray array)
                            {
                                var remainingPath = string.Join(".", paths.SkipWhile(p => p != path).Skip(1));
                                if (string.IsNullOrEmpty(remainingPath))
                                {
                                    return string.Join("^", array);
                                }

                                var values = new List<string>();
                                foreach (var item in array)
                                {
                                    if (item is JObject obj)
                                    {
                                        var val = await ExtractValueFromSource(
                                            obj.ToObject<Dictionary<string, object>>(), remainingPath);
                                        if (!string.IsNullOrEmpty(val))
                                        {
                                            values.Add(val);
                                        }
                                    }
                                }

                                return string.Join("^", values);
                            }

                            return string.Empty;
                        }
                    }
                    else if (current[propertyName] is JArray array)
                    {
                        var remainingPath = string.Join(".", paths.SkipWhile(p => p != path).Skip(1));
                        if (string.IsNullOrEmpty(remainingPath))
                        {
                            return string.Join("^", array);
                        }

                        var values = new List<string>();
                        foreach (var item in array)
                        {
                            if (item is JObject obj)
                            {
                                var val = await ExtractValueFromSource(obj.ToObject<Dictionary<string, object>>(),
                                    remainingPath);
                                if (!string.IsNullOrEmpty(val))
                                {
                                    values.Add(val);
                                }
                            }
                        }

                        return string.Join("^", values);
                    }

                    return string.Empty;
                }

                if (path.Contains("["))
                {
                    var arrayName = path.Substring(0, path.IndexOf('['));
                    var indexStr = path.Substring(path.IndexOf('[') + 1, path.IndexOf(']') - path.IndexOf('[') - 1);

                    if (int.TryParse(indexStr, out var index))
                    {
                        if (current[arrayName]?.ToString() is string jsonString)
                        {
                            try
                            {
                                var array = JArray.Parse(jsonString);
                                current = array.Count > index ? array[index] : null;
                            }
                            catch (JsonReaderException)
                            {
                                if (current[arrayName] is JArray originalArray)
                                {
                                    current = originalArray.Count > index ? originalArray[index] : null;
                                }
                                else
                                {
                                    return string.Empty;
                                }
                            }
                        }
                        else if (current[arrayName] is JArray array)
                        {
                            current = array.Count > index ? array[index] : null;
                        }
                        else
                        {
                            return string.Empty;
                        }
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
                else
                {
                    current = current[path];
                }

                if (current == null)
                {
                    return string.Empty;
                }

                if (paths.Last() == path)
                {
                    return current.ToString();
                }
            }

            return string.Empty;
        }
        catch (JsonReaderException)
        {
            return string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private DataTable ApplyConfigRules(DataTable table, ElasticsearchRequestModel model)
    {
        var resultTable = table.Copy();

        if (model.ExprExists != null)
        {
            foreach (var (column, values) in model.ExprExists)
            {
                foreach (var value in values)
                {
                    var rowsToDelete = !string.IsNullOrEmpty(value)
                        ? resultTable.Select($"{column} NOT LIKE '%{value}%'")
                        : resultTable.Select($"{column} IS NOT NULL AND {column} <> ''");

                    foreach (var row in rowsToDelete)
                    {
                        resultTable.Rows.Remove(row);
                    }
                }
            }
        }

        if (model.ExprNotExists != null)
        {
            foreach (var (column, values) in model.ExprNotExists)
            {
                foreach (var value in values)
                {
                    var rowsToDelete = resultTable.Select($"{column} LIKE '%{value}%'");
                    foreach (var row in rowsToDelete)
                    {
                        resultTable.Rows.Remove(row);
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(model.Func))
        {
            resultTable = model.Func switch
            {
                "Distinct" => resultTable.DefaultView.ToTable(true),
                "Count" => ApplyCountFunction(resultTable, model),
                "Group" => ApplyGroupFunction(resultTable),
                _ => resultTable
            };
        }

        if (!string.IsNullOrWhiteSpace(model.Sort))
        {
            resultTable.DefaultView.Sort = model.Sort;
            resultTable = resultTable.DefaultView.ToTable();
        }

        return resultTable;
    }

    private DataTable ApplyCountFunction(DataTable table, ElasticsearchRequestModel model)
    {
        var result = table.Clone();
        var countColumn = "Count";

        if (!result.Columns.Contains(countColumn))
        {
            result.Columns.Add(countColumn, typeof(int));
        }

        var keyColumns = model.TableHeaders
            .Where(header => !header.Label.Equals("Count", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.Label)
            .ToList();

        var groups = table.AsEnumerable()
            .GroupBy(row => string.Join("|", keyColumns.Select(col => row[col])));

        foreach (var group in groups)
        {
            if (model.MinQty == null || group.Count() >= model.MinQty)
            {
                var row = result.NewRow();
                var values = group.Key.Split('|');

                for (var i = 0; i < keyColumns.Count; i++)
                {
                    row[keyColumns[i]] = values[i];
                }

                row[countColumn] = group.Count();
                result.Rows.Add(row);
            }
        }

        return result;
    }

    private DataTable ApplyGroupFunction(DataTable table)
    {
        var result = table.Clone();
        var groups = table.AsEnumerable()
            .GroupBy(row => string.Join("|", row.ItemArray));

        foreach (var group in groups)
        {
            var values = group.Key.Split('|');
            var row = result.NewRow();

            for (var i = 0; i < table.Columns.Count; i++)
            {
                row[i] = values[i];
            }

            result.Rows.Add(row);
        }

        return result;
    }
}