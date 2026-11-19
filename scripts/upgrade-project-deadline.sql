USE [StudentProjectsDev];
GO
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Projects', N'U') IS NULL
        THROW 51821, 'Select the existing StudentProjectsDev database with the Projects table.', 1;
    IF OBJECT_ID(N'dbo.MilestoneCriteria', N'U') IS NULL
        THROW 51822, 'Apply upgrade-milestone-criteria.sql before this upgrade.', 1;
    IF COL_LENGTH(N'dbo.Projects', N'DeadlineAt') IS NULL
        ALTER TABLE dbo.Projects ADD DeadlineAt datetimeoffset(7) NULL;
    IF COL_LENGTH(N'dbo.MilestoneCriteria', N'DeadlineAt') IS NULL
        ALTER TABLE dbo.MilestoneCriteria ADD DeadlineAt datetimeoffset(7) NULL;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
