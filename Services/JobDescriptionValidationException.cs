namespace ResumeAnalyzer.Api.Services;

/// <summary>
/// Thrown when the submitted job description text fails validation
/// (currently just length). Caught by the controller and translated
/// into a 400 response — the same pattern as FileValidationException,
/// kept as a separate type so callers can distinguish "bad file" from
/// "bad job description" if they ever need to.
/// </summary>
public class JobDescriptionValidationException : Exception
{
    public JobDescriptionValidationException(string message) : base(message)
    {
    }
}
