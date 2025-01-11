using ElasticsearchMedium.Core.Interfaces;
using ElasticsearchMedium.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace ElasticsearchMedium.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ElasticsearchController(IElasticsearchService elasticsearchService) : ControllerBase
{
    [HttpPost("process-logs")]
    public async Task<IActionResult> ProcessLogs([FromBody] ElasticsearchRequestModel config)
    {
        try
        {
            var result = await elasticsearchService.ProcessLogs(config);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    
    [HttpGet("saved-query/{queryId}")]
    public async Task<IActionResult> GetSavedQuery(string queryId)
    {
        try
        {
            var query = await elasticsearchService.GetSavedQueryDefinition(queryId);
            if (query == null)
                return NotFound();
            
            return Ok(new { query });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}