using API.Models;
using System.Text.Json;

namespace API.Services;
public class SearchService
{
    private readonly string _folderPath = @"./dms";

    public SearchService()
    {
    }

    public async Task<List<SearchResult>> SearchAsync(string query)
    {
        var results = new List<SearchResult>();
        var jsonFiles = Directory.EnumerateFiles(_folderPath, "*.json", SearchOption.AllDirectories);

        foreach (var file in jsonFiles)
        {
            var jsonContent = await File.ReadAllTextAsync(file);
            query = query.Trim().ToLower();
            var pdf = JsonSerializer.Deserialize<Pdf>(jsonContent);

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
                            FilePath = Path.ChangeExtension(Path.GetFullPath(file), ".pdf"),
                            PageNumber = page.pageNum,
                            MatchedText = combinedText.Substring(matchIndex, query.Length),
                            MatchIndex = matchIndex,
                            BoxIndex = totalBoxes,
                            BoxSpan = 1
                        });
                    }
                    totalBoxes++;


                }
            }
        }
        return results;
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
            var boxTexts = page.OcrBoxes.Select(b => b.Text ?? string.Empty).ToList();
            string combinedText = string.Join(" ", boxTexts);
            int matchIndex = combinedText.IndexOf(query, StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < page.OcrBoxes.Count; i++)
            {
                if (page.OcrBoxes[i].Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResult
                    {
                        FilePath = Path.ChangeExtension(Path.GetFullPath(file), ".pdf"),
                        PageNumber = page.pageNum,
                        MatchedText = combinedText.Substring(matchIndex, query.Length),
                        MatchIndex = matchIndex,
                        BoxIndex = totalBoxes,
                        BoxSpan = 1
                    });
                }
                totalBoxes++;


            }
        }
        return results;
    }
}