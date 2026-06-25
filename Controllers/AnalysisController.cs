using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResumeAnalyzer.Api.DTOs;
using ResumeAnalyzer.Api.Services;

namespace ResumeAnalyzer.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class AnalysisController : ControllerBase
{
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(IAnalysisOrchestrator orchestrator, ILogger<AnalysisController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes a resume against a job description and persists the result
    /// for the currently authenticated user. Requires a valid JWT bearer
    /// token (see AuthController). Expects multipart/form-data with
    /// fields: "resume" (file) and "jobDescription" (string).
    /// </summary>
    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB hard ceiling at the request level
    [ProducesResponseType(typeof(AnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AnalysisResultDto>> Analyze(
        [FromForm] IFormFile? resume,
        [FromForm] string? jobDescription)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            // Validation (including the null-file check) happens inside the
            // orchestrator/validator, so a null `resume` is handled safely
            // and turned into a 400 via FileValidationException below.
            var result = await _orchestrator.AnalyzeAndSaveAsync(userId.Value, resume, jobDescription ?? string.Empty);
            return Ok(result);
        }
        catch (FileValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid file",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (JobDescriptionValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid job description",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }
        catch (AccountDeletedException ex)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Account deleted",
                Detail = ex.Message,
                Status = StatusCodes.Status401Unauthorized,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while analyzing resume");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Analysis failed",
                Detail = "An unexpected error occurred while analyzing the resume. Please try again.",
                Status = StatusCodes.Status500InternalServerError,
            });
        }
    }

    /// <summary>Returns a lightweight list of the current user's past analyses, most recent first.</summary>
    [HttpGet("analysis")]
    [ProducesResponseType(typeof(List<AnalysisSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<AnalysisSummaryDto>>> GetAll()
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var results = await _orchestrator.GetAllAsync(userId.Value);
        return Ok(results);
    }

    /// <summary>Returns the full result for a single analysis by id, if it belongs to the current user.</summary>
    [HttpGet("analysis/{id:guid}")]
    [ProducesResponseType(typeof(AnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalysisResultDto>> GetById(Guid id)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _orchestrator.GetByIdAsync(userId.Value, id);

        if (result is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not found",
                Detail = $"No analysis found with id '{id}'.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        return Ok(result);
    }
}
