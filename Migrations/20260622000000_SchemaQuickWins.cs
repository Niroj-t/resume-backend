using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResumeAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class SchemaQuickWins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------
            // 1. Composite index (UserId, CreatedAt DESC) on Analyses,
            //    replacing the two separate single-column indexes. This
            //    matches the actual access pattern in
            //    AnalysisOrchestrator.GetAllAsync (WHERE UserId = ? ORDER BY
            //    CreatedAt DESC) and lets Postgres satisfy both the filter
            //    and the sort directly from the index.
            // ---------------------------------------------------------------
            migrationBuilder.DropIndex(
                name: "IX_Analyses_CreatedAt",
                table: "Analyses");

            migrationBuilder.DropIndex(
                name: "IX_Analyses_UserId",
                table: "Analyses");

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_UserId_CreatedAt",
                table: "Analyses",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            // ---------------------------------------------------------------
            // 2. CHECK constraint: MatchScore must be a 0-100 percentage.
            //    Like the column-length changes below, this validates every
            //    existing row when applied — if any row already has a
            //    MatchScore outside 0-100, this migration will fail with a
            //    "check constraint is violated by some row" error. Check
            //    first with:
            //      SELECT id, "MatchScore" FROM "Analyses"
            //      WHERE "MatchScore" NOT BETWEEN 0 AND 100;
            // ---------------------------------------------------------------
            migrationBuilder.AddCheckConstraint(
                name: "CK_Analyses_MatchScore_Range",
                table: "Analyses",
                sql: "\"MatchScore\" BETWEEN 0 AND 100");

            // ---------------------------------------------------------------
            // 3. Cap JobDescription length at 10,000 characters. text and
            //    varchar are binary-compatible in Postgres, so this does NOT
            //    require an explicit USING cast — but it DOES re-validate
            //    every existing row against the new length limit. If any
            //    row's JobDescription is already longer than 10,000 chars,
            //    this migration will fail at apply time with a "value too
            //    long for type character varying(10000)" error. Check first
            //    with:
            //      SELECT id, length("JobDescription") FROM "Analyses"
            //      WHERE length("JobDescription") > 10000;
            //    and truncate/backfill any offending rows before applying.
            // ---------------------------------------------------------------
            migrationBuilder.AlterColumn<string>(
                name: "JobDescription",
                table: "Analyses",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            // ---------------------------------------------------------------
            // 5. Document PasswordHash's expected shape with an explicit
            //    length cap. PBKDF2 hashes from PasswordHasher<T> are a fixed
            //    ~84-character base64 string, so 256 is pure headroom — no
            //    existing data is at risk of truncation.
            // ---------------------------------------------------------------
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            // ---------------------------------------------------------------
            // 6. Soft-delete support on Users, and switch the Analyses ->
            //    Users FK from Cascade to Restrict so that deleting a user no
            //    longer silently destroys their analysis history. Soft
            //    delete (DeletedAt) becomes the supported account-removal
            //    path; a hard DELETE on Users now fails loudly while
            //    Analyses rows still reference it.
            // ---------------------------------------------------------------
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Analyses_Users_UserId",
                table: "Analyses");

            migrationBuilder.AddForeignKey(
                name: "FK_Analyses_Users_UserId",
                table: "Analyses",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // The original unique index on Email didn't account for
            // soft-deleted rows. Replace it with a partial unique index
            // that only applies to active (DeletedAt IS NULL) users, so a
            // deleted account's email can be re-registered later.
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // --- 6. Revert FK to Cascade, drop DeletedAt, restore plain unique index ---
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Analyses_Users_UserId",
                table: "Analyses");

            migrationBuilder.AddForeignKey(
                name: "FK_Analyses_Users_UserId",
                table: "Analyses",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            // --- 5. Revert PasswordHash to unbounded text ---
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            // --- 3. Revert JobDescription to unbounded text ---
            migrationBuilder.AlterColumn<string>(
                name: "JobDescription",
                table: "Analyses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10000)",
                oldMaxLength: 10000);

            // --- 2. Drop the MatchScore CHECK constraint ---
            migrationBuilder.DropCheckConstraint(
                name: "CK_Analyses_MatchScore_Range",
                table: "Analyses");

            // --- 1. Restore the original two single-column indexes ---
            migrationBuilder.DropIndex(
                name: "IX_Analyses_UserId_CreatedAt",
                table: "Analyses");

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_CreatedAt",
                table: "Analyses",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_UserId",
                table: "Analyses",
                column: "UserId");
        }
    }
}
