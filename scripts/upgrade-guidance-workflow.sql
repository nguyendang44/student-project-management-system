-- Select the existing application database in SSMS before running this file.
USE [StudentProjectsDev];
GO
-- Additive upgrade: existing journals and lecturer confirmations are retained.
SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 51830, 'Select the project database (for example StudentProjectsDev).', 1;
IF OBJECT_ID(N'dbo.GuidanceSessions', N'U') IS NULL OR OBJECT_ID(N'dbo.Milestones', N'U') IS NULL
    OR OBJECT_ID(N'dbo.GitProgressRuns', N'U') IS NULL OR OBJECT_ID(N'dbo.MilestoneCriteria', N'U') IS NULL
    THROW 51831, 'Apply the existing guidance, Git progress and milestone criteria schemas first.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH(N'dbo.GuidanceSessions', N'MilestoneId') IS NULL
        ALTER TABLE dbo.GuidanceSessions ADD MilestoneId uniqueidentifier NULL;
    IF COL_LENGTH(N'dbo.GuidanceSessions', N'GitProgressRunId') IS NULL
        ALTER TABLE dbo.GuidanceSessions ADD GitProgressRunId uniqueidentifier NULL;
    IF COL_LENGTH(N'dbo.GuidanceSessions', N'NextCriterionId') IS NULL
        ALTER TABLE dbo.GuidanceSessions ADD NextCriterionId uniqueidentifier NULL;
    IF COL_LENGTH(N'dbo.GuidanceSessions', N'Questions') IS NULL
        ALTER TABLE dbo.GuidanceSessions ADD Questions nvarchar(4000) NOT NULL
            CONSTRAINT DF_GuidanceSessions_Questions DEFAULT (N'') WITH VALUES;
    IF COL_LENGTH(N'dbo.GuidanceSessions', N'ReviewStatus') IS NULL
        ALTER TABLE dbo.GuidanceSessions ADD ReviewStatus nvarchar(24) NOT NULL
            CONSTRAINT DF_GuidanceSessions_ReviewStatus DEFAULT (N'PENDING') WITH VALUES;
    -- Dynamic SQL compiles after the optional columns above have been created.
    EXEC(N'UPDATE dbo.GuidanceSessions SET ReviewStatus = N''CONFIRMED''
        WHERE LecturerConfirmedAt IS NOT NULL AND ReviewStatus = N''PENDING'';');
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_GuidanceSessions_Milestone')
        EXEC(N'ALTER TABLE dbo.GuidanceSessions ADD CONSTRAINT FK_GuidanceSessions_Milestone
            FOREIGN KEY (MilestoneId) REFERENCES dbo.Milestones(Id);');
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_GuidanceSessions_GitProgressRun')
        EXEC(N'ALTER TABLE dbo.GuidanceSessions ADD CONSTRAINT FK_GuidanceSessions_GitProgressRun
            FOREIGN KEY (GitProgressRunId) REFERENCES dbo.GitProgressRuns(Id);');
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_GuidanceSessions_NextCriterion')
        EXEC(N'ALTER TABLE dbo.GuidanceSessions ADD CONSTRAINT FK_GuidanceSessions_NextCriterion
            FOREIGN KEY (NextCriterionId) REFERENCES dbo.MilestoneCriteria(Id);');
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_GuidanceSessions_ReviewStatus')
        EXEC(N'ALTER TABLE dbo.GuidanceSessions ADD CONSTRAINT CK_GuidanceSessions_ReviewStatus
            CHECK (ReviewStatus IN (N''PENDING'',N''CONFIRMED'',N''CHANGES_REQUESTED''));');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.GuidanceSessions') AND name = N'IX_GuidanceSessions_MilestoneId')
        EXEC(N'CREATE INDEX IX_GuidanceSessions_MilestoneId ON dbo.GuidanceSessions(MilestoneId);');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.GuidanceSessions') AND name = N'IX_GuidanceSessions_GitProgressRunId')
        EXEC(N'CREATE INDEX IX_GuidanceSessions_GitProgressRunId ON dbo.GuidanceSessions(GitProgressRunId);');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.GuidanceSessions') AND name = N'IX_GuidanceSessions_NextCriterionId')
        EXEC(N'CREATE INDEX IX_GuidanceSessions_NextCriterionId ON dbo.GuidanceSessions(NextCriterionId);');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
