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
    public FileService(IOptions<ApiKeySettings> apiKeyOptions)
    {
        _apiKey = apiKeyOptions.Value.Key;

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
        string rootparent = GetParentFolder(_rootFolderPath);
        foreach (var directory in directoryPaths)
        {
            var item = new FileItem()
            {
                IsDirectory = true,
                Name = Path.GetFileName(directory),
                Path = Path.GetRelativePath(rootparent, directory),
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
                Path = Path.ChangeExtension(Path.GetRelativePath(rootparent, file), ".pdf"),
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

        string AiPath = @"https://bakalarkaai.cognitiveservices.azure.com/";
        List<OcrPage> pages = new();

        using (FileStream fileStream = new FileStream(path, FileMode.Open))
        {
            var key = _apiKey; // Ensure this is populated from configuration
            var client = new DocumentIntelligenceClient(new Uri(AiPath), new AzureKeyCredential(key));

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
                    ocrTexts.Add(new OcrBox()
                    {
                        Text = line.Content,
                        Rectangle = GetBoundingRectangle(line.Polygon.ToList(), scaleX, scaleY)
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
    public void CopyItem(string selectedPdfPath, string destination)
    {
        string jsonPath = Path.ChangeExtension(selectedPdfPath, ".json");

        Directory.CreateDirectory(destination);

        string pdfFileName = Path.GetFileName(selectedPdfPath);
        string destPdfPath = Path.Combine(destination, pdfFileName);
        File.Copy(selectedPdfPath, destPdfPath, overwrite: true);

        string jsonFileName = Path.GetFileName(jsonPath);
        string destJsonPath = Path.Combine(destination, jsonFileName);
        File.Copy(jsonPath, destJsonPath, overwrite: true);
    }
    public void MoveItem(string selectedPdfPath, string destination)
    {
        string jsonPath = Path.ChangeExtension(selectedPdfPath, ".json");

        Directory.CreateDirectory(destination);

        string pdfFileName = Path.GetFileName(selectedPdfPath);
        string destPdfPath = Path.Combine(destination, pdfFileName);
        if (File.Exists(destPdfPath)) File.Delete(destPdfPath);
        File.Move(selectedPdfPath, destPdfPath);

        string jsonFileName = Path.GetFileName(jsonPath);
        string destJsonPath = Path.Combine(destination, jsonFileName);
        if (File.Exists(destJsonPath)) File.Delete(destJsonPath);
        File.Move(jsonPath, destJsonPath);
    }

    public void DeleteItem(string selectedPdfPath)
    {
        string jsonPath = Path.ChangeExtension(selectedPdfPath, ".json");

        File.Delete(selectedPdfPath);
        File.Delete(jsonPath);
    }

    public void CreateFolder(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
    }
}
