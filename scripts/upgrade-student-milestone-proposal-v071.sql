-- v0.7.1 SQL hotfix: SQL Server cannot bind newly added column names
-- inside statements compiled in the SAME outer batch.
-- Only schema changes. Existing milestones/projects/rows remain untouched.
-- Safe to rerun; no EF code modifications, no DROP/DELETE/UPDATE of business data.
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF DB_NAME() <> N'StudentProjectsDev'
        THROW 51710, 'Migration is limited to StudentProjectsDev.', 1;

    IF OBJECT_ID(N'dbo.Milestones', N'U') IS NULL
        THROW 51711, 'Milestones table missing. Install and run v0.7 SQL upgrade first.', 1;

    IF COL_LENGTH(N'dbo.Milestones', N'ProposedByStudent') IS NULL
    BEGIN
        ALTER TABLE dbo.Milestones
          ADD ProposedByStudent bit NOT NULL
          CONSTRAINT DF_Milestones_ProposedByStudent DEFAULT (0) WITH VALUES;
    END;

    IF COL_LENGTH(N'dbo.Milestones', N'ProposalStatus') IS NULL
    BEGIN
        ALTER TABLE dbo.Milestones
          ADD ProposalStatus nvarchar(32) NOT NULL
          CONSTRAINT DF_Milestones_ProposalStatus DEFAULT (N'CONFIRMED') WITH VALUES;
    END;

    IF COL_LENGTH(N'dbo.Milestones', N'ProposalFeedback') IS NULL
    BEGIN
        ALTER TABLE dbo.Milestones
          ADD ProposalFeedback nvarchar(2000) NULL;
    END;

    -- IMPORTANT: Check constraint must compile AFTER the ALTER TABLE above.
    IF NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Milestones')
          AND name = N'CK_Milestones_ProposalStatus'
    )
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.Milestones WITH CHECK
              ADD CONSTRAINT CK_Milestones_ProposalStatus
              CHECK (ProposalStatus IN (N''PENDING_REVIEW'', N''CHANGES_REQUESTED'', N''CONFIRMED''));
        ';
    END;

    -- Compile the result query only after the new columns exist.
    -- Keep the same Result/DatabaseName/MilestoneCount/StudentProposedCount
    -- fields that the existing PowerShell installer expects.
    EXEC sys.sp_executesql N'
        SELECT N''v0.7.1 milestone proposal schema OK'' AS Result,
               DB_NAME() AS DatabaseName,
               (SELECT COUNT(*) FROM dbo.Milestones) AS MilestoneCount,
               (SELECT COUNT(*) FROM dbo.Milestones WHERE ProposedByStudent = 1) AS StudentProposedCount;
    ';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
