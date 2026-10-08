-- StudentProjectsDev / SQL Server LocalDB, non-destructive schema change.
-- Allows multiple PENDING requests per student across DIFFERENT topics.
-- Active duplicate requests for the same (StudentUserId, TopicId) remain prohibited.
-- Apply ONCE AFTER BACKUP before restarting the updated backend.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TopicRegistrations', N'U') IS NULL
        THROW 51000, 'TopicRegistrations does not exist. Database unchanged.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.TopicRegistrations
        WHERE [Status] IN ('PENDING', 'ACCEPTED')
        GROUP BY [StudentUserId], [TopicId]
        HAVING COUNT_BIG(*) > 1
    )
        THROW 51001, 'Existing duplicate active student/topic registrations detected. Database unchanged. Resolve duplicates manually.', 1;

    -- v0.5's manually generated topic migration names this index _Active.
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TopicRegistrations')
        AND name = N'IX_TopicRegistrations_StudentUserId_RegistrationPeriodId_Active')
        DROP INDEX [IX_TopicRegistrations_StudentUserId_RegistrationPeriodId_Active]
            ON dbo.TopicRegistrations;

    -- Also support v0.5 databases created directly from the EF model.
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TopicRegistrations')
        AND name = N'IX_TopicRegistrations_StudentUserId_RegistrationPeriodId')
        DROP INDEX [IX_TopicRegistrations_StudentUserId_RegistrationPeriodId]
            ON dbo.TopicRegistrations;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TopicRegistrations')
        AND name = N'IX_TopicRegistrations_StudentUserId_TopicId')
        CREATE UNIQUE NONCLUSTERED INDEX [IX_TopicRegistrations_StudentUserId_TopicId]
            ON dbo.TopicRegistrations([StudentUserId], [TopicId])
            WHERE [Status] IN ('PENDING', 'ACCEPTED');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
