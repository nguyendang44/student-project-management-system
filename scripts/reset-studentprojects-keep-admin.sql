-- StudentProjectsDev safe data reset (keep all Admin accounts, Roles, EF migrations).
-- Run only through reset-studentprojects-keep-admin.ps1.
-- @Apply bit is injected by the PowerShell runner; 0=preview, 1=delete.
-- Unknown tables or FKs to preserved tables abort BEFORE any changes.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'StudentProjectsDev'
    THROW 51020, 'Wrong database. Only StudentProjectsDev is supported.', 1;
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL OR OBJECT_ID(N'dbo.Roles', N'U') IS NULL
    THROW 51021, 'Users or Roles table is missing.', 1;
IF NOT EXISTS
(
    SELECT 1 FROM dbo.Users u
    INNER JOIN dbo.Roles r ON r.Id = u.RoleId
    WHERE r.Name = N'Admin' AND u.IsActive = 1 AND NULLIF(u.PasswordHash, N'') IS NOT NULL
)
    THROW 51022, 'No active Admin with a password hash was found. Nothing was deleted.', 1;

-- These are the table names defined by the StudentProjects entity model.
DECLARE @Known TABLE (Name sysname NOT NULL PRIMARY KEY);
INSERT INTO @Known (Name) VALUES
(N'Roles'),(N'Users'),(N'__EFMigrationsHistory'),
(N'Students'),(N'Lecturers'),(N'AuditEntries'),
(N'RegistrationPeriods'),(N'LecturerCapacities'),
(N'Topics'),(N'TopicStateHistories'),(N'TopicRegistrations'),
(N'LecturerRequests'),(N'Projects'),
(N'Milestones'),(N'ProgressUpdates'),(N'MilestoneSubmissions'),
(N'ProjectEvaluations'),(N'Repositories'),(N'CodeAnalysisReports'),
(N'Notifications'),(N'AutomationRuns'),(N'SystemErrors'),(N'SystemSettings');

DECLARE @Unknown nvarchar(max);
SELECT @Unknown = STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)), N', ')
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.is_ms_shipped = 0
  AND (s.name <> N'dbo' OR NOT EXISTS (SELECT 1 FROM @Known k WHERE k.Name = t.name));
IF @Unknown IS NOT NULL
BEGIN
    DECLARE @Error nvarchar(2048) = N'Unexpected tables. STOP. Review before wiping: ' + LEFT(@Unknown, 1850);
    THROW 51023, @Error, 1;
END;

CREATE TABLE #Targets
(
    ObjectId int NOT NULL PRIMARY KEY,
    TableName sysname NOT NULL,
    Processed bit NOT NULL DEFAULT 0
);
INSERT INTO #Targets (ObjectId, TableName)
SELECT t.object_id, t.name
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0 AND s.name = N'dbo'
  AND t.name NOT IN (N'Users', N'Roles', N'__EFMigrationsHistory');

-- Preserved entities must not have FK dependencies pointing into tables being cleared.
IF EXISTS
(
    SELECT 1 FROM sys.foreign_keys fk
    JOIN #Targets parent ON parent.ObjectId = fk.referenced_object_id
    WHERE fk.parent_object_id IN (OBJECT_ID(N'dbo.Users'), OBJECT_ID(N'dbo.Roles'),
                                  OBJECT_ID(N'dbo.__EFMigrationsHistory'))
)
    THROW 51024, 'A preserved table references a table to be cleared. Aborted.', 1;

CREATE TABLE #RowCounts
(
    TableName sysname NOT NULL PRIMARY KEY,
    RowsBefore bigint NOT NULL DEFAULT 0,
    RowsDeleted bigint NOT NULL DEFAULT 0
);
DECLARE @Name sysname, @Command nvarchar(max), @Count bigint;
DECLARE target_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT TableName FROM #Targets ORDER BY TableName;
OPEN target_cursor;
FETCH NEXT FROM target_cursor INTO @Name;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Command = N'SELECT @N = COUNT_BIG(*) FROM dbo.' + QUOTENAME(@Name) + N';';
    EXEC sp_executesql @Command, N'@N bigint OUTPUT', @N = @Count OUTPUT;
    INSERT INTO #RowCounts (TableName, RowsBefore) VALUES (@Name, @Count);
    FETCH NEXT FROM target_cursor INTO @Name;
