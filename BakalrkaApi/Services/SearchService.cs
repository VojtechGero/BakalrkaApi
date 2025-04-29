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
        var results = new List<SearchResult>();
        var jsonFiles = Directory.EnumerateFiles(_rootFolderPath, "*.json", SearchOption.AllDirectories);

        foreach (var file in jsonFiles)
        {
            var jsonContent = await File.ReadAllTextAsync(file);
            query = query.Trim().ToLower();
            var pdf = JsonSerializer.Deserialize<Pdf>(jsonContent);
            string rootParent = GetParentFolder(_rootFolderPath);

            if (pdf == null) continue;

            int totalBoxes = 0;
            foreach (var page in pdf.Pages)
            {
                var boxTexts = page.OcrBoxes.Select(b => b.Text ?? string.Empty).ToList();
                string combinedText = string.Join(" ", boxTexts);
                int matchIndex = combinedText.IndexOf(query, StringComparison.OrdinalIgnoreCase);

                for (int i = 0; i < page.OcrBoxes.Count; i++)
                {
                    if (page.OcrBoxes[i].Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new SearchResult
                        {
                            FilePath = Path.ChangeExtension(Path.GetRelativePath(rootParent, file), ".pdf"),
                            PageNumber = page.pageNum,
                            MatchedText = combinedText.Substring(matchIndex, query.Length),
                            MatchIndex = matchIndex,
                            BoxIndex = totalBoxes,
                        });
                    }
                    totalBoxes++;


                }
            }
        }
        var fileResults = results
        .GroupBy(sr => sr.FilePath)
        .Select(group => new FileResults
        {
            Query = query,
            FilePath = group.Key,
            OccurrenceCount = group.Count()
        })
        .ToList();
        return fileResults;
    }
    public async Task<List<SearchResult>> SearchFileAsync(string query, string filename)
    {
        var results = new List<SearchResult>();
        var file = Path.ChangeExtension(filename, ".json");
        var jsonContent = await File.ReadAllTextAsync(file);
        query = query.Trim().ToLower();
        var pdf = JsonSerializer.Deserialize<Pdf>(jsonContent);


        int totalBoxes = 0;
        foreach (var page in pdf.Pages)
        {
            int matchIndex = 0;
            var boxTexts = page.OcrBoxes.Select(b => b.Text ?? string.Empty).ToList();
            string combinedText = string.Join(" ", boxTexts);

            for (int i = 0; i < page.OcrBoxes.Count; i++)
            {
                if (page.OcrBoxes[i].Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResult
                    {
                        FilePath = Path.ChangeExtension(Path.GetFullPath(file), ".pdf"),
                        PageNumber = page.pageNum,
                        MatchedText = page.OcrBoxes[i].Text,
                        MatchIndex = matchIndex,
                        BoxIndex = totalBoxes,
                    });
                }
                totalBoxes++;
                matchIndex += page.OcrBoxes[i].Text.Length + 1;

            }
        }
        return results;
    }
    private string GetParentFolder(string filePath)
    {
        DirectoryInfo parentDirectory = Directory.GetParent(filePath);
        return parentDirectory.FullName;
    }
}