using Microsoft.EntityFrameworkCore;
using StudentProjects.Domain.Entities;

namespace StudentProjects.Infrastructure.Persistence;
/// <summary>Draft EF model only. No migrations or database integration is active.</summary>
public sealed class StudentProjectsDbContext(DbContextOptions<StudentProjectsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<StudentProfile> Students => Set<StudentProfile>();
    public DbSet<LecturerProfile> Lecturers => Set<LecturerProfile>();
    public DbSet<RegistrationPeriod> RegistrationPeriods => Set<RegistrationPeriod>();
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
        // These provisional constraints do not substitute for transactional business validation.
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<StudentProfile>().HasIndex(x => x.StudentCode).IsUnique();
        modelBuilder.Entity<StudentProfile>().HasIndex(x => x.UserId).IsUnique();
        modelBuilder.Entity<LecturerProfile>().HasIndex(x => x.UserId).IsUnique();
        modelBuilder.Entity<LecturerProfile>().Property(x => x.RowVersion).IsRowVersion();
        modelBuilder.Entity<LecturerProfile>().ToTable(t => t.HasCheckConstraint(
            "CK_Lecturer_Capacity", "[MaxStudents] >= 0 AND [CurrentStudents] >= 0 AND [CurrentStudents] <= [MaxStudents]"));
        modelBuilder.Entity<SystemSetting>().HasIndex(x => x.Key).IsUnique();
        modelBuilder.Entity<Topic>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<Project>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<Milestone>().Property(x => x.Status).HasConversion<string>();
    }
}
