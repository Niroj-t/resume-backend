namespace ResumeAnalyzer.Api.Models;

/// <summary>
/// EF Core entity representing a single resume analysis run.
/// Maps to the "Analyses" table in PostgreSQL.
/// </summary>
public class Analysis
{
    public Guid Id { get; set; }

    /// <summary>
    /// The user who created this analysis. Every analysis now requires
    /// an authenticated user — see AnalysisController's [Authorize].
    /// </summary>
    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string ResumeFileName { get; set; } = string.Empty;

    /// <summary>
    /// Path (relative to wwwroot) where the uploaded resume file is stored.
    /// </summary>
    public string ResumeFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Raw text extracted from the resume file. Optional — extraction can fail
    /// or be skipped for unsupported formats.
    /// </summary>
    public string? ResumeText { get; set; }

    /// <summary>
    /// Capped at 10,000 characters (see AppDbContext and JobDescriptionValidator) —
    /// this is submitted as a raw multipart form field, not a file, so unlike the
    /// resume upload it has no size limit unless enforced explicitly.
    /// </summary>
    public string JobDescription { get; set; } = string.Empty;

    public int MatchScore { get; set; }

    public string MatchLabel { get; set; } = string.Empty;

    /// <summary>
    /// Stored as native PostgreSQL text[] via Npgsql.
    /// </summary>
    public List<string> MatchedKeywords { get; set; } = new();

    public List<string> MissingSkills { get; set; } = new();

    public List<string> Strengths { get; set; } = new();

    public List<string> Suggestions { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
