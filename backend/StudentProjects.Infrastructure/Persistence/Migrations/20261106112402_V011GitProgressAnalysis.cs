using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentProjects.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V011GitProgressAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GitProgressRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    HeadSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    TotalCommits = table.Column<int>(type: "int", nullable: false),
                    TotalFiles = table.Column<int>(type: "int", nullable: false),
                    Additions = table.Column<int>(type: "int", nullable: false),
                    Deletions = table.Column<int>(type: "int", nullable: false),
                    SentFiles = table.Column<int>(type: "int", nullable: false),
                    PartialCoverage = table.Column<bool>(type: "bit", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FilesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitProgressRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitProgressRuns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GitProgressRuns_Repositories_RepositoryLinkId",
                        column: x => x.RepositoryLinkId,
                        principalTable: "Repositories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GitProgressRuns_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitProgressRuns_ProjectId_CreatedAt",
                table: "GitProgressRuns",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GitProgressRuns_RepositoryLinkId_BaseSha_HeadSha_ModelName",
                table: "GitProgressRuns",
                columns: new[] { "RepositoryLinkId", "BaseSha", "HeadSha", "ModelName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitProgressRuns_RequestedByUserId",
                table: "GitProgressRuns",
                column: "RequestedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitProgressRuns");
        }
    }
}
