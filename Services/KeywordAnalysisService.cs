using System.Text.RegularExpressions;

namespace ResumeAnalyzer.Api.Services;

public class AnalysisOutcome
{
    public int MatchScore { get; set; }
    public string MatchLabel { get; set; } = string.Empty;
    public List<string> MatchedKeywords { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
}

public interface IResumeAnalysisService
{
    /// <summary>
    /// Performs ATS-style analysis between resume text and a job description.
    /// Returns a Task to allow implementations that call external APIs.
    /// </summary>
    Task<AnalysisOutcome> AnalyzeAsync(string resumeText, string jobDescription);
}

public class KeywordAnalysisService : IResumeAnalysisService
{
    private static readonly string[] SkillVocabulary =
    {
        "javascript", "typescript", "react", "next.js", "nextjs", "node.js", "nodejs",
        "express", "rest api", "graphql", "postgresql", "mysql", "mongodb", "redis",
        "docker", "kubernetes", "aws", "azure", "gcp", "ci/cd", "git", "github",
        "agile", "scrum", "unit testing", "jest", "tailwind", "css", "html",
        "system design", "microservices", "python", "java", "c#", ".net", "asp.net",
        "entity framework", "sql", "communication", "leadership", "problem solving",
    };

    public Task<AnalysisOutcome> AnalyzeAsync(string resumeText, string jobDescription)
    {
        return Task.FromResult(Analyze(resumeText, jobDescription));
    }

    private AnalysisOutcome Analyze(string resumeText, string jobDescription)
    {
        var resumeTokens = Tokenize(resumeText);
        var jdTokens = Tokenize(jobDescription);

        var jdSkills = SkillVocabulary
            .Where(skill => ContainsPhrase(jdTokens, jobDescription, skill))
            .ToList();

        var skillsToCheck = jdSkills.Count > 0 ? jdSkills : SkillVocabulary.ToList();

        var matched = skillsToCheck
            .Where(skill => ContainsPhrase(resumeTokens, resumeText, skill))
            .Select(Capitalize)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        var missing = skillsToCheck
            .Where(skill => !ContainsPhrase(resumeTokens, resumeText, skill))
            .Select(Capitalize)
            .Distinct()
            .OrderBy(s => s)
            .Take(8)
            .ToList();

        var matchScore = CalculateScore(matched.Count, skillsToCheck.Count, resumeText);

        return new AnalysisOutcome
        {
            MatchScore = matchScore,
            MatchLabel = ScoreToLabel(matchScore),
            MatchedKeywords = matched,
            MissingSkills = missing,
            Strengths = BuildStrengths(resumeText, matched),
            Suggestions = BuildSuggestions(resumeText, missing),
        };
    }

    private static int CalculateScore(int matchedCount, int totalChecked, string resumeText)
    {
        if (totalChecked == 0) return 50;
        var keywordScore = (double)matchedCount / totalChecked * 80;
        var lengthBonus = Math.Min(20, resumeText.Length / 200.0);
        return Math.Clamp((int)Math.Round(keywordScore + lengthBonus), 0, 100);
    }

    private static string ScoreToLabel(int score) => score switch
    {
        >= 85 => "Excellent",
        >= 70 => "Good",
        >= 50 => "Fair",
        _ => "Poor",
    };

    private static List<string> BuildStrengths(string resumeText, List<string> matched)
    {
        var strengths = new List<string>();
        if (matched.Count > 0)
            strengths.Add($"Resume includes {matched.Count} key skill(s) relevant to the role: {string.Join(", ", matched.Take(5))}");
        if (Regex.IsMatch(resumeText, @"\d+%|\$\d+|\d+\+", RegexOptions.Compiled))
            strengths.Add("Includes measurable, quantified achievements");
        if (resumeText.Length > 1500)
            strengths.Add("Resume has substantial detail across experience and skills");
        if (strengths.Count == 0)
            strengths.Add("Resume was successfully parsed and reviewed");
        return strengths;
    }

    private static List<string> BuildSuggestions(string resumeText, List<string> missing)
    {
        var suggestions = new List<string>();
        if (missing.Count > 0)
            suggestions.Add($"Consider adding these keywords if applicable: {string.Join(", ", missing.Take(5))}");
        if (!Regex.IsMatch(resumeText, @"\d+%|\$\d+|\d+\+", RegexOptions.Compiled))
            suggestions.Add("Add measurable achievements (e.g., 'increased performance by 20%')");
        if (resumeText.Length < 800)
            suggestions.Add("Resume content seems short — consider expanding on your experience and skills");
        suggestions.Add("Tailor your resume's wording to closely match the job description's terminology");
        suggestions.Add("Keep formatting simple (avoid tables/columns) for best ATS compatibility");
        return suggestions.Distinct().ToList();
    }

    private static HashSet<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new HashSet<string>();
        var words = Regex.Matches(text.ToLowerInvariant(), @"[a-z0-9#+\.]+").Select(m => m.Value);
        return new HashSet<string>(words);
    }

    private static bool ContainsPhrase(HashSet<string> tokens, string originalText, string phrase)
    {
        if (phrase.Contains(' ') || phrase.Contains('/'))
            return originalText.Contains(phrase, StringComparison.OrdinalIgnoreCase);
        return tokens.Contains(phrase.ToLowerInvariant());
    }

    private static string Capitalize(string skill)
    {
        var knownCasing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["javascript"] = "JavaScript", ["typescript"] = "TypeScript", ["react"] = "React",
            ["next.js"] = "Next.js", ["nextjs"] = "Next.js", ["node.js"] = "Node.js", ["nodejs"] = "Node.js",
            ["rest api"] = "REST APIs", ["graphql"] = "GraphQL", ["postgresql"] = "PostgreSQL",
            ["mysql"] = "MySQL", ["mongodb"] = "MongoDB", ["redis"] = "Redis", ["docker"] = "Docker",
            ["kubernetes"] = "Kubernetes", ["aws"] = "AWS", ["azure"] = "Azure", ["gcp"] = "GCP",
            ["ci/cd"] = "CI/CD", ["git"] = "Git", ["github"] = "GitHub", ["agile"] = "Agile",
            ["scrum"] = "Scrum", ["unit testing"] = "Unit Testing", ["jest"] = "Jest",
            ["tailwind"] = "Tailwind CSS", ["css"] = "CSS", ["html"] = "HTML",
            ["system design"] = "System Design", ["microservices"] = "Microservices",
            ["python"] = "Python", ["java"] = "Java", ["c#"] = "C#", [".net"] = ".NET",
            ["asp.net"] = "ASP.NET", ["entity framework"] = "Entity Framework", ["sql"] = "SQL",
        };
        return knownCasing.TryGetValue(skill, out var styled)
            ? styled
            : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(skill);
    }
}
