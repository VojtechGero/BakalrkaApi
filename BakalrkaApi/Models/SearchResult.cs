namespace API.Models;

public class SearchResult
{
    public string Query { get; set; }
    public string FilePath { get; set; }
    public int PageNumber { get; set; }
    public string MatchedText { get; set; }
    public int MatchIndex { get; set; }
    public int BoxIndex { get; set; }
}
