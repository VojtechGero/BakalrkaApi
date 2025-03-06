namespace API.Models;

public class FileItem
{
    public string Name { get; set; }
    public string Path { get; set; }
    public bool IsDirectory { get; set; }
    public List<FileItem>? SubItems { get; set; }
}
