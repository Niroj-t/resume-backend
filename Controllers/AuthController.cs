using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResumeAnalyzer.Api.DTOs;
using ResumeAnalyzer.Api.Services;

namespace ResumeAnalyzer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthOrchestrator _authOrchestrator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthOrchestrator authOrchestrator, ILogger<AuthController> logger)
    {
        _authOrchestrator = authOrchestrator;
        _logger = logger;
    }

    /// <summary>Creates a new account and returns a JWT for it.</summary>
    [HttpPost("signup")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> Signup([FromBody] SignupRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid signup details",
                Detail = "Check that your name, email, and password (min 8 characters) are valid.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        try
        {
            var result = await _authOrchestrator.SignupAsync(request);
            return Ok(result);
        }
        catch (EmailAlreadyInUseException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Email already in use",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during signup");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Signup failed",
                Detail = "An unexpected error occurred. Please try again.",
                Status = StatusCodes.Status500InternalServerError,
            });
        }
    }

    /// <summary>Authenticates an existing account and returns a JWT for it.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid login details",
                Detail = "Email and password are required.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        try
        {
            var result = await _authOrchestrator.LoginAsync(request);
            return Ok(result);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Login failed",
                Detail = ex.Message,
                Status = StatusCodes.Status401Unauthorized,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Login failed",
                Detail = "An unexpected error occurred. Please try again.",
                Status = StatusCodes.Status500InternalServerError,
            });
        }
    }

    /// <summary>Returns the currently authenticated user, derived from the JWT.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _authOrchestrator.GetUserAsync(userId.Value);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(user);
    }

    /// <summary>
    /// Deletes (soft-deletes) the currently authenticated user's account.
    /// The account is deactivated immediately — its email becomes free to
    /// re-register and login/"/me" will treat it as gone — but the user's
    /// past Analyses are intentionally retained, not cascade-deleted. This
    /// is the only supported account-removal path; there is no endpoint
    /// that hard-deletes a Users row.
    /// </summary>
    [HttpDelete("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMe()
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await _authOrchestrator.DeleteAccountAsync(userId.Value);
        return NoContent();
    }
}

/// <summary>Small helper for pulling the authenticated user's id out of JWT claims.</summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;

        return Guid.TryParse(subClaim, out var id) ? id : null;
    }
}
