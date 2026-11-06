using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentProjects.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V011AiCodeAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
                name: "CodeAnalysisRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CommitSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DevelopmentWork = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AnalyzedPathsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SkippedFiles = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeAnalysisRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeAnalysisRuns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CodeAnalysisRuns_Repositories_RepositoryLinkId",
                        column: x => x.RepositoryLinkId,
                        principalTable: "Repositories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CodeAnalysisRuns_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CodeAnalysisFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeAnalysisFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeAnalysisFindings_CodeAnalysisRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "CodeAnalysisRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CodeAnalysisReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LecturerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeAnalysisReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeAnalysisReviews_CodeAnalysisRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "CodeAnalysisRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CodeAnalysisReviews_Users_LecturerUserId",
                        column: x => x.LecturerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodeAnalysisFindings_RunId_Fingerprint",
                table: "CodeAnalysisFindings",
                columns: new[] { "RunId", "Fingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeAnalysisReviews_LecturerUserId",
                table: "CodeAnalysisReviews",
                column: "LecturerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeAnalysisReviews_RunId_CreatedAt",
                table: "CodeAnalysisReviews",
                columns: new[] { "RunId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeAnalysisRuns_ProjectId_CreatedAt",
                table: "CodeAnalysisRuns",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeAnalysisRuns_RepositoryLinkId_CommitSha_Scope_ModelName",
                table: "CodeAnalysisRuns",
                columns: new[] { "RepositoryLinkId", "CommitSha", "Scope", "ModelName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeAnalysisRuns_RequestedByUserId",
                table: "CodeAnalysisRuns",
                column: "RequestedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodeAnalysisFindings");

            migrationBuilder.DropTable(
                name: "CodeAnalysisReviews");

            migrationBuilder.DropTable(
                name: "CodeAnalysisRuns");

        }
    }
}
