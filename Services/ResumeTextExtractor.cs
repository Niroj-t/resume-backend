using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace ResumeAnalyzer.Api.Services;

public interface IResumeTextExtractor
{
    /// <summary>
    /// Extracts plain text from an uploaded resume file (PDF or DOCX).
    /// Returns an empty string (never throws) if extraction fails or the
    /// file type isn't supported — callers should treat that as "no text
    /// available" rather than a hard error.
    /// </summary>
    Task<string> ExtractTextAsync(IFormFile file);
}

/// <summary>
/// Simple, dependency-light text extraction:
///  - PDF via PdfPig (pure managed code, no native dependencies)
///  - DOCX via DocumentFormat.OpenXml (Microsoft's official OOXML SDK)
///
/// This is intentionally basic — good enough for keyword matching, not a
/// full resume parser. Swap this out for a more advanced library (or an
/// external parsing service) later if you need structured section parsing.
/// </summary>
public class ResumeTextExtractor : IResumeTextExtractor
{
    private readonly ILogger<ResumeTextExtractor> _logger;

    public ResumeTextExtractor(ILogger<ResumeTextExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractTextAsync(IFormFile file)
    {
        try
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            await using var stream = file.OpenReadStream();

            return extension switch
            {
                ".pdf" => ExtractFromPdf(stream),
                ".docx" => ExtractFromDocx(stream),
                _ => string.Empty,
            };
        }
        catch (Exception ex)
        {
            // Text extraction is a "best effort" step — a malformed file
            // shouldn't take down the whole analysis request.
            _logger.LogWarning(ex, "Failed to extract text from {FileName}", file.FileName);
            return string.Empty;
        }
    }

    private static string ExtractFromPdf(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var sb = new System.Text.StringBuilder();

        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }

    private static string ExtractFromDocx(Stream stream)
    {
        using var wordDocument = WordprocessingDocument.Open(stream, false);
        var body = wordDocument.MainDocumentPart?.Document?.Body;

        if (body is null)
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder();

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            sb.AppendLine(paragraph.InnerText);
        }

        return sb.ToString();
    }
}
