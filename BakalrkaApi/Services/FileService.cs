using API.Models;

namespace API.Services;

public class FileService
{
    private readonly string _rootFolderPath = @"./dms";
    public FileService()
    {

    }

    public List<FileItem> ListAllTopItems(string path)
    {
        List<FileItem> list = new List<FileItem>();
        var filePaths = Directory.EnumerateFiles(path, "*.json");
        var directoryPaths = Directory.EnumerateDirectories(path);
        if (Path.GetFullPath(_rootFolderPath) != Path.GetFullPath(path))
        {
            var parent = GetParentFolder(path);
            var root = new FileItem()
            {
                IsDirectory = true,
                Path = parent,
                Name = $"Zpět na {Path.GetFileName(parent)}",
                SubItems = null
            };
            list.Add(root);
        }

        foreach (var directory in directoryPaths)
        {
            var item = new FileItem()
            {
                IsDirectory = true,
                Name = Path.GetFileName(directory),
                Path = directory,
                SubItems = null
            };
            list.Add(item);
        }
        foreach (var file in filePaths)
        {
            var item = new FileItem()
            {
                IsDirectory = false,
                Name = Path.ChangeExtension(Path.GetFileName(file), ".pdf"),
                Path = Path.ChangeExtension(file, ".pdf"),
                SubItems = null
            };
            list.Add(item);
        }
        return list;
    }
    private string GetParentFolder(string filePath)
    {
        DirectoryInfo parentDirectory = Directory.GetParent(filePath);
        return parentDirectory.FullName;
    }

    /*
    public FileContentResult? GetFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            return File(filePath, "application/pdf", "test.pdf");
        }
    }
    */

    public FileItem ListAllItems()
    {
        if (!Directory.Exists(Path.GetFullPath(_rootFolderPath)))
        {
            Directory.CreateDirectory(Path.GetFullPath(_rootFolderPath));
        }
        var item = GetFolderStructure(_rootFolderPath);
        return item;
    }
    private FileItem GetFolderStructure(string path)
    {
        var folderName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(folderName))
        {
            folderName = path;
        }

        var folder = new FileItem
        {
            Name = folderName,
            Path = path,
            IsDirectory = true,
            SubItems = new List<FileItem>()
        };

        foreach (var dir in Directory.GetDirectories(path))
        {
            folder.SubItems.Add(GetFolderStructure(dir));
        }

        foreach (var file in Directory.GetFiles(path)
                     .Where(x => x.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            folder.SubItems.Add(new FileItem
            {
                Name = Path.GetFileName(file),
                Path = file,
                IsDirectory = false,
                SubItems = null
            });
        }

        return folder;
    }
}
