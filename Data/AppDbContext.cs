using Microsoft.EntityFrameworkCore;
using ResumeAnalyzer.Api.Models;

namespace ResumeAnalyzer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Analysis> Analyses => Set<Analysis>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(u => u.Id);

            entity.Property(u => u.Id)
                .ValueGeneratedNever(); // Id is always assigned in application code (Guid.NewGuid())

            entity.Property(u => u.Name)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);

            // PBKDF2 hashes from ASP.NET Core's PasswordHasher<T> are a fixed
            // ~84-character base64 string. 256 leaves comfortable headroom for
            // a future hasher with a longer output without ever truncating —
            // this is documentation-as-schema, not a behavior change.
            entity.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(u => u.CreatedAt)
                .HasDefaultValueSql("now()");

            entity.Property(u => u.DeletedAt);

            // Emails are always stored lower-cased (see AuthOrchestrator),
            // so a unique index here reliably prevents duplicate accounts.
            // Filtered to active (non-soft-deleted) users so a deleted
            // account's email can be re-registered later.
            entity.HasIndex(u => u.Email)
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL");
        });

        modelBuilder.Entity<Analysis>(entity =>
        {
            entity.ToTable("Analyses");

            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id)
                .ValueGeneratedNever(); // Id is always assigned in application code (Guid.NewGuid())

            entity.Property(a => a.ResumeFileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(a => a.ResumeFilePath)
                .IsRequired()
                .HasMaxLength(500);

            // Job descriptions arrive as a raw multipart form field (not a
            // file), so unlike the resume upload they had no size limit at
            // all. 10,000 chars (~1,500-2,000 words) comfortably covers any
            // real job posting while capping worst-case row size and
            // preventing abuse via oversized form submissions.
            entity.Property(a => a.JobDescription)
                .IsRequired()
                .HasMaxLength(10_000);

            entity.Property(a => a.MatchLabel)
                .HasMaxLength(20);

            // Native PostgreSQL text[] columns (requires Npgsql provider).
            entity.Property(a => a.MatchedKeywords)
                .HasColumnType("text[]");

            entity.Property(a => a.MissingSkills)
                .HasColumnType("text[]");

            entity.Property(a => a.Strengths)
                .HasColumnType("text[]");

            entity.Property(a => a.Suggestions)
                .HasColumnType("text[]");

            entity.Property(a => a.CreatedAt)
                .HasDefaultValueSql("now()");

            // Composite index matching the actual access pattern used by
            // AnalysisOrchestrator.GetAllAsync: WHERE UserId = ? ORDER BY
            // CreatedAt DESC. A single (UserId, CreatedAt DESC) index lets
            // Postgres satisfy both the filter and the sort directly from
            // the index, with no separate in-memory sort step. This
            // replaces the previous two single-column indexes, which could
            // only be used for one predicate at a time.
            entity.HasIndex(a => new { a.UserId, a.CreatedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_Analyses_UserId_CreatedAt");

            entity.HasOne(a => a.User)
                .WithMany(u => u.Analyses)
                .HasForeignKey(a => a.UserId)
                // Changed from Cascade: deleting a user must no longer
                // silently wipe their entire analysis history. With
                // soft-delete (User.DeletedAt) as the normal account-removal
                // path, a hard delete of a Users row should now be a rare,
                // deliberate action — and Restrict makes it fail loudly
                // (instead of cascading) unless the caller has explicitly
                // dealt with that user's Analyses first.
                .OnDelete(DeleteBehavior.Restrict);

            // CHECK constraint: MatchScore is meant to be a 0-100 percentage.
            // Nothing upstream currently enforces that at the DB layer, so a
            // bad write (bug, manual SQL, future code change) could silently
            // store an out-of-range value that corrupts downstream analytics.
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Analyses_MatchScore_Range",
                "\"MatchScore\" BETWEEN 0 AND 100"));
        });
    }
}
