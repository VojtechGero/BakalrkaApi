using API.Models;
using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.Text.Json;

namespace API.Services;

public class FileService
{
    private readonly string _rootFolderPath = @"./dms";
    private readonly string _apiKey;
    private readonly string _apiUrl;
    public FileService(IOptions<ApiSettings> apiKeyOptions)
    {
        _apiKey = apiKeyOptions.Value.Key;
        _apiUrl = apiKeyOptions.Value.Url;
    }

    public List<FileItem> ListAllTopItems(string path)
    {
        List<FileItem> list = new List<FileItem>();
        var filePaths = Directory.EnumerateFiles(path, "*.json");
        var directoryPaths = Directory.EnumerateDirectories(path);
        string rootParent = GetParentFolder(_rootFolderPath);
        if (Path.GetFullPath(_rootFolderPath) != Path.GetFullPath(path))
        {
            var parent = GetParentFolder(path);
            var root = new FileItem()
            {
                IsDirectory = true,
                Name = $"Zpět na {Path.GetFileName(parent)}",
                Path = Path.GetRelativePath(rootParent, parent),
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
                Path = Path.GetRelativePath(rootParent, directory),
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
                Path = Path.ChangeExtension(Path.GetRelativePath(rootParent, file), ".pdf"),
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

    public async Task<Pdf> GetPdfOcr(string path, int targetHeight, int targetWidth)
    {
        Pdf pdf;
        path = Path.GetFullPath(path);
        string jsonPath = Path.ChangeExtension(path, ".json");

        if (File.Exists(jsonPath))
        {
            string jsonContent = await File.ReadAllTextAsync(jsonPath);
            pdf = JsonSerializer.Deserialize<Pdf>(jsonContent);
            return pdf;
        }

        List<OcrPage> pages = new();

        using (FileStream fileStream = new FileStream(path, FileMode.Open))
        {
            var key = _apiKey; // Ensure this is populated from configuration
            var client = new DocumentIntelligenceClient(new Uri(_apiUrl), new AzureKeyCredential(key));

            Operation<AnalyzeResult> operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                "prebuilt-layout",
                BinaryData.FromStream(fileStream));

            AnalyzeResult result = operation.Value;

            foreach (DocumentPage page in result.Pages)
            {
                float originalWidthInches = (float)page.Width;
                float originalHeightInches = (float)page.Height;
                float scaleX = targetWidth / originalWidthInches * 2;
                float scaleY = targetHeight / originalHeightInches * 2;

                List<OcrBox> ocrTexts = new List<OcrBox>();
                for (int i = 0; i < page.Lines.Count; i++)
                {
                    DocumentLine line = page.Lines[i];
                    var boundingRectangle = GetBoundingRectangle(line.Polygon.ToList(), scaleX, scaleY);
                    ocrTexts.Add(new OcrBox()
                    {
                        Text = line.Content,
                        X = boundingRectangle.X,
                        Y = boundingRectangle.Y,
                        Width = boundingRectangle.Width,
                        Height = boundingRectangle.Height
                    });
                }
                pages.Add(new OcrPage()
                {
                    OcrBoxes = ocrTexts,
                    pageNum = page.PageNumber,
                });
            }
        }

        pdf = new Pdf()
        {
            Path = path,
            Pages = pages,
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(pdf));
        return pdf;
    }

    private static Rectangle GetBoundingRectangle(List<float> polygon, float scaleX, float scaleY)
    {
        if (polygon.Count % 2 != 0)
        {
            throw new ArgumentException("Invalid polygon data. Points should be in pairs.");
        }

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        for (int i = 0; i < polygon.Count; i += 2)
        {
            float xInches = polygon[i];
            float yInches = polygon[i + 1];
            float xPixels = xInches * scaleX;
            float yPixels = yInches * scaleY;

            if (xPixels < minX) minX = xPixels;
            if (yPixels < minY) minY = yPixels;
            if (xPixels > maxX) maxX = xPixels;
            if (yPixels > maxY) maxY = yPixels;
        }

        return new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
    }
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
    public async Task UploadFileAsync(IFormFile file, string path)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Invalid file");
        }

        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new ArgumentException("Invalid path");
        }

