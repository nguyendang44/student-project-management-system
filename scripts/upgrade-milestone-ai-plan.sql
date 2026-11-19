USE [StudentProjectsDev];
GO
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.MilestoneCriteria', N'U') IS NULL
        THROW 51823, 'Apply the milestone criteria upgrade first.', 1;
    IF OBJECT_ID(N'dbo.Projects', N'U') IS NULL
        THROW 51824, 'Select the existing StudentProjectsDev database.', 1;
    IF COL_LENGTH(N'dbo.Projects', N'DeadlineAt') IS NULL
        ALTER TABLE dbo.Projects ADD DeadlineAt datetimeoffset(7) NULL;
    IF COL_LENGTH(N'dbo.MilestoneCriteria', N'DeadlineAt') IS NULL
        ALTER TABLE dbo.MilestoneCriteria ADD DeadlineAt datetimeoffset(7) NULL;
    IF COL_LENGTH(N'dbo.MilestoneCriteria', N'IsDeleted') IS NULL
        ALTER TABLE dbo.MilestoneCriteria ADD IsDeleted bit NOT NULL
            CONSTRAINT DF_MilestoneCriteria_IsDeleted DEFAULT (0) WITH VALUES;
    IF COL_LENGTH(N'dbo.MilestoneCriteria', N'DefinitionChangedAt') IS NULL
        ALTER TABLE dbo.MilestoneCriteria ADD DefinitionChangedAt datetimeoffset(7) NULL;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
