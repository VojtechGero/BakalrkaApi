using API.Models;
using System.Text.Json;

namespace API.Services;
public class SearchService
{
    private readonly string _rootFolderPath = @"./dms";

    public SearchService()
    {
    }

    public async Task<List<FileResults>> SearchAsync(string query)
    {
        query = NormalizeQuery(query);
        var jsonFiles = Directory.EnumerateFiles(_rootFolderPath, "*.json", SearchOption.AllDirectories);
        var results = new List<SearchResult>();

        foreach (var file in jsonFiles)
        {
            results.AddRange(await ProcessFileAsync(file, query));
        }

        return GroupResults(results, query);
    }
    private async Task<List<SearchResult>> ProcessFileAsync(string filePath, string query)
    {
        var results = new List<SearchResult>();
        var jsonContent = await File.ReadAllTextAsync(filePath);
        var pdf = JsonSerializer.Deserialize<Pdf>(jsonContent);

        if (pdf == null) return results;

        var relativePdfPath = GetRelativePdfPath(filePath);
        int totalBoxes = 0;

        foreach (var page in pdf.Pages)
        {
            int currentIndex = 0;
            foreach (var box in page.OcrBoxes)
            {
                if (ContainsQuery(box.Text, query))
                {
                    var result = new SearchResult()
                    {
                        FilePath = relativePdfPath,
                        PageNumber = page.pageNum,
                        MatchedText = box.Text,
                        MatchIndex = currentIndex,
                        BoxIndex = totalBoxes,
                    };
                    results.Add(result);
                }
                currentIndex += box.Text.Length + 1;
                totalBoxes++;
            }
        }

        return results;
    }
    private static List<FileResults> GroupResults(List<SearchResult> results, string query)
    {
        return results
            .GroupBy(r => r.FilePath)
            .Select(g => new FileResults
            {
                Query = query,
                FilePath = g.Key,
                OccurrenceCount = g.Count()
            })
            .ToList();
    }
    public async Task<List<SearchResult>> SearchFileAsync(string query, string filename)
    {
        query = NormalizeQuery(query);
        var jsonFile = Path.Combine(".", Path.ChangeExtension(filename, ".json"));

        return File.Exists(jsonFile)
            ? await ProcessFileAsync(jsonFile, query)
            : new List<SearchResult>();
    }

    private string GetRelativePdfPath(string jsonFilePath)
    {
        var rootParent = Directory.GetParent(_rootFolderPath)?.FullName ?? _rootFolderPath;
        return Path.ChangeExtension(Path.GetRelativePath(rootParent, jsonFilePath), ".pdf");
    }

    private static string NormalizeQuery(string query) => query.Trim().ToLower();

    private static bool ContainsQuery(string text, string query) =>
        text.Contains(query, StringComparison.OrdinalIgnoreCase);
}