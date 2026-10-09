-- v0.6.1: joint topic + lecturer application with revisions.
-- Safe to rerun. Keeps existing LecturerRequests rows and all projects.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.LecturerRequests', N'U') IS NULL
        THROW 51061, 'Apply upgrade-supervision-v06.ps1 first.', 1;

    IF COL_LENGTH(N'dbo.LecturerRequests', N'IsCombined') IS NULL
        ALTER TABLE dbo.LecturerRequests ADD IsCombined bit NOT NULL
            CONSTRAINT DF_LecturerRequests_IsCombined DEFAULT (0);
    IF COL_LENGTH(N'dbo.LecturerRequests', N'DraftTitle') IS NULL
        ALTER TABLE dbo.LecturerRequests ADD DraftTitle nvarchar(300) NULL;
    IF COL_LENGTH(N'dbo.LecturerRequests', N'DraftDescription') IS NULL
        ALTER TABLE dbo.LecturerRequests ADD DraftDescription nvarchar(4000) NULL;
    IF COL_LENGTH(N'dbo.LecturerRequests', N'DraftObjective') IS NULL
        ALTER TABLE dbo.LecturerRequests ADD DraftObjective nvarchar(2000) NULL;
    IF COL_LENGTH(N'dbo.LecturerRequests', N'DraftExpectedContent') IS NULL
        ALTER TABLE dbo.LecturerRequests ADD DraftExpectedContent nvarchar(2000) NULL;
    IF COL_LENGTH(N'dbo.LecturerRequests', N'DraftProposedTechnology') IS NULL
        ALTER TABLE dbo.LecturerRequests ADD DraftProposedTechnology nvarchar(1000) NULL;

    -- v0.6 allowed 4 statuses. Permit the additional revision workflow status.
    IF EXISTS (SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.LecturerRequests') AND name = N'CK_LecturerRequests_Status')
        ALTER TABLE dbo.LecturerRequests DROP CONSTRAINT CK_LecturerRequests_Status;
    ALTER TABLE dbo.LecturerRequests WITH CHECK ADD CONSTRAINT CK_LecturerRequests_Status
        CHECK (Status IN ('PENDING','ACCEPTED','REJECTED','CANCELLED','REVISION_REQUIRED'));
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
