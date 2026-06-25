namespace ResumeAnalyzer.Api.Services;

public interface IJobDescriptionValidator
{
    /// <summary>Throws JobDescriptionValidationException if the text is invalid.</summary>
    void Validate(string? jobDescription);
}

/// <summary>
/// Job descriptions are submitted as a raw multipart form field, not a
/// file, so unlike the resume upload they previously had no size limit
/// of their own — the only ceiling was the controller's blanket 10 MB
/// request-size limit. This validator rejects oversized submissions
/// early, with a clear error, instead of relying solely on the
/// "JobDescription" column's HasMaxLength(10_000)/CHECK-equivalent
/// constraint in AppDbContext to fail the save at the very end of the
/// (file upload + text extraction + AI call) pipeline.
/// </summary>
public class JobDescriptionValidator : IJobDescriptionValidator
{
    // Keep in sync with the HasMaxLength(10_000) configured for
    // Analysis.JobDescription in AppDbContext.
    public const int MaxLength = 10_000;

    public void Validate(string? jobDescription)
    {
        if (string.IsNullOrWhiteSpace(jobDescription))
        {
            throw new JobDescriptionValidationException("A job description is required.");
        }

        if (jobDescription.Length > MaxLength)
        {
            throw new JobDescriptionValidationException(
                $"Job description must be {MaxLength:N0} characters or fewer.");
        }
    }
}
