-- StudentProjectsDev v0.6: lecturer capacity, supervision requests, registered projects.
-- Requires v0.5 tables: Users, Topics, RegistrationPeriods. Idempotent; does not delete old data.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Users', N'U') IS NULL OR OBJECT_ID(N'dbo.Topics', N'U') IS NULL
       OR OBJECT_ID(N'dbo.RegistrationPeriods', N'U') IS NULL
        THROW 51006, 'Apply the v0.5 schema before running v0.6 upgrade.', 1;

    IF OBJECT_ID(N'dbo.LecturerCapacities', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.LecturerCapacities (
            Id uniqueidentifier NOT NULL CONSTRAINT PK_LecturerCapacities PRIMARY KEY,
            CreatedAt datetimeoffset(7) NOT NULL, UpdatedAt datetimeoffset(7) NOT NULL,
            LecturerUserId uniqueidentifier NOT NULL,
            RegistrationPeriodId uniqueidentifier NOT NULL,
            MaxStudents int NOT NULL, CurrentStudents int NOT NULL,
            RowVersion rowversion NOT NULL,
            CONSTRAINT FK_LecturerCapacities_Users_LecturerUserId FOREIGN KEY (LecturerUserId) REFERENCES dbo.Users(Id),
            CONSTRAINT FK_LecturerCapacities_RegistrationPeriods_RegistrationPeriodId FOREIGN KEY (RegistrationPeriodId) REFERENCES dbo.RegistrationPeriods(Id),
            CONSTRAINT CK_LecturerCapacity_Bounds CHECK (MaxStudents >= 0 AND CurrentStudents >= 0 AND CurrentStudents <= MaxStudents)
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.LecturerCapacities') AND name=N'IX_LecturerCapacities_LecturerUserId_RegistrationPeriodId')
        CREATE UNIQUE INDEX IX_LecturerCapacities_LecturerUserId_RegistrationPeriodId
            ON dbo.LecturerCapacities(LecturerUserId, RegistrationPeriodId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.LecturerCapacities') AND name=N'IX_LecturerCapacities_RegistrationPeriodId')
        CREATE INDEX IX_LecturerCapacities_RegistrationPeriodId ON dbo.LecturerCapacities(RegistrationPeriodId);

    IF OBJECT_ID(N'dbo.LecturerRequests', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.LecturerRequests (
            Id uniqueidentifier NOT NULL CONSTRAINT PK_LecturerRequests PRIMARY KEY,
            CreatedAt datetimeoffset(7) NOT NULL, UpdatedAt datetimeoffset(7) NOT NULL,
            StudentUserId uniqueidentifier NOT NULL,
            LecturerUserId uniqueidentifier NOT NULL,
            TopicId uniqueidentifier NOT NULL,
            RegistrationPeriodId uniqueidentifier NOT NULL,
            Status nvarchar(32) NOT NULL,
            RejectionReason nvarchar(2000) NULL,
            CONSTRAINT FK_LecturerRequests_Users_StudentUserId FOREIGN KEY (StudentUserId) REFERENCES dbo.Users(Id),
            CONSTRAINT FK_LecturerRequests_Users_LecturerUserId FOREIGN KEY (LecturerUserId) REFERENCES dbo.Users(Id),
            CONSTRAINT FK_LecturerRequests_Topics_TopicId FOREIGN KEY (TopicId) REFERENCES dbo.Topics(Id),
            CONSTRAINT FK_LecturerRequests_RegistrationPeriods_RegistrationPeriodId FOREIGN KEY (RegistrationPeriodId) REFERENCES dbo.RegistrationPeriods(Id),
            CONSTRAINT CK_LecturerRequests_Status CHECK (Status IN ('PENDING','ACCEPTED','REJECTED','CANCELLED'))
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.LecturerRequests') AND name=N'IX_LecturerRequests_LecturerUserId_RegistrationPeriodId_Status')
        CREATE INDEX IX_LecturerRequests_LecturerUserId_RegistrationPeriodId_Status ON dbo.LecturerRequests(LecturerUserId, RegistrationPeriodId, Status);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.LecturerRequests') AND name=N'IX_LecturerRequests_StudentUserId_RegistrationPeriodId_Status')
        CREATE INDEX IX_LecturerRequests_StudentUserId_RegistrationPeriodId_Status ON dbo.LecturerRequests(StudentUserId, RegistrationPeriodId, Status);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.LecturerRequests') AND name=N'IX_LecturerRequests_TopicId')
        CREATE INDEX IX_LecturerRequests_TopicId ON dbo.LecturerRequests(TopicId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.LecturerRequests') AND name=N'UX_LecturerRequests_OneAcceptedPerStudent')
        CREATE UNIQUE INDEX UX_LecturerRequests_OneAcceptedPerStudent ON dbo.LecturerRequests(StudentUserId) WHERE Status = 'ACCEPTED';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.LecturerRequests') AND name=N'UX_LecturerRequests_OnePendingPerStudentLecturerTopicPeriod')
        CREATE UNIQUE INDEX UX_LecturerRequests_OnePendingPerStudentLecturerTopicPeriod
            ON dbo.LecturerRequests(StudentUserId, LecturerUserId, TopicId, RegistrationPeriodId) WHERE Status = 'PENDING';

    IF OBJECT_ID(N'dbo.Projects', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Projects (
            Id uniqueidentifier NOT NULL CONSTRAINT PK_Projects PRIMARY KEY,
            CreatedAt datetimeoffset(7) NOT NULL, UpdatedAt datetimeoffset(7) NOT NULL,
            TopicId uniqueidentifier NOT NULL,
            StudentUserId uniqueidentifier NOT NULL,
            LecturerUserId uniqueidentifier NOT NULL,
            RegistrationPeriodId uniqueidentifier NOT NULL,
            AcceptedLecturerRequestId uniqueidentifier NULL,
            Description nvarchar(4000) NULL,
            Status nvarchar(32) NOT NULL,
            CONSTRAINT FK_Projects_Topics_TopicId FOREIGN KEY (TopicId) REFERENCES dbo.Topics(Id),
            CONSTRAINT FK_Projects_Users_StudentUserId FOREIGN KEY (StudentUserId) REFERENCES dbo.Users(Id),
            CONSTRAINT FK_Projects_Users_LecturerUserId FOREIGN KEY (LecturerUserId) REFERENCES dbo.Users(Id),
            CONSTRAINT FK_Projects_RegistrationPeriods_RegistrationPeriodId FOREIGN KEY (RegistrationPeriodId) REFERENCES dbo.RegistrationPeriods(Id),
            CONSTRAINT FK_Projects_LecturerRequests_AcceptedLecturerRequestId FOREIGN KEY (AcceptedLecturerRequestId) REFERENCES dbo.LecturerRequests(Id),
            CONSTRAINT CK_Projects_Status CHECK (Status IN ('DRAFT','REGISTERED','IN_PROGRESS','COMPLETED','CANCELLED'))
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Projects') AND name=N'IX_Projects_AcceptedLecturerRequestId')
        CREATE UNIQUE INDEX IX_Projects_AcceptedLecturerRequestId ON dbo.Projects(AcceptedLecturerRequestId) WHERE AcceptedLecturerRequestId IS NOT NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Projects') AND name=N'IX_Projects_StudentUserId_RegistrationPeriodId')
        CREATE INDEX IX_Projects_StudentUserId_RegistrationPeriodId ON dbo.Projects(StudentUserId, RegistrationPeriodId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Projects') AND name=N'UX_Projects_OneActiveProjectPerStudent')
        CREATE UNIQUE INDEX UX_Projects_OneActiveProjectPerStudent ON dbo.Projects(StudentUserId) WHERE Status <> 'CANCELLED';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Projects') AND name=N'UX_Projects_OneActiveProjectPerTopic')
        CREATE UNIQUE INDEX UX_Projects_OneActiveProjectPerTopic ON dbo.Projects(TopicId) WHERE Status <> 'CANCELLED';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Projects') AND name=N'IX_Projects_LecturerUserId')
        CREATE INDEX IX_Projects_LecturerUserId ON dbo.Projects(LecturerUserId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Projects') AND name=N'IX_Projects_TopicId')
        CREATE INDEX IX_Projects_TopicId ON dbo.Projects(TopicId);

    COMMIT TRANSACTION;
    PRINT 'OK: v0.6 supervision schema installed. No existing records deleted.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
