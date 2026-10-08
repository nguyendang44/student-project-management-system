using Microsoft.EntityFrameworkCore;
using StudentProjects.Domain.Entities;

namespace StudentProjects.Infrastructure.Persistence;

/// <summary>
/// Week 3 relational model proposal. No migrations or database operations run on startup.
/// Business invariants spanning multiple rows still require transactional application logic.
/// </summary>
public sealed class StudentProjectsDbContext(DbContextOptions<StudentProjectsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<StudentProfile> Students => Set<StudentProfile>();
    public DbSet<LecturerProfile> Lecturers => Set<LecturerProfile>();
    public DbSet<RegistrationPeriod> RegistrationPeriods => Set<RegistrationPeriod>();
    public DbSet<LecturerCapacity> LecturerCapacities => Set<LecturerCapacity>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<TopicStateHistory> TopicStateHistories => Set<TopicStateHistory>();
    public DbSet<TopicRegistration> TopicRegistrations => Set<TopicRegistration>();
    public DbSet<LecturerRequest> LecturerRequests => Set<LecturerRequest>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<ProgressUpdate> ProgressUpdates => Set<ProgressUpdate>();
    public DbSet<MilestoneSubmission> MilestoneSubmissions => Set<MilestoneSubmission>();
    public DbSet<ProjectEvaluation> ProjectEvaluations => Set<ProjectEvaluation>();
    public DbSet<RepositoryLink> Repositories => Set<RepositoryLink>();
    public DbSet<CodeAnalysisReport> CodeAnalysisReports => Set<CodeAnalysisReport>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AutomationRun> AutomationRuns => Set<AutomationRun>();
    public DbSet<SystemError> SystemErrors => Set<SystemError>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Avoid cascade paths between multiple user-owned relationships and retain audit history.
        // Deletion semantics and sensitive-data retention will be decided before production.
        const DeleteBehavior onDelete = DeleteBehavior.NoAction;

        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<StudentProfile>(e =>
        {
            e.Property(x => x.StudentCode).HasMaxLength(40).IsRequired();
            e.Property(x => x.Faculty).HasMaxLength(200);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(onDelete);
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasIndex(x => x.StudentCode).IsUnique();
        });

        modelBuilder.Entity<LecturerProfile>(e =>
        {
            e.Property(x => x.Specialty).HasMaxLength(300);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(onDelete);
            e.HasIndex(x => x.UserId).IsUnique();
        });

        modelBuilder.Entity<RegistrationPeriod>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.ToTable(t => t.HasCheckConstraint("CK_RegistrationPeriod_Dates", "[EndsAt] > [StartsAt]"));
        });

        modelBuilder.Entity<LecturerCapacity>(e =>
        {
            e.HasOne<User>().WithMany().HasForeignKey(x => x.LecturerUserId).OnDelete(onDelete);
            e.HasOne<RegistrationPeriod>().WithMany().HasForeignKey(x => x.RegistrationPeriodId).OnDelete(onDelete);
            e.HasIndex(x => new { x.LecturerUserId, x.RegistrationPeriodId }).IsUnique();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.ToTable(t => t.HasCheckConstraint("CK_LecturerCapacity_Bounds", "[MaxStudents] >= 0 AND [CurrentStudents] >= 0 AND [CurrentStudents] <= [MaxStudents]"));
        });

        modelBuilder.Entity<Topic>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Objective).HasMaxLength(2000);
            e.Property(x => x.ExpectedContent).HasMaxLength(2000);
            e.Property(x => x.ProposedTechnology).HasMaxLength(1000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ProposedByUserId).OnDelete(onDelete);
            e.HasIndex(x => new { x.Status, x.IsRegistrationOpen });
        });

        modelBuilder.Entity<TopicStateHistory>(e =>
        {
            e.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Reason).HasMaxLength(2000);
            e.HasOne<Topic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(onDelete);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(onDelete);
            e.HasIndex(x => new { x.TopicId, x.CreatedAt });
        });

        modelBuilder.Entity<TopicRegistration>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.HasOne<Topic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(onDelete);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(onDelete);
            e.HasOne<RegistrationPeriod>().WithMany().HasForeignKey(x => x.RegistrationPeriodId).OnDelete(onDelete);
            e.HasIndex(x => new { x.StudentUserId, x.RegistrationPeriodId, x.Status });
        });

        modelBuilder.Entity<LecturerRequest>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.Property(x => x.RejectionReason).HasMaxLength(2000);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(onDelete);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.LecturerUserId).OnDelete(onDelete);
            e.HasOne<Topic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(onDelete);
            e.HasOne<RegistrationPeriod>().WithMany().HasForeignKey(x => x.RegistrationPeriodId).OnDelete(onDelete);
            e.HasIndex(x => new { x.LecturerUserId, x.RegistrationPeriodId, x.Status });
            e.HasIndex(x => new { x.StudentUserId, x.RegistrationPeriodId, x.Status });
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.HasOne<Topic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(onDelete);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(onDelete);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.LecturerUserId).OnDelete(onDelete);
            e.HasOne<RegistrationPeriod>().WithMany().HasForeignKey(x => x.RegistrationPeriodId).OnDelete(onDelete);
            e.HasOne<LecturerRequest>().WithMany().HasForeignKey(x => x.AcceptedLecturerRequestId).OnDelete(onDelete);
            e.HasIndex(x => x.AcceptedLecturerRequestId).IsUnique(); // SQL Server filters nullable unique indexes
            e.HasIndex(x => new { x.StudentUserId, x.RegistrationPeriodId });
        });

        modelBuilder.Entity<Milestone>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.ExpectedResult).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(onDelete);
            e.HasIndex(x => new { x.ProjectId, x.DeadlineAt });
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Milestone_Percent", "[PercentComplete] BETWEEN 0 AND 100");
                t.HasCheckConstraint("CK_Milestone_Dates", "[DeadlineAt] >= [StartAt]");
            });
        });

        modelBuilder.Entity<ProgressUpdate>(e =>
        {
            e.Property(x => x.WorkDone).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Note).HasMaxLength(2000);
            e.HasOne<Milestone>().WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(onDelete);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(onDelete);
            e.HasIndex(x => new { x.MilestoneId, x.CreatedAt });
            e.ToTable(t => t.HasCheckConstraint("CK_ProgressUpdate_Percent", "[PercentComplete] BETWEEN 0 AND 100"));
        });

        modelBuilder.Entity<MilestoneSubmission>(e =>
        {
            e.Property(x => x.ResultSummary).HasMaxLength(4000).IsRequired();
            e.Property(x => x.DocumentReference).HasMaxLength(1000);
            e.HasOne<Milestone>().WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(onDelete);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.SubmittedByStudentUserId).OnDelete(onDelete);
            e.HasIndex(x => new { x.MilestoneId, x.SubmittedAt });
        });

        modelBuilder.Entity<ProjectEvaluation>(e =>
        {
            e.Property(x => x.Feedback).HasMaxLength(4000);
            e.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(onDelete);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.LecturerUserId).OnDelete(onDelete);
            e.HasOne<Milestone>().WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(onDelete);
            e.HasIndex(x => new { x.ProjectId, x.CreatedAt });
        });

        modelBuilder.Entity<RepositoryLink>(e =>
        {
            e.Property(x => x.RepositoryUrl).HasMaxLength(1000).IsRequired();
            e.Property(x => x.DefaultBranch).HasMaxLength(200);
            e.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(onDelete);
            e.HasIndex(x => x.ProjectId);
        });

        modelBuilder.Entity<CodeAnalysisReport>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.Property(x => x.CodeQuality).HasMaxLength(2000);
            e.Property(x => x.Complexity).HasMaxLength(2000);
            e.Property(x => x.Maintainability).HasMaxLength(2000);
            e.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(onDelete);
            e.HasOne<RepositoryLink>().WithMany().HasForeignKey(x => x.RepositoryLinkId).OnDelete(onDelete);
            e.HasIndex(x => new { x.ProjectId, x.CreatedAt });
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(100).IsRequired();
            e.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(onDelete);
            e.HasIndex(x => new { x.RecipientUserId, x.IsRead, x.CreatedAt });
        });

        modelBuilder.Entity<AutomationRun>(e =>
        {
            e.Property(x => x.JobName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.Property(x => x.ErrorMessage).HasMaxLength(4000);
            e.HasIndex(x => new { x.JobName, x.StartedAt });
        });

        modelBuilder.Entity<SystemError>(e =>
        {
            e.Property(x => x.Origin).HasMaxLength(200).IsRequired();
            e.Property(x => x.Severity).HasMaxLength(40).IsRequired();
            e.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        });

        modelBuilder.Entity<AuditEntry>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(200).IsRequired();
            e.Property(x => x.EntityName).HasMaxLength(200).IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(onDelete);
            e.HasIndex(x => new { x.EntityName, x.EntityId, x.CreatedAt });
        });

        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.Property(x => x.Key).HasMaxLength(200).IsRequired();
            e.Property(x => x.Value).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => x.Key).IsUnique();
        });
    }
}
