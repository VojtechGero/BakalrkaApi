using API.Models;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using FileResults = API.Models.FileResults;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class SearchController : ControllerBase
{

    private readonly ILogger<SearchController> _logger;
    private SearchService _searchService;

    public SearchController(ILogger<SearchController> logger)
    {
        _logger = logger;
        _searchService = new SearchService();
    }

    [HttpGet("results")]
    public async Task<IEnumerable<FileResults>> GetResults(string query)
    {
        var results = await _searchService.SearchAsync(query);
        return results;
    }
    [HttpGet("result")]
    public async Task<List<SearchResult>> GetResult(string query, string fileName)
    {
        var result = await _searchService.SearchFileAsync(query, fileName);
        return result;
    }
}