END;
CLOSE target_cursor;
DEALLOCATE target_cursor;

DECLARE @OtherUsers bigint, @Admins bigint;
SELECT @OtherUsers = COUNT_BIG(*)
FROM dbo.Users u INNER JOIN dbo.Roles r ON r.Id = u.RoleId WHERE r.Name <> N'Admin';
SELECT @Admins = COUNT_BIG(*)
FROM dbo.Users u INNER JOIN dbo.Roles r ON r.Id = u.RoleId WHERE r.Name = N'Admin';
INSERT INTO #RowCounts (TableName, RowsBefore) VALUES (N'Users (non-Admin)', @OtherUsers);

IF @Apply = 1
BEGIN
    BEGIN TRY
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        BEGIN TRANSACTION;
        -- Ensure an Admin still exists after the backup; concurrent writes wait on this table lock.
        DECLARE @CurrentAdmins bigint;
        SELECT @CurrentAdmins = COUNT_BIG(*) FROM dbo.Users u WITH (TABLOCKX, HOLDLOCK)
            INNER JOIN dbo.Roles r ON r.Id = u.RoleId WHERE r.Name = N'Admin';
        IF @CurrentAdmins <> @Admins OR @CurrentAdmins < 1
            THROW 51025, 'Admin accounts changed since preview. Rolled back.', 1;

        DECLARE @ObjectId int, @Deleted bigint;
        WHILE EXISTS (SELECT 1 FROM #Targets WHERE Processed = 0)
        BEGIN
            SET @ObjectId = NULL;
            -- Delete child rows before parent rows without disabling FK constraints.
            SELECT TOP (1) @ObjectId = parent.ObjectId, @Name = parent.TableName
            FROM #Targets parent
            WHERE parent.Processed = 0
              AND NOT EXISTS
              (
                  SELECT 1 FROM sys.foreign_keys fk
                  JOIN #Targets child ON child.ObjectId = fk.parent_object_id AND child.Processed = 0
                  WHERE fk.referenced_object_id = parent.ObjectId
                    AND fk.parent_object_id <> parent.ObjectId
              )
            ORDER BY parent.TableName;
            IF @ObjectId IS NULL
                THROW 51026, 'Cyclic FK dependencies detected. Rolled back without changing schema.', 1;

            SET @Command = N'DELETE FROM dbo.' + QUOTENAME(@Name) + N'; SELECT @N = @@ROWCOUNT;';
            EXEC sp_executesql @Command, N'@N bigint OUTPUT', @N = @Deleted OUTPUT;
            UPDATE #Targets SET Processed = 1 WHERE ObjectId = @ObjectId;
            UPDATE #RowCounts SET RowsDeleted = @Deleted WHERE TableName = @Name;
        END;

        DELETE u
        FROM dbo.Users u
        INNER JOIN dbo.Roles r ON r.Id = u.RoleId
        WHERE r.Name <> N'Admin';
        SET @Deleted = @@ROWCOUNT;
        UPDATE #RowCounts SET RowsDeleted = @Deleted WHERE TableName = N'Users (non-Admin)';

        -- Verify no rows remain in business tables; Admin IDs, hashes and roles were untouched.
        SELECT @CurrentAdmins = COUNT_BIG(*)
        FROM dbo.Users u JOIN dbo.Roles r ON r.Id = u.RoleId WHERE r.Name = N'Admin';
        IF @CurrentAdmins <> @Admins
            THROW 51027, 'Admin count changed during reset. Rolled back.', 1;
        IF EXISTS (SELECT 1 FROM #RowCounts WHERE RowsDeleted <> RowsBefore)
            THROW 51028, 'Row count mismatch. Rolled back.', 1;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;

SELECT TableName AS [Table], RowsBefore AS [RowsBefore], RowsDeleted AS [RowsDeleted]
FROM #RowCounts ORDER BY TableName;
SELECT CASE WHEN @Apply = 1 THEN N'APPLIED' ELSE N'PREVIEW' END AS [Mode],
    @Admins AS [AdminAccountsPreserved],
    (SELECT COUNT_BIG(*) FROM dbo.Roles) AS [RolesPreserved],
    (SELECT COUNT_BIG(*) FROM dbo.Users) AS [UsersRemaining];
