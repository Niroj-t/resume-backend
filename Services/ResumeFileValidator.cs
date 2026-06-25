namespace ResumeAnalyzer.Api.Services;

public interface IFileValidator
{
    /// <summary>Throws FileValidationException if the file is invalid.</summary>
    void Validate(IFormFile? file);
}

public class ResumeFileValidator : IFileValidator
{
    private static readonly string[] AllowedExtensions = { ".pdf", ".docx" };

    private static readonly string[] AllowedContentTypes =
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public void Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            throw new FileValidationException("A resume file is required.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new FileValidationException("Resume file must be 5 MB or smaller.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            throw new FileValidationException("Only PDF and DOCX files are supported.");
        }

        // Belt-and-suspenders: also check the declared content type, since
        // extension alone can be spoofed. We don't hard-fail purely on a
        // missing/odd content-type header (browsers are inconsistent here),
        // but we do reject known-bad types.
        if (!string.IsNullOrEmpty(file.ContentType) &&
            !AllowedContentTypes.Contains(file.ContentType) &&
            file.ContentType != "application/octet-stream")
        {
            throw new FileValidationException("Only PDF and DOCX files are supported.");
        }
    }
}
