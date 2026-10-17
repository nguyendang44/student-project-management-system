-- v0.7: create ONLY missing project-progress tables. No DELETE, no DROP, no seed.
-- Targets exact EF entities in CoreEntities.cs. SQL Server LocalDB.
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRAN;
    IF DB_NAME() <> N'StudentProjectsDev' THROW 51000, 'Only StudentProjectsDev is supported by this migration.', 1;
    IF OBJECT_ID(N'dbo.Projects', N'U') IS NULL OR OBJECT_ID(N'dbo.Users', N'U') IS NULL
        THROW 51001, 'Projects / Users tables missing. Apply v0.6 migrations first.', 1;

    IF OBJECT_ID(N'dbo.Milestones', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Milestones (
            Id uniqueidentifier NOT NULL CONSTRAINT PK_Milestones PRIMARY KEY,
            CreatedAt datetimeoffset(7) NOT NULL,
            UpdatedAt datetimeoffset(7) NOT NULL,
            ProjectId uniqueidentifier NOT NULL,
            Title nvarchar(300) NOT NULL,
            Description nvarchar(4000) NULL,
            StartAt datetimeoffset(7) NOT NULL,
            DeadlineAt datetimeoffset(7) NOT NULL,
            ExpectedResult nvarchar(2000) NULL,
            PercentComplete int NOT NULL,
            Status nvarchar(32) NOT NULL,
            CONSTRAINT FK_Milestones_Projects_ProjectId FOREIGN KEY(ProjectId) REFERENCES dbo.Projects(Id) ON DELETE NO ACTION,
            CONSTRAINT CK_Milestone_Percent CHECK (PercentComplete BETWEEN 0 AND 100),
            CONSTRAINT CK_Milestone_Dates CHECK (DeadlineAt >= StartAt)
        );
        CREATE INDEX IX_Milestones_ProjectId_DeadlineAt ON dbo.Milestones(ProjectId, DeadlineAt);
    END

    IF OBJECT_ID(N'dbo.ProgressUpdates', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ProgressUpdates (
            Id uniqueidentifier NOT NULL CONSTRAINT PK_ProgressUpdates PRIMARY KEY,
            CreatedAt datetimeoffset(7) NOT NULL,
            UpdatedAt datetimeoffset(7) NOT NULL,
            MilestoneId uniqueidentifier NOT NULL,
            StudentUserId uniqueidentifier NOT NULL,
            PercentComplete int NOT NULL,
            WorkDone nvarchar(4000) NOT NULL,
            Note nvarchar(2000) NULL,
            CONSTRAINT FK_ProgressUpdates_Milestones_MilestoneId FOREIGN KEY(MilestoneId) REFERENCES dbo.Milestones(Id) ON DELETE NO ACTION,
            CONSTRAINT FK_ProgressUpdates_Users_StudentUserId FOREIGN KEY(StudentUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION,
            CONSTRAINT CK_ProgressUpdate_Percent CHECK (PercentComplete BETWEEN 0 AND 100)
        );
        CREATE INDEX IX_ProgressUpdates_MilestoneId_CreatedAt ON dbo.ProgressUpdates(MilestoneId, CreatedAt);
        CREATE INDEX IX_ProgressUpdates_StudentUserId ON dbo.ProgressUpdates(StudentUserId);
    END

    IF OBJECT_ID(N'dbo.MilestoneSubmissions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.MilestoneSubmissions (
            Id uniqueidentifier NOT NULL CONSTRAINT PK_MilestoneSubmissions PRIMARY KEY,
            CreatedAt datetimeoffset(7) NOT NULL,
            UpdatedAt datetimeoffset(7) NOT NULL,
            MilestoneId uniqueidentifier NOT NULL,
            SubmittedByStudentUserId uniqueidentifier NOT NULL,
            ResultSummary nvarchar(4000) NOT NULL,
            DocumentReference nvarchar(1000) NULL,
            SubmittedAt datetimeoffset(7) NOT NULL,
            CONSTRAINT FK_MilestoneSubmissions_Milestones_MilestoneId FOREIGN KEY(MilestoneId) REFERENCES dbo.Milestones(Id) ON DELETE NO ACTION,
            CONSTRAINT FK_MilestoneSubmissions_Users_SubmittedByStudentUserId FOREIGN KEY(SubmittedByStudentUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION
        );
        CREATE INDEX IX_MilestoneSubmissions_MilestoneId_SubmittedAt ON dbo.MilestoneSubmissions(MilestoneId, SubmittedAt);
        CREATE INDEX IX_MilestoneSubmissions_SubmittedByStudentUserId ON dbo.MilestoneSubmissions(SubmittedByStudentUserId);
    END

    IF OBJECT_ID(N'dbo.ProjectEvaluations', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ProjectEvaluations (
            Id uniqueidentifier NOT NULL CONSTRAINT PK_ProjectEvaluations PRIMARY KEY,
            CreatedAt datetimeoffset(7) NOT NULL,
            UpdatedAt datetimeoffset(7) NOT NULL,
            ProjectId uniqueidentifier NOT NULL,
            LecturerUserId uniqueidentifier NOT NULL,
            MilestoneId uniqueidentifier NULL,
            Feedback nvarchar(4000) NULL,
            IsApproved bit NOT NULL,
            CONSTRAINT FK_ProjectEvaluations_Projects_ProjectId FOREIGN KEY(ProjectId) REFERENCES dbo.Projects(Id) ON DELETE NO ACTION,
            CONSTRAINT FK_ProjectEvaluations_Users_LecturerUserId FOREIGN KEY(LecturerUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION,
            CONSTRAINT FK_ProjectEvaluations_Milestones_MilestoneId FOREIGN KEY(MilestoneId) REFERENCES dbo.Milestones(Id) ON DELETE NO ACTION
        );
        CREATE INDEX IX_ProjectEvaluations_ProjectId_CreatedAt ON dbo.ProjectEvaluations(ProjectId, CreatedAt);
        CREATE INDEX IX_ProjectEvaluations_LecturerUserId ON dbo.ProjectEvaluations(LecturerUserId);
        CREATE INDEX IX_ProjectEvaluations_MilestoneId ON dbo.ProjectEvaluations(MilestoneId);
    END
    COMMIT TRAN;
    SELECT 'v0.7 schema OK' AS Result, DB_NAME() AS DatabaseName,
        (SELECT COUNT(*) FROM dbo.Milestones) AS Milestones,
        (SELECT COUNT(*) FROM dbo.ProgressUpdates) AS ProgressUpdates,
        (SELECT COUNT(*) FROM dbo.MilestoneSubmissions) AS MilestoneSubmissions,
        (SELECT COUNT(*) FROM dbo.ProjectEvaluations) AS ProjectEvaluations;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    THROW;
END CATCH;
