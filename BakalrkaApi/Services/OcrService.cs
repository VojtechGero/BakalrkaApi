using API.Models;
using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.Text.Json;

namespace BakalrkaApi.Services;

public class OcrService
{
    private readonly DocumentIntelligenceClient _documentClient;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private const float ScaleFactor = 2f;

    public OcrService(IOptions<ApiSettings> apiKeyOptions)
    {
        var settings = apiKeyOptions.Value;
        _documentClient = new DocumentIntelligenceClient(
            new Uri(settings.Url),
            new AzureKeyCredential(settings.Key));
    }

    public async Task<Pdf> GetPdfOcr(string path, int targetHeight, int targetWidth)
    {
        var jsonPath = Path.ChangeExtension(path, ".json");
        if (TryGetCachedPdf(jsonPath, out var cachedPdf))
            return cachedPdf;

        using var fileStream = new FileStream(path, FileMode.Open);
        var result = await AnalyzeDocumentAsync(fileStream);

        var pdf = MapAnalyzeResultToPdf(result, path, targetHeight, targetWidth);
        await CachePdfResultAsync(jsonPath, pdf);

        return pdf;
    }

    private bool TryGetCachedPdf(string jsonPath, out Pdf pdf)
    {
        pdf = null;
        if (!File.Exists(jsonPath)) return false;

        var jsonContent = File.ReadAllText(jsonPath);
        pdf = JsonSerializer.Deserialize<Pdf>(jsonContent);
        return true;
    }

    private async Task<AnalyzeResult> AnalyzeDocumentAsync(FileStream fileStream)
    {
        var operation = await _documentClient.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-layout",
            BinaryData.FromStream(fileStream));

        return operation.Value;
    }

    private Pdf MapAnalyzeResultToPdf(AnalyzeResult result, string path, int targetHeight, int targetWidth)
    {
        return new Pdf
        {
            Path = path,
            Pages = result.Pages.Select(page =>
                ConvertToOcrPage(page, targetHeight, targetWidth)).ToList()
        };
    }

    private OcrPage ConvertToOcrPage(DocumentPage page, int targetHeight, int targetWidth)
    {
        var (scaleX, scaleY) = CalculateScalingFactors(page, targetHeight, targetWidth);

        return new OcrPage
        {
            pageNum = page.PageNumber,
            OcrBoxes = page.Lines.Select(line =>
                ConvertToOcrBox(line, scaleX, scaleY)).ToList()
        };
    }

    private (float scaleX, float scaleY) CalculateScalingFactors(DocumentPage page, int targetHeight, int targetWidth)
    {
        var originalWidth = (float)page.Width;
        var originalHeight = (float)page.Height;

        return (
            targetWidth / originalWidth * ScaleFactor,
            targetHeight / originalHeight * ScaleFactor
        );
    }

    private static OcrBox ConvertToOcrBox(DocumentLine line, float scaleX, float scaleY)
    {
        var rect = GetBoundingRectangle(line.Polygon, scaleX, scaleY);
        return new OcrBox
        {
            Text = line.Content,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height
        };
    }

    private static Rectangle GetBoundingRectangle(IReadOnlyList<float> polygon, float scaleX, float scaleY)
    {
        if (polygon.Count % 2 != 0)
            throw new ArgumentException("Invalid polygon data. Points should be in pairs.");

        var scaledPoints = polygon.Select((p, i) =>
            i % 2 == 0 ? p * scaleX : p * scaleY).ToList();

        var minX = scaledPoints.Where((_, i) => i % 2 == 0).Min();
        var minY = scaledPoints.Where((_, i) => i % 2 == 1).Min();
        var maxX = scaledPoints.Where((_, i) => i % 2 == 0).Max();
        var maxY = scaledPoints.Where((_, i) => i % 2 == 1).Max();

        return new Rectangle(
            (int)minX,
            (int)minY,
            (int)(maxX - minX),
            (int)(maxY - minY));
    }

    private async Task CachePdfResultAsync(string jsonPath, Pdf pdf)
    {
        await using var jsonStream = File.Create(jsonPath);
        await JsonSerializer.SerializeAsync(jsonStream, pdf, _jsonOptions);
    }
}