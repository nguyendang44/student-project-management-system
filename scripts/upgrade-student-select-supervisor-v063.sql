-- v0.6.3: Lecturer approval becomes OFFERED; student selects a single supervisor.
-- Preserve all old requests/projects. Recover lecturer global max from highest
-- historically configured period (e.g. 10 in period 1 and 7 in period 2 -> 10).
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.LecturerRequests', N'U') IS NULL
       OR OBJECT_ID(N'dbo.LecturerCapacities', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Projects', N'U') IS NULL
        THROW 51063, 'Run v0.6 and v0.6.1 database upgrades before v0.6.3.', 1;
    IF EXISTS (SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.LecturerRequests')
          AND name = N'CK_LecturerRequests_Status')
        ALTER TABLE dbo.LecturerRequests DROP CONSTRAINT CK_LecturerRequests_Status;
    ALTER TABLE dbo.LecturerRequests WITH CHECK ADD CONSTRAINT CK_LecturerRequests_Status
       CHECK (Status IN ('PENDING','OFFERED','ACCEPTED','REJECTED','CANCELLED','REVISION_REQUIRED'));

    -- Adjust legacy CurrentStudents caches safely, not by period.
    ;WITH totals AS (
        SELECT LecturerUserId,
               COUNT(*) AS ActiveCount
        FROM dbo.Projects
        WHERE Status NOT IN ('CANCELLED','COMPLETED')
        GROUP BY LecturerUserId
    ), limits AS (
        SELECT LecturerUserId, MAX(MaxStudents) AS HistoricalMax
        FROM dbo.LecturerCapacities
        GROUP BY LecturerUserId
    )
    UPDATE c
        SET c.MaxStudents = CASE WHEN COALESCE(t.ActiveCount,0) > l.HistoricalMax
                                 THEN t.ActiveCount ELSE l.HistoricalMax END,
            c.CurrentStudents = COALESCE(t.ActiveCount,0),
            c.UpdatedAt = SYSUTCDATETIME()
    FROM dbo.LecturerCapacities c
    JOIN limits l ON l.LecturerUserId = c.LecturerUserId
    LEFT JOIN totals t ON t.LecturerUserId = c.LecturerUserId;

    COMMIT TRANSACTION;
    PRINT 'OK: v0.6.3 supervisor selection and global capacity upgrade applied.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
