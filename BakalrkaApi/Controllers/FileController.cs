using API.Models;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class FileController : ControllerBase
{
    private readonly ILogger<SearchController> _logger;
    private readonly FileService _fileService;
    public FileController(ILogger<SearchController> logger, FileService fileService)
    {
        _logger = logger;
        _fileService = fileService;
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
        try
        {
            await _fileService.UploadFileAsync(file, path);
            return Ok(new { Message = "File uploaded successfully", FileName = file.FileName });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            return StatusCode(500, "Internal server error");
        }
    }
    [HttpGet("ocr")]
    public async Task<Pdf> GetOcr(string path, int height, int width)
    {
        var pdf = await _fileService.GetPdfOcr(path, height, width);
        return pdf;
    }

    [HttpGet("copy")]
    public ActionResult CopyFile(string selectedItem, string destination)
    {
        _fileService.CopyItem(selectedItem, destination);
        return Ok();
    }
    [HttpPut("move")]
    public ActionResult MoveFile(string selectedItem, string destination)
    {
        _fileService.MoveItem(selectedItem, destination);
        return Ok();
    }

    [HttpDelete("delete")]
    public ActionResult DeleteFile(string selectedItem)
    {
        _fileService.DeleteItem(selectedItem);
        return Ok();
    }

    [HttpPost("create-folder")]
    public ActionResult CreateFolder(string directoryPath)
    {
        _fileService.CreateFolder(directoryPath);
        return Ok();
    }
}
