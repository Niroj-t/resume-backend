using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResumeAnalyzer.Api.Services;

/// <summary>
/// Configuration for the Gemini integration. Bound from the "Gemini"
/// section of appsettings.json (see appsettings.json / user-secrets).
/// </summary>
public class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model id to call, e.g. "gemini-2.5-flash". Flash is fast/cheap and
    /// plenty capable for this kind of structured text analysis; swap to
    /// "gemini-2.5-pro" if you want deeper reasoning at higher latency/cost.
    /// </summary>
    public string Model { get; set; } = "gemini-2.5-flash";
}

/// <summary>
/// AI-powered resume analysis via the Gemini API (generateContent).
///
/// Falls back automatically to KeywordAnalysisService if:
///  - Gemini:ApiKey is not configured
///  - The API call fails or returns a non-success status
///  - The response cannot be parsed into an AnalysisOutcome
///
/// Uses Gemini's responseSchema to constrain the model to return valid,
/// structured JSON rather than relying on prompt engineering.
/// </summary>
public class GeminiAnalysisService : IResumeAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly KeywordAnalysisService _fallback;
    private readonly ILogger<GeminiAnalysisService> _logger;

    private const string ApiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiAnalysisService(
        HttpClient httpClient,
        Microsoft.Extensions.Options.IOptions<GeminiOptions> options,
        ILogger<GeminiAnalysisService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _fallback = new KeywordAnalysisService();
        _logger = logger;
    }

    public async Task<AnalysisOutcome> AnalyzeAsync(string resumeText, string jobDescription)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Gemini:ApiKey is not configured — falling back to KeywordAnalysisService.");
            return await _fallback.AnalyzeAsync(resumeText, jobDescription);
        }

        try
        {
            var requestBody = BuildRequestBody(resumeText, jobDescription);
            var url = $"{ApiBaseUrl}/{_options.Model}:generateContent";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-goog-api-key", _options.ApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Gemini API returned {StatusCode}: {Body} — falling back to KeywordAnalysisService.",
                    response.StatusCode, responseJson);
                return await _fallback.AnalyzeAsync(resumeText, jobDescription);
            }

            var outcome = ParseOutcome(responseJson);
            if (outcome is null)
            {
                _logger.LogWarning("Gemini response could not be parsed into an AnalysisOutcome — falling back.");
                return await _fallback.AnalyzeAsync(resumeText, jobDescription);
            }

            return outcome;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini API call failed — falling back to KeywordAnalysisService.");
            return await _fallback.AnalyzeAsync(resumeText, jobDescription);
        }
    }

    private static object BuildRequestBody(string resumeText, string jobDescription)
    {
        var prompt = $"""
            You are an expert ATS (Applicant Tracking System) resume reviewer and
            career coach. Compare the RESUME against the JOB DESCRIPTION and
            produce a structured evaluation.

            Score generously but honestly (0-100) based on relevant skills,
            experience alignment, and overall resume quality. Identify concrete
            keyword/skill gaps and give specific, actionable improvement advice
            (not generic platitudes).

            JOB DESCRIPTION:
            {(string.IsNullOrWhiteSpace(jobDescription) ? "(none provided — evaluate the resume on its own general strength)" : jobDescription)}

            RESUME TEXT:
            {resumeText}
            """;

        var schema = new
        {
            type = "object",
            properties = new
            {
                matchScore = new { type = "integer" },
                matchLabel = new { type = "string", @enum = new[] { "Excellent", "Good", "Fair", "Poor" } },
                matchedKeywords = new { type = "array", items = new { type = "string" } },
                missingSkills = new { type = "array", items = new { type = "string" } },
                strengths = new { type = "array", items = new { type = "string" } },
                suggestions = new { type = "array", items = new { type = "string" } },
            },
            required = new[] { "matchScore", "matchLabel", "matchedKeywords", "missingSkills", "strengths", "suggestions" },
        };

        return new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[] { new { text = prompt } },
                },
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = schema,
                temperature = 0.4,
            },
        };
    }

    private AnalysisOutcome? ParseOutcome(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parsed = JsonSerializer.Deserialize<GeminiOutcomeJson>(text, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (parsed is null)
        {
            return null;
        }

        return new AnalysisOutcome
        {
            MatchScore = Math.Clamp(parsed.MatchScore, 0, 100),
            MatchLabel = string.IsNullOrWhiteSpace(parsed.MatchLabel) ? ScoreToLabel(parsed.MatchScore) : parsed.MatchLabel,
            MatchedKeywords = parsed.MatchedKeywords ?? new List<string>(),
            MissingSkills = parsed.MissingSkills ?? new List<string>(),
            Strengths = parsed.Strengths ?? new List<string>(),
            Suggestions = parsed.Suggestions ?? new List<string>(),
        };
    }

    private static string ScoreToLabel(int score) => score switch
    {
        >= 85 => "Excellent",
        >= 70 => "Good",
        >= 50 => "Fair",
        _ => "Poor",
    };

    /// <summary>Shape of the JSON Gemini returns, matching the responseSchema above.</summary>
    private class GeminiOutcomeJson
    {
        [JsonPropertyName("matchScore")]
        public int MatchScore { get; set; }

        [JsonPropertyName("matchLabel")]
        public string MatchLabel { get; set; } = string.Empty;

        [JsonPropertyName("matchedKeywords")]
        public List<string>? MatchedKeywords { get; set; }

        [JsonPropertyName("missingSkills")]
        public List<string>? MissingSkills { get; set; }

        [JsonPropertyName("strengths")]
        public List<string>? Strengths { get; set; }

        [JsonPropertyName("suggestions")]
        public List<string>? Suggestions { get; set; }
    }
}
