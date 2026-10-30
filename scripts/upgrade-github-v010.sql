-- StudentProjects v0.10 - SQL Server schema repair (safe, idempotent).
-- Run against (localdb)\MSSQLLocalDB after backing up StudentProjectsDev.
-- Does NOT delete or reset any existing application data.
USE [StudentProjectsDev];
GO
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF DB_NAME() <> N'StudentProjectsDev'
        THROW 51000, N'Wrong database. Expected StudentProjectsDev.', 1;

    IF OBJECT_ID(N'dbo.Projects', N'U') IS NULL
        THROW 51001, N'dbo.Projects does not exist. Stop and inspect the database.', 1;

    IF OBJECT_ID(N'dbo.Repositories', N'U') IS NULL
    BEGIN
        -- Execute DDL separately so the rest of the batch does not have to
        -- compile references to a table which does not exist yet.
        EXEC sys.sp_executesql N'
            CREATE TABLE [dbo].[Repositories]
            (
                [Id] UNIQUEIDENTIFIER NOT NULL,
                [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
                [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
                [ProjectId] UNIQUEIDENTIFIER NOT NULL,
                [RepositoryUrl] NVARCHAR(1000) NOT NULL,
                [DefaultBranch] NVARCHAR(200) NULL,
                [LastCommitAt] DATETIMEOFFSET(7) NULL,
                CONSTRAINT [PK_Repositories] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_Repositories_Projects_ProjectId]
                    FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects]([Id])
                    ON DELETE NO ACTION
            );';
    END;

    -- Never mutate a pre-existing incompatible table.
    IF OBJECT_ID(N'dbo.Repositories', N'U') IS NULL OR
       COL_LENGTH(N'dbo.Repositories', N'Id') IS NULL OR
       COL_LENGTH(N'dbo.Repositories', N'ProjectId') IS NULL OR
       COL_LENGTH(N'dbo.Repositories', N'RepositoryUrl') IS NULL OR
       COL_LENGTH(N'dbo.Repositories', N'DefaultBranch') IS NULL OR
       COL_LENGTH(N'dbo.Repositories', N'LastCommitAt') IS NULL OR
       COL_LENGTH(N'dbo.Repositories', N'CreatedAt') IS NULL OR
       COL_LENGTH(N'dbo.Repositories', N'UpdatedAt') IS NULL
        THROW 51002, N'Existing dbo.Repositories schema differs. No changes committed.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Repositories')
          AND name = N'IX_Repositories_ProjectId'
    )
        EXEC sys.sp_executesql N'
            CREATE INDEX [IX_Repositories_ProjectId]
            ON [dbo].[Repositories]([ProjectId]);';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Repositories')
          AND name = N'UX_Repositories_Project_Url'
    )
        EXEC sys.sp_executesql N'
            CREATE UNIQUE INDEX [UX_Repositories_Project_Url]
            ON [dbo].[Repositories]([ProjectId], [RepositoryUrl]);';

    COMMIT TRANSACTION;

    EXEC sys.sp_executesql N'
        SELECT N''v0.10 GitHub schema ready'' AS Result,
               DB_NAME() AS DatabaseName,
               COUNT(*) AS ExistingRepositoryLinks
        FROM [dbo].[Repositories];';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
