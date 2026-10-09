using Microsoft.EntityFrameworkCore.Migrations;

namespace StudentProjects.Infrastructure.Persistence.Migrations;

// Hand-authored, feature-scoped migration. Baseline snapshot in v0.4 contains excluded
// entities; do not regenerate this migration with `dotnet ef migrations add`.
public partial class TopicManagementV05 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RegistrationPeriods",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsOpen = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RegistrationPeriods", x => x.Id);
                table.CheckConstraint("CK_RegistrationPeriod_Dates", "[EndsAt] > [StartsAt]");
            });
        migrationBuilder.CreateTable(
            name: "Topics",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                Objective = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                ExpectedContent = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                ProposedTechnology = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ProposedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                IsRegistrationOpen = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Topics", x => x.Id);
                table.ForeignKey("FK_Topics_Users_ProposedByUserId", x => x.ProposedByUserId,
                    "Users", "Id", onDelete: ReferentialAction.NoAction);
            });
        migrationBuilder.CreateTable(
            name: "TopicStateHistories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FromStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ToStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TopicStateHistories", x => x.Id);
                table.ForeignKey("FK_TopicStateHistories_Topics_TopicId", x => x.TopicId,
                    "Topics", "Id", onDelete: ReferentialAction.NoAction);
                table.ForeignKey("FK_TopicStateHistories_Users_ActorUserId", x => x.ActorUserId,
                    "Users", "Id", onDelete: ReferentialAction.NoAction);
            });
        migrationBuilder.CreateTable(
            name: "TopicRegistrations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RegistrationPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TopicRegistrations", x => x.Id);
                table.ForeignKey("FK_TopicRegistrations_Topics_TopicId", x => x.TopicId, "Topics", "Id", onDelete: ReferentialAction.NoAction);
                table.ForeignKey("FK_TopicRegistrations_Users_StudentUserId", x => x.StudentUserId, "Users", "Id", onDelete: ReferentialAction.NoAction);
                table.ForeignKey("FK_TopicRegistrations_RegistrationPeriods_RegistrationPeriodId", x => x.RegistrationPeriodId,
                    "RegistrationPeriods", "Id", onDelete: ReferentialAction.NoAction);
            });
        migrationBuilder.CreateIndex(name: "IX_Topics_ProposedByUserId", table: "Topics", column: "ProposedByUserId");
        migrationBuilder.CreateIndex(name: "IX_Topics_Status_IsRegistrationOpen", table: "Topics", columns: new[] { "Status", "IsRegistrationOpen" });
        migrationBuilder.CreateIndex(name: "IX_TopicStateHistories_TopicId_CreatedAt", table: "TopicStateHistories", columns: new[] { "TopicId", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_TopicStateHistories_ActorUserId", table: "TopicStateHistories", column: "ActorUserId");
        migrationBuilder.CreateIndex(name: "IX_TopicRegistrations_TopicId", table: "TopicRegistrations", column: "TopicId");
        migrationBuilder.CreateIndex(name: "IX_TopicRegistrations_RegistrationPeriodId", table: "TopicRegistrations", column: "RegistrationPeriodId");
        migrationBuilder.CreateIndex(name: "IX_TopicRegistrations_StudentUserId_RegistrationPeriodId_Status",
            table: "TopicRegistrations", columns: new[] { "StudentUserId", "RegistrationPeriodId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_TopicRegistrations_StudentUserId_RegistrationPeriodId_Active",
            table: "TopicRegistrations", columns: new[] { "StudentUserId", "RegistrationPeriodId" }, unique: true,
            filter: "[Status] IN ('PENDING','ACCEPTED')");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TopicRegistrations");
        migrationBuilder.DropTable(name: "TopicStateHistories");
        migrationBuilder.DropTable(name: "Topics");
        migrationBuilder.DropTable(name: "RegistrationPeriods");
    }
}
