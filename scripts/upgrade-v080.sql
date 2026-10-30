-- v0.8 additive schema, never deletes existing business data.
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRY
BEGIN TRAN;
IF DB_NAME() <> N'StudentProjectsDev' THROW 51000, 'Expected StudentProjectsDev DB', 1;
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL OR OBJECT_ID(N'dbo.Projects', N'U') IS NULL
    THROW 51001, 'Missing v0.7 tables', 1;
IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.Notifications (
      Id uniqueidentifier NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
      CreatedAt datetimeoffset(7) NOT NULL,
      UpdatedAt datetimeoffset(7) NOT NULL,
      RecipientUserId uniqueidentifier NOT NULL,
      Type nvarchar(100) NOT NULL,
      Message nvarchar(4000) NOT NULL,
      IsRead bit NOT NULL,
      CONSTRAINT FK_Notifications_Users_RecipientUserId FOREIGN KEY(RecipientUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION
  );
  CREATE INDEX IX_Notifications_RecipientUserId_IsRead_CreatedAt ON dbo.Notifications(RecipientUserId, IsRead, CreatedAt);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Notifications') AND name=N'UX_V08_Notifications_EventKey')
BEGIN
  CREATE UNIQUE INDEX UX_V08_Notifications_EventKey ON dbo.Notifications(RecipientUserId, Type)
      WHERE Type >= N'V08:' AND Type < N'V09:';
END;
IF OBJECT_ID(N'dbo.GuidanceSessions', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.GuidanceSessions (
      Id uniqueidentifier NOT NULL CONSTRAINT PK_GuidanceSessions PRIMARY KEY,
      CreatedAt datetimeoffset(7) NOT NULL,
      UpdatedAt datetimeoffset(7) NOT NULL,
      ProjectId uniqueidentifier NOT NULL,
      CreatedByUserId uniqueidentifier NOT NULL,
      MeetingAt datetimeoffset(7) NOT NULL,
      Summary nvarchar(4000) NOT NULL,
      Feedback nvarchar(4000) NOT NULL,
      NextActions nvarchar(4000) NOT NULL,
      LecturerConfirmedAt datetimeoffset(7) NULL,
      CONSTRAINT FK_GuidanceSessions_Projects FOREIGN KEY(ProjectId) REFERENCES dbo.Projects(Id) ON DELETE NO ACTION,
      CONSTRAINT FK_GuidanceSessions_Users FOREIGN KEY(CreatedByUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION
  );
  CREATE INDEX IX_GuidanceSessions_ProjectId_MeetingAt ON dbo.GuidanceSessions(ProjectId, MeetingAt);
END;
COMMIT;
SELECT N'v0.8 schema OK' AS Result, DB_NAME() AS DatabaseName;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT > 0 ROLLBACK;
 THROW;
END CATCH;