        var fileName = file.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Filename is required");
        }

        try
        {
            Directory.CreateDirectory(path);
            var filePath = Path.Combine(path, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

        }
        catch (Exception ex)
        {
            throw;
        }
    }
    public void CopyItem(string sourcePath, string destinationRoot)
    {
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            throw new FileNotFoundException("The source path does not exist.", sourcePath);
        }

        bool isDirectory = Directory.Exists(sourcePath);

        string targetBasePath = isDirectory
            ? Path.Combine(destinationRoot, Path.GetFileName(sourcePath))
            : destinationRoot;

        Directory.CreateDirectory(targetBasePath);

        if (isDirectory)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourcePath, dirPath);
                Directory.CreateDirectory(Path.Combine(targetBasePath, relativePath));
            }

            foreach (string filePath in Directory.GetFiles(sourcePath, "*.pdf", SearchOption.AllDirectories))
            {
                ProcessFile(filePath, sourcePath, targetBasePath);
            }
        }
        else
        {
            ProcessFile(sourcePath, Path.GetDirectoryName(sourcePath), targetBasePath);
        }
    }

    private void ProcessFile(string sourceFilePath, string sourceRoot, string targetRoot)
    {
        string relativePath = Path.GetRelativePath(sourceRoot, sourceFilePath);
        string targetFilePath = Path.Combine(targetRoot, relativePath);

        targetFilePath = GetUniqueFileName(Path.GetDirectoryName(targetFilePath), Path.GetFileName(targetFilePath));

        File.Copy(sourceFilePath, targetFilePath);

        string sourceJsonPath = Path.ChangeExtension(sourceFilePath, ".json");
        string targetJsonPath = Path.ChangeExtension(targetFilePath, ".json");

        if (File.Exists(sourceJsonPath))
        {
            File.Copy(sourceJsonPath, targetJsonPath);

            var jsonContent = JsonSerializer.Deserialize<Pdf>(File.ReadAllText(targetJsonPath));
            jsonContent.Path = targetFilePath;
            File.WriteAllText(targetJsonPath, JsonSerializer.Serialize(jsonContent));
        }
    }



    private string GetUniqueFileName(string directory, string fileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        int counter = 1;

        while (true)
        {
            string newFileName = $"{baseName}{GetCopySuffix(counter)}{extension}";
            string fullPath = Path.Combine(directory, newFileName);

            if (!File.Exists(fullPath))
            {
                return fullPath;
            }

            counter++;
        }
    }

    private string GetCopySuffix(int counter)
    {
        return counter switch
        {
            1 => " - copy",
            _ => $" - copy ({counter - 1})"
        };
    }
    public void MoveItem(string sourcePath, string destinationRoot)
    {
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            throw new FileNotFoundException("Source path not found", sourcePath);

        bool isDirectory = Directory.Exists(sourcePath);
        string originalSource = sourcePath; // Store original path for cleanup
        string targetBasePath = isDirectory
            ? GetUniqueDirectoryName(destinationRoot, Path.GetFileName(sourcePath))
            : destinationRoot;

        Directory.CreateDirectory(targetBasePath);

        if (isDirectory)
        {
            foreach (string filePath in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                MoveFileSystemEntry(filePath, sourcePath, targetBasePath);
            }

            DeleteEntireSourceStructure(originalSource);
        }
        else
        {
            MoveFileSystemEntry(sourcePath, Path.GetDirectoryName(sourcePath), targetBasePath);
            DeleteEmptyDirectory(Path.GetDirectoryName(sourcePath));
        }
    }

    private void DeleteEntireSourceStructure(string sourcePath)
    {
        try
        {
            Directory.Delete(sourcePath, true);
        }
        catch (IOException)
        {
            DeleteDirectoryRecursive(sourcePath);
        }
    }

    private void DeleteDirectoryRecursive(string path)
    {
        foreach (string directory in Directory.GetDirectories(path))
        {
            DeleteDirectoryRecursive(directory);
        }

        foreach (string file in Directory.GetFiles(path))
        {
            File.Delete(file);
        }

        Directory.Delete(path);
    }

    private void MoveFileSystemEntry(string sourcePath, string sourceRoot, string targetRoot)
    {
        string relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
        string targetPath = Path.Combine(targetRoot, relativePath);

        if (File.Exists(sourcePath))
        {
            string targetDir = Path.GetDirectoryName(targetPath);
            Directory.CreateDirectory(targetDir);

            string uniquePath = GetUniqueFileName(targetDir, Path.GetFileName(targetPath));
            File.Move(sourcePath, uniquePath);

            if (IsPdfFile(sourcePath))
            {
                MoveAssociatedJson(sourcePath, uniquePath);
            }
        }
    }

    private bool IsPdfFile(string path) =>
        Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private void MoveAssociatedJson(string pdfSourcePath, string newPdfPath)
    {
        string sourceJson = Path.ChangeExtension(pdfSourcePath, ".json");
        string targetJson = Path.ChangeExtension(newPdfPath, ".json");

        if (File.Exists(sourceJson))
        {
            File.Move(sourceJson, targetJson);
            UpdateJsonPath(targetJson, newPdfPath);
        }
    }

    private void DeleteEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) &&
                !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
        catch { /* Ignore deletion errors */ }
    }

    private void UpdateJsonPath(string jsonPath, string newPdfPath)
    {
        Pdf jsonContent = JsonSerializer.Deserialize<Pdf>(File.ReadAllText(jsonPath));
        jsonContent.Path = newPdfPath;
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(jsonContent));
    }

    private string GetUniqueDirectoryName(string parentDir, string dirName)
    {
        string targetDir = Path.Combine(parentDir, dirName);
        int count = 1;

        while (Directory.Exists(targetDir))
        {
            targetDir = Path.Combine(parentDir, $"{dirName} ({count++})");
        }
        return targetDir;
    }

    private void DeleteEmptyDirectories(string startDir)
    {
        foreach (string directory in Directory.GetDirectories(startDir))
        {
            DeleteEmptyDirectories(directory);
            if (Directory.GetFiles(directory).Length == 0 &&
                Directory.GetDirectories(directory).Length == 0)
            {
                Directory.Delete(directory);
            }
        }
    }


    public void DeleteItem(string path)
    {
        FileAttributes attr = File.GetAttributes(path);
        bool isDirectory = (attr & FileAttributes.Directory) == FileAttributes.Directory;

        try
        {
            if (isDirectory)
            {
                // Handle directory deletion
                ClearDirectoryAttributes(path);
                Directory.Delete(path, true);
            }
            else
            {
                // Handle file deletion
                string jsonPath = Path.ChangeExtension(path, ".json");

                // Delete JSON first
                if (File.Exists(jsonPath))
                {
                    ClearFileAttributes(jsonPath);
                    File.Delete(jsonPath);
                }

                // Delete PDF
                ClearFileAttributes(path);
                File.Delete(path);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Access denied: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"File in use: {ex.Message}", ex);
        }
    }

    private void ClearFileAttributes(string filePath)
    {
        if (!File.Exists(filePath)) return;

        FileAttributes attributes = File.GetAttributes(filePath);
        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
        {
            File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
        }
    }

    private void ClearDirectoryAttributes(string dirPath)
    {
        // Clear attributes from directory itself
        FileAttributes dirAttr = File.GetAttributes(dirPath);
        if ((dirAttr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
        {
            File.SetAttributes(dirPath, dirAttr & ~FileAttributes.ReadOnly);
        }

        // Clear attributes from all contained files
        foreach (string file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
        {
            ClearFileAttributes(file);
        }
    }

    public void CreateFolder(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
    }
    public void RenameItem(string originalPath, string newName)
    {
        FileAttributes attr = File.GetAttributes(originalPath);
        bool isDirectory = attr.HasFlag(FileAttributes.Directory);

        if (!isDirectory)
        {
            // Handle file rename
            string jsonPath = Path.ChangeExtension(originalPath, ".json");
            string directory = Path.GetDirectoryName(originalPath);
            string newPath = Path.Combine(directory, newName);

            // Ensure .pdf extension
            if (!newPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                newPath += ".pdf";

            string newJsonPath = Path.ChangeExtension(newPath, ".json");

            File.Move(originalPath, newPath);
            File.Move(jsonPath, newJsonPath);

            UpdateJsonPath(newJsonPath, newPath);
        }
        else
        {
            // Handle directory rename
            string parentDir = Directory.GetParent(originalPath).FullName;
            string newPath = Path.Combine(parentDir, newName);

            if (Directory.Exists(newPath))
                throw new IOException("Target directory already exists");

            Directory.Move(originalPath, newPath);

            // Update all JSON files in the directory hierarchy
            UpdateJsonPathsInDirectory(newPath);
        }
    }

    private void UpdateJsonPathsInDirectory(string directoryPath)
    {
        foreach (string jsonFile in Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories))
        {
            string pdfPath = Path.ChangeExtension(jsonFile, ".pdf");
            if (File.Exists(pdfPath))
            {
                UpdateJsonPath(jsonFile, pdfPath);
            }
        }
    }

}
