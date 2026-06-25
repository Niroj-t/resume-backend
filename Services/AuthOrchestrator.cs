using Microsoft.EntityFrameworkCore;
using ResumeAnalyzer.Api.Data;
using ResumeAnalyzer.Api.DTOs;
using ResumeAnalyzer.Api.Models;

namespace ResumeAnalyzer.Api.Services;

public interface IAuthOrchestrator
{
    Task<AuthResponseDto> SignupAsync(SignupRequestDto request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<UserDto?> GetUserAsync(Guid userId);

    /// <summary>
    /// Soft-deletes the given user's account (sets DeletedAt) without
    /// touching their Analyses rows. Their email becomes available for
    /// re-registration, and login/"/me" will treat the account as gone,
    /// but analysis history is retained for audit purposes — see the
    /// Restrict (not Cascade) FK on Analyses.UserId in AppDbContext.
    /// </summary>
    Task DeleteAccountAsync(Guid userId);
}

public class AuthOrchestrator : IAuthOrchestrator
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _tokenService;

    public AuthOrchestrator(AppDbContext db, IPasswordHasher passwordHasher, IJwtTokenService tokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> SignupAsync(SignupRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Soft-deleted accounts don't block re-registration of the same
        // email — only active accounts do (matches the partial unique
        // index on Users.Email in AppDbContext).
        var alreadyExists = await _db.Users
            .AnyAsync(u => u.Email == normalizedEmail && u.DeletedAt == null);
        if (alreadyExists)
        {
            throw new EmailAlreadyInUseException();
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.DeletedAt == null);

        // Deliberately the same exception/message whether the email doesn't
        // exist, belongs to a deleted account, or the password is wrong —
        // avoids leaking which emails are registered via different error
        // messages.
        if (user is null || !_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new InvalidCredentialsException();
        }

        return BuildAuthResponse(user);
    }

    public async Task<UserDto?> GetUserAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user is null || user.DeletedAt is not null ? null : MapToDto(user);
    }

    public async Task DeleteAccountAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null || user.DeletedAt is not null)
        {
            // Already gone (or already deleted) — treat as a no-op rather
            // than throwing, so this stays idempotent.
            return;
        }

        user.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Note: we deliberately do NOT touch user.Analyses here. The FK is
        // now Restrict (see AppDbContext), so a hard DELETE on this Users
        // row would fail while Analyses rows still reference it — soft
        // delete is the supported path, and history is kept intact.
    }

    private AuthResponseDto BuildAuthResponse(User user) => new()
    {
        Token = _tokenService.GenerateToken(user),
        User = MapToDto(user),
    };

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        CreatedAt = user.CreatedAt,
    };
}
