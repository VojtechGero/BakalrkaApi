using API.Models;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class FileController : ControllerBase
{
    private readonly ILogger<SearchController> _logger;
    private readonly FileService _fileService;
    private readonly string _apiKey;
    public FileController(ILogger<SearchController> logger, IOptions<ApiKeySettings> apiKeyOptions)
    {
        _apiKey = apiKeyOptions.Value.Key;
        // Ensure key is not empty
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new ArgumentException("API Key is missing");
        }
        _logger = logger;
        _fileService = new FileService(_apiKey);
    }
    [HttpGet("list")]
    public List<FileItem> GetTopLevel(string path)
    {
        var FileItems = _fileService.ListAllTopItems(path);
        return FileItems;
    }
    [HttpGet("structure")]
    public FileItem GetStructure()
    {
        var FileItems = _fileService.ListAllItems();
        return FileItems;
    }
    [HttpGet("file")]
    public ActionResult GetFile(string path)
    {
        byte[] fileBytes = System.IO.File.ReadAllBytes(path);

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(path, out string contentType))
        {
            contentType = "application/octet-stream";
        }

        string fileName = Path.GetFileName(path);

        return File(fileBytes, contentType, fileName);
    }
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string path)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded or file is empty.");
        }

        try
        {
            Directory.CreateDirectory(path);

            var filePath = Path.Combine(path, file.FileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation($"File uploaded successfully: {filePath}");
            return Ok(new { Message = "File uploaded successfully", FileName = file.FileName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while uploading the file.");
            return StatusCode(500, "Internal server error while uploading the file.");
        }
    }
    [HttpGet("ocr")]
    public async Task<Pdf> GetOcr(string path, int height, int width)
    {
        var pdf = await _fileService.GetPdfOcr(path, height, width);
        return pdf;
    }
}
