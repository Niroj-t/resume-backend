using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResumeAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Analyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ResumeFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ResumeText = table.Column<string>(type: "text", nullable: true),
                    JobDescription = table.Column<string>(type: "text", nullable: false),
                    MatchScore = table.Column<int>(type: "integer", nullable: false),
                    MatchLabel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MatchedKeywords = table.Column<string[]>(type: "text[]", nullable: false),
                    MissingSkills = table.Column<string[]>(type: "text[]", nullable: false),
                    Strengths = table.Column<string[]>(type: "text[]", nullable: false),
                    Suggestions = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analyses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_CreatedAt",
                table: "Analyses",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Analyses");
        }
    }
}
