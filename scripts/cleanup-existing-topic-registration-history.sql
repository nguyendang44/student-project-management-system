-- One-time v0.5 cleanup: remove legacy registration history for students
-- who currently own EXACTLY ONE topic. Preserve ACCEPTED registrations.
-- Requires SQL Server 2016+; run against StudentProjectsDev after backing up.
-- @Apply is supplied by the PowerShell wrapper (0 = preview, 1 = delete).
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.TopicRegistrations', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Topics', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Users', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Roles', N'U') IS NULL
    THROW 51000, 'Required tables missing; no data changed.', 1;

-- Create temp table before transaction so preview can be safely rolled back.
CREATE TABLE #ToDelete (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    StudentUserId uniqueidentifier NOT NULL
);
DECLARE @Found int = 0;
DECLARE @Students int = 0;
DECLARE @Deleted int = 0;

SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

;WITH Owners AS (
    -- Registration-based ownership.
    SELECT r.StudentUserId
    FROM dbo.TopicRegistrations AS r
    WHERE r.Status = 'ACCEPTED'
    UNION ALL
    -- Approved student-authored proposals represent ownership without an
    -- ACCEPTED registration. Do not count proposals by staff members.
    SELECT t.ProposedByUserId AS StudentUserId
    FROM dbo.Topics AS t
    INNER JOIN dbo.Users AS u ON u.Id = t.ProposedByUserId
    INNER JOIN dbo.Roles AS role ON role.Id = u.RoleId
    WHERE role.Name = 'Student'
      AND t.Status IN ('APPROVED', 'IN_PROGRESS', 'COMPLETED')
), ExactlyOneOwner AS (
    SELECT StudentUserId
    FROM Owners
    GROUP BY StudentUserId
    HAVING COUNT_BIG(*) = 1
)
INSERT INTO #ToDelete (Id, StudentUserId)
SELECT r.Id, r.StudentUserId
FROM dbo.TopicRegistrations AS r WITH (TABLOCKX, HOLDLOCK)
INNER JOIN ExactlyOneOwner AS o ON o.StudentUserId = r.StudentUserId
WHERE r.Status <> 'ACCEPTED';

SELECT @Found = COUNT(*), @Students = COUNT(DISTINCT StudentUserId)
FROM #ToDelete;

IF @Apply = 1
BEGIN
    DELETE r
    FROM dbo.TopicRegistrations AS r
    INNER JOIN #ToDelete AS d ON d.Id = r.Id;
    SET @Deleted = @@ROWCOUNT;
    IF @Deleted <> @Found
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51001, 'Cleanup row count mismatch; transaction rolled back.', 1;
    END;
    COMMIT TRANSACTION;
END
ELSE
BEGIN
    ROLLBACK TRANSACTION;
END;

SELECT CASE WHEN @Apply = 1 THEN 'APPLIED' ELSE 'PREVIEW' END AS [Mode],
       @Students AS [AffectedStudents],
       @Found AS [OldRegistrationsFound],
       @Deleted AS [DeletedRegistrations];
