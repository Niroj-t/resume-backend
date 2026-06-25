namespace ResumeAnalyzer.Api.DTOs;

/// <summary>
/// Response DTO returned by POST /api/analyze and GET /api/analysis/{id}.
/// Intentionally mirrors the shape the Next.js frontend's AnalysisResult
/// type expects, so the frontend can consume it with minimal mapping.
/// </summary>
public class AnalysisResultDto
{
    public Guid Id { get; set; }

    public string ResumeFileName { get; set; } = string.Empty;

    public string JobDescription { get; set; } = string.Empty;

    public int MatchScore { get; set; }

    public string MatchLabel { get; set; } = string.Empty;

    public List<string> MatchedKeywords { get; set; } = new();

    public List<string> MissingSkills { get; set; } = new();

    public List<string> Strengths { get; set; } = new();

    public List<string> ImprovementSuggestions { get; set; } = new();

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Lightweight DTO used for the GET /api/analysis list endpoint, so we
/// don't ship full resume text / large payloads in a list response.
/// </summary>
public class AnalysisSummaryDto
{
    public Guid Id { get; set; }
    public string ResumeFileName { get; set; } = string.Empty;
    public int MatchScore { get; set; }
    public string MatchLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
