using System.Drawing;

namespace API.Models;

public class OcrBox
{
    public string Text { get; set; }
    public Rectangle Rectangle { get; set; }
}