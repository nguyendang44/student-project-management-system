BEGIN TRANSACTION;
CREATE TABLE [RegistrationPeriods] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [StartsAt] datetimeoffset NOT NULL,
    [EndsAt] datetimeoffset NOT NULL,
    [IsOpen] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_RegistrationPeriods] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_RegistrationPeriod_Dates] CHECK ([EndsAt] > [StartsAt])
);

CREATE TABLE [Topics] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(300) NOT NULL,
    [Description] nvarchar(4000) NOT NULL,
    [Objective] nvarchar(2000) NULL,
    [ExpectedContent] nvarchar(2000) NULL,
    [ProposedTechnology] nvarchar(1000) NULL,
    [ProposedByUserId] uniqueidentifier NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [IsRegistrationOpen] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Topics] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Topics_Users_ProposedByUserId] FOREIGN KEY ([ProposedByUserId]) REFERENCES [Users] ([Id])
);

CREATE TABLE [TopicStateHistories] (
    [Id] uniqueidentifier NOT NULL,
    [TopicId] uniqueidentifier NOT NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [FromStatus] nvarchar(32) NOT NULL,
    [ToStatus] nvarchar(32) NOT NULL,
    [Reason] nvarchar(2000) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_TopicStateHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TopicStateHistories_Topics_TopicId] FOREIGN KEY ([TopicId]) REFERENCES [Topics] ([Id]),
    CONSTRAINT [FK_TopicStateHistories_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id])
);

CREATE TABLE [TopicRegistrations] (
    [Id] uniqueidentifier NOT NULL,
    [TopicId] uniqueidentifier NOT NULL,
    [StudentUserId] uniqueidentifier NOT NULL,
    [RegistrationPeriodId] uniqueidentifier NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_TopicRegistrations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TopicRegistrations_Topics_TopicId] FOREIGN KEY ([TopicId]) REFERENCES [Topics] ([Id]),
    CONSTRAINT [FK_TopicRegistrations_Users_StudentUserId] FOREIGN KEY ([StudentUserId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_TopicRegistrations_RegistrationPeriods_RegistrationPeriodId] FOREIGN KEY ([RegistrationPeriodId]) REFERENCES [RegistrationPeriods] ([Id])
);

CREATE INDEX [IX_Topics_ProposedByUserId] ON [Topics] ([ProposedByUserId]);

CREATE INDEX [IX_Topics_Status_IsRegistrationOpen] ON [Topics] ([Status], [IsRegistrationOpen]);

CREATE INDEX [IX_TopicStateHistories_TopicId_CreatedAt] ON [TopicStateHistories] ([TopicId], [CreatedAt]);

CREATE INDEX [IX_TopicStateHistories_ActorUserId] ON [TopicStateHistories] ([ActorUserId]);

CREATE INDEX [IX_TopicRegistrations_TopicId] ON [TopicRegistrations] ([TopicId]);

CREATE INDEX [IX_TopicRegistrations_RegistrationPeriodId] ON [TopicRegistrations] ([RegistrationPeriodId]);

CREATE INDEX [IX_TopicRegistrations_StudentUserId_RegistrationPeriodId_Status] ON [TopicRegistrations] ([StudentUserId], [RegistrationPeriodId], [Status]);

CREATE UNIQUE INDEX [IX_TopicRegistrations_StudentUserId_RegistrationPeriodId_Active] ON [TopicRegistrations] ([StudentUserId], [RegistrationPeriodId]) WHERE [Status] IN ('PENDING','ACCEPTED');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261008074743_TopicManagementV05', N'10.0.10');

COMMIT;
GO

