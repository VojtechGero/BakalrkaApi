namespace API.Models;

public class Pdf
{
    public string Path { get; set; }
    public List<OcrPage> Pages { get; set; }
}

