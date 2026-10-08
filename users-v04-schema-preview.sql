BEGIN TRANSACTION;
CREATE TABLE [Students] (
    [Id] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [StudentCode] nvarchar(40) NOT NULL,
    [Faculty] nvarchar(200) NULL,
    CONSTRAINT [PK_Students] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Students_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);

CREATE TABLE [Lecturers] (
    [Id] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Specialty] nvarchar(300) NULL,
    CONSTRAINT [PK_Lecturers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Lecturers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);

CREATE TABLE [AuditEntries] (
    [Id] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [ActorUserId] uniqueidentifier NULL,
    [Action] nvarchar(200) NOT NULL,
    [EntityName] nvarchar(200) NOT NULL,
    [EntityId] uniqueidentifier NULL,
    CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AuditEntries_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id])
);

CREATE UNIQUE INDEX [IX_Students_UserId] ON [Students] ([UserId]);

CREATE UNIQUE INDEX [IX_Students_StudentCode] ON [Students] ([StudentCode]);

CREATE UNIQUE INDEX [IX_Lecturers_UserId] ON [Lecturers] ([UserId]);

CREATE INDEX [IX_AuditEntries_ActorUserId] ON [AuditEntries] ([ActorUserId]);

CREATE INDEX [IX_AuditEntries_EntityName_EntityId_CreatedAt] ON [AuditEntries] ([EntityName], [EntityId], [CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261008034714_UserManagementV04Schema', N'10.0.10');

COMMIT;
GO

