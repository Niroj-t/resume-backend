namespace ResumeAnalyzer.Api.Services;

/// <summary>
/// Thrown when an uploaded file fails validation (type, size, etc).
/// Caught by the controller and translated into a 400 response.
/// </summary>
public class FileValidationException : Exception
{
    public FileValidationException(string message) : base(message)
    {
    }
}
