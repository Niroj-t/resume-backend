namespace ResumeAnalyzer.Api.Models;

/// <summary>
/// EF Core entity representing a registered user account.
/// Maps to the "Users" table in PostgreSQL.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Hashed password (via ASP.NET Core's PasswordHasher&lt;T&gt;) — the
    /// plaintext password is never stored or logged anywhere.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft-delete marker. Null means active. When set, the account is
    /// treated as deleted (login/signup-by-email and "/me" all exclude
    /// soft-deleted users) but the row — and the user's Analyses, which
    /// are no longer cascade-deleted — is retained for audit/history.
    /// See AuthOrchestrator and the FK change in AppDbContext.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Analyses created by this user.</summary>
    public List<Analysis> Analyses { get; set; } = new();
}
