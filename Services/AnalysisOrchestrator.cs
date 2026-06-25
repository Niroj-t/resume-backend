using Microsoft.EntityFrameworkCore;
using ResumeAnalyzer.Api.Data;
using ResumeAnalyzer.Api.DTOs;
using ResumeAnalyzer.Api.Models;

namespace ResumeAnalyzer.Api.Services;

public interface IAnalysisOrchestrator
{
    Task<AnalysisResultDto> AnalyzeAndSaveAsync(Guid userId, IFormFile? resumeFile, string jobDescription);
    Task<List<AnalysisSummaryDto>> GetAllAsync(Guid userId);
    Task<AnalysisResultDto?> GetByIdAsync(Guid userId, Guid id);
}

/// <summary>
/// Coordinates the full "analyze a resume" workflow:
/// validate -> store file -> extract text -> run analysis -> persist -> return DTO.
/// Controllers should depend on this, not on the individual services directly.
///
/// Every method takes the authenticated user's id and scopes all reads and
/// writes to that user — the analyzer requires login (see AnalysisController's
/// [Authorize]), and one user should never be able to see or affect another
/// user's analyses, including by guessing another user's analysis id.
/// </summary>
public class AnalysisOrchestrator : IAnalysisOrchestrator
{
    private readonly AppDbContext _db;
    private readonly IFileValidator _fileValidator;
    private readonly IJobDescriptionValidator _jobDescriptionValidator;
    private readonly IFileStorageService _fileStorage;
    private readonly IResumeTextExtractor _textExtractor;
    private readonly IResumeAnalysisService _analysisService;
    private readonly ILogger<AnalysisOrchestrator> _logger;

    public AnalysisOrchestrator(
        AppDbContext db,
        IFileValidator fileValidator,
        IJobDescriptionValidator jobDescriptionValidator,
        IFileStorageService fileStorage,
        IResumeTextExtractor textExtractor,
        IResumeAnalysisService analysisService,
        ILogger<AnalysisOrchestrator> logger)
    {
        _db = db;
        _fileValidator = fileValidator;
        _jobDescriptionValidator = jobDescriptionValidator;
        _fileStorage = fileStorage;
        _textExtractor = textExtractor;
        _analysisService = analysisService;
        _logger = logger;
    }

    public async Task<AnalysisResultDto> AnalyzeAndSaveAsync(Guid userId, IFormFile? resumeFile, string jobDescription)
    {
        // A soft-deleted account's JWT can still be technically valid (tokens
        // last up to 7 days — see JwtTokenService) even after DeleteAccountAsync
        // has run, since [Authorize] only checks signature/expiry, not whether
        // the user row is still active. Block new analyses for deleted accounts
        // explicitly, rather than relying on the FK alone (FK is Restrict, not
        // a presence check, so it wouldn't catch this on its own).
        var userIsActive = await _db.Users.AnyAsync(u => u.Id == userId && u.DeletedAt == null);
        if (!userIsActive)
        {
            throw new AccountDeletedException();
        }

        _fileValidator.Validate(resumeFile);
        _jobDescriptionValidator.Validate(jobDescription);

        // Validate() throws FileValidationException if resumeFile is null,
        // so by this point it's guaranteed to be non-null.
        var file = resumeFile!;

        var filePath = await _fileStorage.SaveAsync(file);
        var resumeText = await _textExtractor.ExtractTextAsync(file);

        var outcome = await _analysisService.AnalyzeAsync(resumeText, jobDescription ?? string.Empty);

        var entity = new Analysis
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeFileName = file.FileName,
            ResumeFilePath = filePath,
            ResumeText = resumeText,
            JobDescription = jobDescription ?? string.Empty,
            MatchScore = outcome.MatchScore,
            MatchLabel = outcome.MatchLabel,
            MatchedKeywords = outcome.MatchedKeywords,
            MissingSkills = outcome.MissingSkills,
            Strengths = outcome.Strengths,
            Suggestions = outcome.Suggestions,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Analyses.Add(entity);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Saved analysis {AnalysisId} for user {UserId} ({FileName}) with score {Score}",
            entity.Id, userId, entity.ResumeFileName, entity.MatchScore);

        return MapToDto(entity);
    }

    public async Task<List<AnalysisSummaryDto>> GetAllAsync(Guid userId)
    {
        return await _db.Analyses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AnalysisSummaryDto
            {
                Id = a.Id,
                ResumeFileName = a.ResumeFileName,
                MatchScore = a.MatchScore,
                MatchLabel = a.MatchLabel,
                CreatedAt = a.CreatedAt,
            })
            .ToListAsync();
    }

    public async Task<AnalysisResultDto?> GetByIdAsync(Guid userId, Guid id)
    {
        // Filtering by UserId here (rather than just Id) means a request for
        // another user's analysis id returns null -> 404, not someone else's
        // data and not a 403 that would confirm the id exists.
        var entity = await _db.Analyses
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

        return entity is null ? null : MapToDto(entity);
    }

    private static AnalysisResultDto MapToDto(Analysis entity) => new()
    {
        Id = entity.Id,
        ResumeFileName = entity.ResumeFileName,
        JobDescription = entity.JobDescription,
        MatchScore = entity.MatchScore,
        MatchLabel = entity.MatchLabel,
        MatchedKeywords = entity.MatchedKeywords,
        MissingSkills = entity.MissingSkills,
        Strengths = entity.Strengths,
        ImprovementSuggestions = entity.Suggestions,
        CreatedAt = entity.CreatedAt,
    };
}
