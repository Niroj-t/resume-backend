using Microsoft.AspNetCore.Identity;
using ResumeAnalyzer.Api.Models;

namespace ResumeAnalyzer.Api.Services;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>True if the plaintext password matches the stored hash.</summary>
    bool Verify(string passwordHash, string suppliedPassword);
}

/// <summary>
/// Wraps ASP.NET Core Identity's PasswordHasher&lt;T&gt; — a well-tested,
/// salted, PBKDF2-based hashing implementation that ships with the
/// framework, so no extra third-party crypto package is needed.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password)
    {
        // PasswordHasher<T> only uses the passed instance for typing —
        // it doesn't read any properties off it, so a placeholder is fine.
        return _hasher.HashPassword(new User(), password);
    }

    public bool Verify(string passwordHash, string suppliedPassword)
    {
        var result = _hasher.VerifyHashedPassword(new User(), passwordHash, suppliedPassword);
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
