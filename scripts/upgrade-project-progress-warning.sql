USE [StudentProjectsDev];
GO
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Projects', N'U') IS NULL
        THROW 51825, 'Select StudentProjectsDev containing the existing Projects table.', 1;
    IF COL_LENGTH(N'dbo.Projects', N'DeadlineAt') IS NULL
        THROW 51826, 'Apply the project deadline schema before this upgrade.', 1;
    IF COL_LENGTH(N'dbo.Projects', N'StartAt') IS NULL
        ALTER TABLE dbo.Projects ADD StartAt datetimeoffset(7) NULL;
    -- Existing rows keep a null start date. The lecturer selects the actual planned start.
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
