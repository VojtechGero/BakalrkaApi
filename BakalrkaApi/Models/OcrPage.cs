namespace API.Models;

public class OcrPage
{
    public int pageNum { get; set; }
    public List<OcrBox> OcrBoxes { get; set; }
}
