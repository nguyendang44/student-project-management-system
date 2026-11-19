USE [StudentProjectsDev];

SELECT
    DB_NAME() AS CurrentDatabase,
    OBJECT_ID(N'dbo.Milestones', N'U') AS Milestones,
    OBJECT_ID(N'dbo.GitProgressRuns', N'U') AS GitProgressRuns;
-- Select the application's development database before executing.
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Milestones', N'U') IS NULL OR OBJECT_ID(N'dbo.GitProgressRuns', N'U') IS NULL
    THROW 51820, 'Select the project database and apply the Git progress schema first.', 1;

IF OBJECT_ID(N'dbo.MilestoneCriteria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MilestoneCriteria (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_MilestoneCriteria PRIMARY KEY,
        MilestoneId uniqueidentifier NOT NULL REFERENCES dbo.Milestones(Id),
        Title nvarchar(200) NOT NULL,
        AcceptanceCriteria nvarchar(250) NOT NULL,
        Points int NOT NULL CHECK (Points BETWEEN 1 AND 100),
        CreatedAt datetimeoffset NOT NULL,
        UpdatedAt datetimeoffset NOT NULL
    );
    CREATE INDEX IX_MilestoneCriteria_MilestoneId ON dbo.MilestoneCriteria(MilestoneId);
END;

IF OBJECT_ID(N'dbo.MilestoneCriterionAssessments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MilestoneCriterionAssessments (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_MilestoneCriterionAssessments PRIMARY KEY,
        CriterionId uniqueidentifier NOT NULL REFERENCES dbo.MilestoneCriteria(Id),
        ActorUserId uniqueidentifier NOT NULL REFERENCES dbo.Users(Id),
        GitProgressRunId uniqueidentifier NULL REFERENCES dbo.GitProgressRuns(Id),
        Source nvarchar(8) NOT NULL CHECK (Source IN ('AI', 'REVIEW')),
        Outcome nvarchar(8) NOT NULL CHECK (Outcome IN ('full', 'partial', 'missing')),
        Note nvarchar(1000) NULL,
        EvidenceJson nvarchar(max) NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        UpdatedAt datetimeoffset NOT NULL
    );
    CREATE INDEX IX_MilestoneCriterionAssessments_CriterionId_CreatedAt
        ON dbo.MilestoneCriterionAssessments(CriterionId, CreatedAt DESC);
END;

COMMIT TRANSACTION;
