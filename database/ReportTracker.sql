IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'tracker') IS NULL EXEC(N'CREATE SCHEMA [tracker];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[AuditEvents] (
        [Id] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(max) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [DataJson] nvarchar(max) NOT NULL,
        [CorrelationId] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[ChannelRequirements] (
        [Id] uniqueidentifier NOT NULL,
        [TrackingObligationId] uniqueidentifier NOT NULL,
        [Channel] nvarchar(450) NOT NULL,
        [RequirementType] int NOT NULL,
        [IsFulfilled] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ChannelRequirements] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[ComponentDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [DefinitionVersionId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [ProviderDivisionId] uniqueidentifier NOT NULL,
        [PrimaryOfficerId] uniqueidentifier NULL,
        [DeputyOfficerId] uniqueidentifier NULL,
        [ReceivingDivisionId] uniqueidentifier NOT NULL,
        [ReceiptConfirmationRequired] bit NOT NULL,
        [DeadlineRuleJson] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ComponentDefinitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[CorrectionCases] (
        [Id] uniqueidentifier NOT NULL,
        [ReportCycleId] uniqueidentifier NOT NULL,
        [OriginalSendingEventId] uniqueidentifier NULL,
        [ReasonCategory] nvarchar(max) NOT NULL,
        [Explanation] nvarchar(1000) NULL,
        [AffectedComponentNames] nvarchar(max) NOT NULL,
        [IsFullScope] bit NOT NULL,
        [Status] int NOT NULL,
        [DueAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CorrectionCases] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[Delegations] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [DelegateUserId] uniqueidentifier NOT NULL,
        [GrantedByUserId] uniqueidentifier NOT NULL,
        [EffectiveFromUtc] datetime2 NOT NULL,
        [EffectiveToUtc] datetime2 NOT NULL,
        [Scope] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Delegations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[Divisions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Divisions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[EvidenceDecisions] (
        [Id] uniqueidentifier NOT NULL,
        [EvidenceDocumentId] uniqueidentifier NOT NULL,
        [DecidedByUserId] uniqueidentifier NOT NULL,
        [Decision] int NOT NULL,
        [Reason] nvarchar(max) NULL,
        [DecidedAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EvidenceDecisions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[EvidenceDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [SendingEventId] uniqueidentifier NULL,
        [ExtensionRequestId] uniqueidentifier NULL,
        [Purpose] int NOT NULL,
        [OriginalFileName] nvarchar(max) NOT NULL,
        [StorageKey] nvarchar(max) NOT NULL,
        [ContentType] nvarchar(max) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Sha256] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [UploadedByUserId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EvidenceDocuments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[ExtensionRequests] (
        [Id] uniqueidentifier NOT NULL,
        [TrackingObligationId] uniqueidentifier NOT NULL,
        [OriginalDueAtUtc] datetime2 NOT NULL,
        [RequestedDueAtUtc] datetime2 NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [PermissionReference] nvarchar(max) NULL,
        [RequestedByUserId] uniqueidentifier NOT NULL,
        [ApproverUserId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [DecidedAtUtc] datetime2 NULL,
        [DecisionReason] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ExtensionRequests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[ReceiptConfirmations] (
        [Id] uniqueidentifier NOT NULL,
        [SendingEventId] uniqueidentifier NOT NULL,
        [ActingUserId] uniqueidentifier NOT NULL,
        [ReceivedAtUtc] datetime2 NOT NULL,
        [IsDisputed] bit NOT NULL,
        [Reason] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ReceiptConfirmations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[ReportDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [OwnerDivisionId] uniqueidentifier NOT NULL,
        [BbRecipient] nvarchar(max) NOT NULL,
        [InstructionReference] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ReportDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportDefinitions_Divisions_OwnerDivisionId] FOREIGN KEY ([OwnerDivisionId]) REFERENCES [tracker].[Divisions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[Teams] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [DivisionId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Teams] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Teams_Divisions_DivisionId] FOREIGN KEY ([DivisionId]) REFERENCES [tracker].[Divisions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[Users] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] nvarchar(450) NOT NULL,
        [DisplayName] nvarchar(max) NOT NULL,
        [Email] nvarchar(450) NOT NULL,
        [DivisionId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Divisions_DivisionId] FOREIGN KEY ([DivisionId]) REFERENCES [tracker].[Divisions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[DefinitionVersions] (
        [Id] uniqueidentifier NOT NULL,
        [ReportDefinitionId] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [Frequency] int NOT NULL,
        [DueTime] time NOT NULL,
        [TimeZoneId] nvarchar(64) NOT NULL,
        [ScheduleRuleJson] nvarchar(max) NULL,
        [ReceiptConfirmationRequired] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DefinitionVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DefinitionVersions_ReportDefinitions_ReportDefinitionId] FOREIGN KEY ([ReportDefinitionId]) REFERENCES [tracker].[ReportDefinitions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[ReportCycles] (
        [Id] uniqueidentifier NOT NULL,
        [ReportDefinitionId] uniqueidentifier NOT NULL,
        [DefinitionVersionId] uniqueidentifier NOT NULL,
        [PeriodKey] nvarchar(80) NOT NULL,
        [PeriodLabel] nvarchar(max) NOT NULL,
        [PeriodStart] date NOT NULL,
        [PeriodEnd] date NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ReportCycles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportCycles_ReportDefinitions_ReportDefinitionId] FOREIGN KEY ([ReportDefinitionId]) REFERENCES [tracker].[ReportDefinitions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[TrackingObligations] (
        [Id] uniqueidentifier NOT NULL,
        [ReportCycleId] uniqueidentifier NOT NULL,
        [ParentObligationId] uniqueidentifier NULL,
        [CorrectionCaseId] uniqueidentifier NULL,
        [Type] int NOT NULL,
        [Name] nvarchar(250) NOT NULL,
        [AccountableDivisionId] uniqueidentifier NOT NULL,
        [PrimaryOfficerId] uniqueidentifier NULL,
        [DeputyOfficerId] uniqueidentifier NULL,
        [Recipient] nvarchar(max) NOT NULL,
        [OriginalDueAtUtc] datetime2 NOT NULL,
        [EffectiveDueAtUtc] datetime2 NOT NULL,
        [SendingStatus] int NOT NULL,
        [ReceiptStatus] int NOT NULL,
        [EvidenceStatus] int NOT NULL,
        [CorrectionStatus] int NOT NULL,
        [ExceptionStatus] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TrackingObligations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TrackingObligations_Divisions_AccountableDivisionId] FOREIGN KEY ([AccountableDivisionId]) REFERENCES [tracker].[Divisions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TrackingObligations_ReportCycles_ReportCycleId] FOREIGN KEY ([ReportCycleId]) REFERENCES [tracker].[ReportCycles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE TABLE [tracker].[SendingEvents] (
        [Id] uniqueidentifier NOT NULL,
        [TrackingObligationId] uniqueidentifier NOT NULL,
        [ActingUserId] uniqueidentifier NOT NULL,
        [ActualSentAtUtc] datetime2 NOT NULL,
        [Recipient] nvarchar(max) NOT NULL,
        [Channel] nvarchar(max) NOT NULL,
        [Reference] nvarchar(max) NULL,
        [IsPartial] bit NOT NULL,
        [ScopeNames] nvarchar(max) NULL,
        [TrackingRemarks] nvarchar(1000) NULL,
        [IsReversed] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SendingEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SendingEvents_TrackingObligations_TrackingObligationId] FOREIGN KEY ([TrackingObligationId]) REFERENCES [tracker].[TrackingObligations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChannelRequirements_TrackingObligationId_Channel] ON [tracker].[ChannelRequirements] ([TrackingObligationId], [Channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DefinitionVersions_ReportDefinitionId_VersionNumber] ON [tracker].[DefinitionVersions] ([ReportDefinitionId], [VersionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Divisions_Code] ON [tracker].[Divisions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReportCycles_ReportDefinitionId_PeriodKey] ON [tracker].[ReportCycles] ([ReportDefinitionId], [PeriodKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReportDefinitions_Code] ON [tracker].[ReportDefinitions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReportDefinitions_OwnerDivisionId] ON [tracker].[ReportDefinitions] ([OwnerDivisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SendingEvents_TrackingObligationId] ON [tracker].[SendingEvents] ([TrackingObligationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Teams_DivisionId] ON [tracker].[Teams] ([DivisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TrackingObligations_AccountableDivisionId] ON [tracker].[TrackingObligations] ([AccountableDivisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TrackingObligations_ReportCycleId] ON [tracker].[TrackingObligations] ([ReportCycleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_DivisionId] ON [tracker].[Users] ([DivisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [tracker].[Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_EmployeeId] ON [tracker].[Users] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160016_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101160016_InitialCreate', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[CalendarLinks] (
        [Id] uniqueidentifier NOT NULL,
        [TrackingObligationId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Provider] nvarchar(450) NOT NULL,
        [ExternalEventId] nvarchar(max) NOT NULL,
        [LastSynchronizedAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CalendarLinks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[ExtensionDecisions] (
        [Id] uniqueidentifier NOT NULL,
        [ExtensionRequestId] uniqueidentifier NOT NULL,
        [DecidedByUserId] uniqueidentifier NOT NULL,
        [Decision] int NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [DecidedAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ExtensionDecisions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[HolidayCalendars] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Year] int NOT NULL,
        [TimeZoneId] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_HolidayCalendars] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[NotificationDeliveries] (
        [Id] uniqueidentifier NOT NULL,
        [NotificationJobId] uniqueidentifier NOT NULL,
        [AttemptedAtUtc] datetime2 NOT NULL,
        [Succeeded] bit NOT NULL,
        [ProviderReference] nvarchar(max) NULL,
        [FailureReason] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_NotificationDeliveries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[NotificationJobs] (
        [Id] uniqueidentifier NOT NULL,
        [TrackingObligationId] uniqueidentifier NOT NULL,
        [ReminderPolicyId] uniqueidentifier NOT NULL,
        [LogicalKey] nvarchar(450) NOT NULL,
        [LocalDate] date NOT NULL,
        [Channel] nvarchar(max) NOT NULL,
        [DueAtUtc] datetime2 NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [AttemptCount] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_NotificationJobs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[Permissions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[ReminderPolicies] (
        [Id] uniqueidentifier NOT NULL,
        [ReportDefinitionId] uniqueidentifier NULL,
        [ObligationCategory] nvarchar(max) NOT NULL,
        [LeadDaysCsv] nvarchar(max) NOT NULL,
        [DispatchTime] time NOT NULL,
        [TimeZoneId] nvarchar(max) NOT NULL,
        [ChannelsCsv] nvarchar(max) NOT NULL,
        [EscalationAfterDays] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ReminderPolicies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[ResponsibilityAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [ReportDefinitionId] uniqueidentifier NOT NULL,
        [DivisionId] uniqueidentifier NOT NULL,
        [PrimaryOfficerId] uniqueidentifier NOT NULL,
        [DeputyOfficerId] uniqueidentifier NULL,
        [ResponsibilityType] nvarchar(max) NOT NULL,
        [EffectiveFromUtc] datetime2 NOT NULL,
        [EffectiveToUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ResponsibilityAssignments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[RolePermissions] (
        [Id] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[ScheduleRules] (
        [Id] uniqueidentifier NOT NULL,
        [DefinitionVersionId] uniqueidentifier NOT NULL,
        [PeriodConvention] nvarchar(max) NOT NULL,
        [FixedDay] int NULL,
        [OffsetDays] int NULL,
        [UsesBusinessDays] bit NOT NULL,
        [HolidayAdjustmentAuthorized] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ScheduleRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[StatusAmendments] (
        [Id] uniqueidentifier NOT NULL,
        [TrackingObligationId] uniqueidentifier NOT NULL,
        [ReplacedSendingEventId] uniqueidentifier NULL,
        [PreviousStatus] nvarchar(max) NOT NULL,
        [ReplacementStatus] nvarchar(max) NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [ActingUserId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_StatusAmendments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[UserRoles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [DivisionId] uniqueidentifier NULL,
        [ReportDefinitionId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE TABLE [tracker].[HolidayDates] (
        [Id] uniqueidentifier NOT NULL,
        [HolidayCalendarId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_HolidayDates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HolidayDates_HolidayCalendars_HolidayCalendarId] FOREIGN KEY ([HolidayCalendarId]) REFERENCES [tracker].[HolidayCalendars] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CalendarLinks_TrackingObligationId_UserId_Provider] ON [tracker].[CalendarLinks] ([TrackingObligationId], [UserId], [Provider]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_HolidayCalendars_Name_Year] ON [tracker].[HolidayCalendars] ([Name], [Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_HolidayDates_HolidayCalendarId_Date] ON [tracker].[HolidayDates] ([HolidayCalendarId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationJobs_LogicalKey] ON [tracker].[NotificationJobs] ([LogicalKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_Code] ON [tracker].[Permissions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RolePermissions_RoleId_PermissionId] ON [tracker].[RolePermissions] ([RoleId], [PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [tracker].[Roles] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserRoles_UserId_RoleId_DivisionId_ReportDefinitionId] ON [tracker].[UserRoles] ([UserId], [RoleId], [DivisionId], [ReportDefinitionId]) WHERE [DivisionId] IS NOT NULL AND [ReportDefinitionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160135_CompleteCoreModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101160135_CompleteCoreModel', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101184530_LocalAccounts'
)
BEGIN
    CREATE TABLE [tracker].[LocalCredentials] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [FailedAttempts] int NOT NULL,
        [LockedUntilUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_LocalCredentials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LocalCredentials_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [tracker].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101184530_LocalAccounts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LocalCredentials_UserId] ON [tracker].[LocalCredentials] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101184530_LocalAccounts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101184530_LocalAccounts', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102124108_SmartWorkflowFoundation'
)
BEGIN
    ALTER TABLE [tracker].[DefinitionVersions] ADD [ComponentDeadlineOffsetDays] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102124108_SmartWorkflowFoundation'
)
BEGIN
    ALTER TABLE [tracker].[ComponentDefinitions] ADD [Description] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102124108_SmartWorkflowFoundation'
)
BEGIN
    ALTER TABLE [tracker].[ComponentDefinitions] ADD [EvidenceRequired] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102124108_SmartWorkflowFoundation'
)
BEGIN
    ALTER TABLE [tracker].[ComponentDefinitions] ADD [IsRequired] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102124108_SmartWorkflowFoundation'
)
BEGIN
    ALTER TABLE [tracker].[ComponentDefinitions] ADD [Sequence] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102124108_SmartWorkflowFoundation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102124108_SmartWorkflowFoundation', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102154533_ComprehensiveOperations'
)
BEGIN
    ALTER TABLE [tracker].[Users] ADD [MobileNumber] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102154533_ComprehensiveOperations'
)
BEGIN
    ALTER TABLE [tracker].[ReminderPolicies] ADD [DailyOverdueReminders] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102154533_ComprehensiveOperations'
)
BEGIN
    ALTER TABLE [tracker].[ReminderPolicies] ADD [Name] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102154533_ComprehensiveOperations'
)
BEGIN
    ALTER TABLE [tracker].[ReminderPolicies] ADD [QuietHoursEnd] time NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102154533_ComprehensiveOperations'
)
BEGIN
    ALTER TABLE [tracker].[ReminderPolicies] ADD [QuietHoursStart] time NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102154533_ComprehensiveOperations'
)
BEGIN
    ALTER TABLE [tracker].[ReminderPolicies] ADD [RecipientsCsv] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102154533_ComprehensiveOperations'
)
BEGIN
    ALTER TABLE [tracker].[ReminderPolicies] ADD [WorkingDaysCsv] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102154533_ComprehensiveOperations'
)
BEGIN
    ALTER TABLE [tracker].[NotificationJobs] ADD [Recipient] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102154533_ComprehensiveOperations'
)
BEGIN
    ALTER TABLE [tracker].[EvidenceDocuments] ADD [TrackingObligationId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102154533_ComprehensiveOperations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102154533_ComprehensiveOperations', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109191357_ReceivingDivisionResponsibilities'
)
BEGIN
    ALTER TABLE [tracker].[TrackingObligations] ADD [ReceivingDivisionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109191357_ReceivingDivisionResponsibilities'
)
BEGIN
    CREATE INDEX [IX_TrackingObligations_ReceivingDivisionId] ON [tracker].[TrackingObligations] ([ReceivingDivisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109191357_ReceivingDivisionResponsibilities'
)
BEGIN
    ALTER TABLE [tracker].[TrackingObligations] ADD CONSTRAINT [FK_TrackingObligations_Divisions_ReceivingDivisionId] FOREIGN KEY ([ReceivingDivisionId]) REFERENCES [tracker].[Divisions] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109191357_ReceivingDivisionResponsibilities'
)
BEGIN
    EXEC(N'UPDATE obligation
    SET ReceivingDivisionId = division.Id
    FROM tracker.TrackingObligations AS obligation
    INNER JOIN tracker.Divisions AS division ON division.Id = TRY_CONVERT(uniqueidentifier, obligation.Recipient)
    WHERE obligation.Type = 1 AND obligation.ReceivingDivisionId IS NULL AND LEN(obligation.Recipient) = 36;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109191357_ReceivingDivisionResponsibilities'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109191357_ReceivingDivisionResponsibilities', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110192740_SeparateCoordinatingAndSubmittingDivisions'
)
BEGIN
    ALTER TABLE [tracker].[ReportDefinitions] ADD [SubmittingDivisionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110192740_SeparateCoordinatingAndSubmittingDivisions'
)
BEGIN
    EXEC(N'UPDATE report
    SET SubmittingDivisionId = COALESCE((
        SELECT TOP (1) assignment.DivisionId
        FROM tracker.ResponsibilityAssignments AS assignment
        INNER JOIN tracker.Divisions AS division ON division.Id = assignment.DivisionId
        INNER JOIN tracker.Users AS officer ON officer.Id = assignment.PrimaryOfficerId
            AND officer.DivisionId = assignment.DivisionId AND officer.IsActive = 1
        WHERE assignment.ReportDefinitionId = report.Id AND assignment.ResponsibilityType = N''MainSender''
            AND assignment.EffectiveFromUtc <= GETUTCDATE()
            AND (assignment.EffectiveToUtc IS NULL OR assignment.EffectiveToUtc > GETUTCDATE())
        ORDER BY assignment.EffectiveFromUtc DESC, assignment.Id
    ), report.OwnerDivisionId)
    FROM tracker.ReportDefinitions AS report;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110192740_SeparateCoordinatingAndSubmittingDivisions'
)
BEGIN
    CREATE INDEX [IX_ReportDefinitions_SubmittingDivisionId] ON [tracker].[ReportDefinitions] ([SubmittingDivisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110192740_SeparateCoordinatingAndSubmittingDivisions'
)
BEGIN
    ALTER TABLE [tracker].[ReportDefinitions] ADD CONSTRAINT [FK_ReportDefinitions_Divisions_SubmittingDivisionId] FOREIGN KEY ([SubmittingDivisionId]) REFERENCES [tracker].[Divisions] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110192740_SeparateCoordinatingAndSubmittingDivisions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110192740_SeparateCoordinatingAndSubmittingDivisions', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110202339_ReportReceivingDivision'
)
BEGIN
    ALTER TABLE [tracker].[ReportDefinitions] ADD [ReceivingDivisionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110202339_ReportReceivingDivision'
)
BEGIN
    CREATE INDEX [IX_ReportDefinitions_ReceivingDivisionId] ON [tracker].[ReportDefinitions] ([ReceivingDivisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110202339_ReportReceivingDivision'
)
BEGIN
    ALTER TABLE [tracker].[ReportDefinitions] ADD CONSTRAINT [FK_ReportDefinitions_Divisions_ReceivingDivisionId] FOREIGN KEY ([ReceivingDivisionId]) REFERENCES [tracker].[Divisions] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110202339_ReportReceivingDivision'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110202339_ReportReceivingDivision', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110202916_ComponentMandatorySnapshot'
)
BEGIN
    ALTER TABLE [tracker].[TrackingObligations] ADD [IsRequired] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110202916_ComponentMandatorySnapshot'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110202916_ComponentMandatorySnapshot', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111173026_SubmissionRecipientDirectory'
)
BEGIN
    ALTER TABLE [tracker].[ReportDefinitions] ADD [SubmissionRecipientId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111173026_SubmissionRecipientDirectory'
)
BEGIN
    CREATE TABLE [tracker].[SubmissionRecipients] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(250) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SubmissionRecipients] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111173026_SubmissionRecipientDirectory'
)
BEGIN
    CREATE TABLE [tracker].[SubmissionRecipientDepartments] (
        [Id] uniqueidentifier NOT NULL,
        [SubmissionRecipientId] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SubmissionRecipientDepartments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubmissionRecipientDepartments_SubmissionRecipients_SubmissionRecipientId] FOREIGN KEY ([SubmissionRecipientId]) REFERENCES [tracker].[SubmissionRecipients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111173026_SubmissionRecipientDirectory'
)
BEGIN
    CREATE TABLE [tracker].[ReportSubmissionDepartments] (
        [ReportDefinitionId] uniqueidentifier NOT NULL,
        [DepartmentId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ReportSubmissionDepartments] PRIMARY KEY ([ReportDefinitionId], [DepartmentId]),
        CONSTRAINT [FK_ReportSubmissionDepartments_ReportDefinitions_ReportDefinitionId] FOREIGN KEY ([ReportDefinitionId]) REFERENCES [tracker].[ReportDefinitions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ReportSubmissionDepartments_SubmissionRecipientDepartments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [tracker].[SubmissionRecipientDepartments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111173026_SubmissionRecipientDirectory'
)
BEGIN
    CREATE INDEX [IX_ReportDefinitions_SubmissionRecipientId] ON [tracker].[ReportDefinitions] ([SubmissionRecipientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111173026_SubmissionRecipientDirectory'
)
BEGIN
    CREATE INDEX [IX_ReportSubmissionDepartments_DepartmentId] ON [tracker].[ReportSubmissionDepartments] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111173026_SubmissionRecipientDirectory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubmissionRecipientDepartments_SubmissionRecipientId_Name] ON [tracker].[SubmissionRecipientDepartments] ([SubmissionRecipientId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111173026_SubmissionRecipientDirectory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubmissionRecipients_Name] ON [tracker].[SubmissionRecipients] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111173026_SubmissionRecipientDirectory'
)
BEGIN
    ALTER TABLE [tracker].[ReportDefinitions] ADD CONSTRAINT [FK_ReportDefinitions_SubmissionRecipients_SubmissionRecipientId] FOREIGN KEY ([SubmissionRecipientId]) REFERENCES [tracker].[SubmissionRecipients] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111173026_SubmissionRecipientDirectory'
)
BEGIN
    INSERT INTO [tracker].[SubmissionRecipients] ([Id], [Name], [CreatedAtUtc])
    SELECT NEWID(), [Name], SYSUTCDATETIME()
    FROM (
        SELECT DISTINCT CONVERT(nvarchar(250), LTRIM(RTRIM([BbRecipient]))) AS [Name]
        FROM [tracker].[ReportDefinitions]
        WHERE DATALENGTH(LTRIM(RTRIM([BbRecipient]))) BETWEEN 2 AND 500
    ) AS [LegacyRecipients];

    EXEC(N'
        UPDATE report
        SET [SubmissionRecipientId] = recipient.[Id]
        FROM [tracker].[ReportDefinitions] AS report
        JOIN [tracker].[SubmissionRecipients] AS recipient
          ON LTRIM(RTRIM(report.[BbRecipient])) = recipient.[Name]
        WHERE report.[SubmissionRecipientId] IS NULL;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111173026_SubmissionRecipientDirectory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111173026_SubmissionRecipientDirectory', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111183238_ComponentContributorAssignments'
)
BEGIN
    ALTER TABLE [tracker].[TrackingObligations] ADD [ContributorsJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111183238_ComponentContributorAssignments'
)
BEGIN
    ALTER TABLE [tracker].[ComponentDefinitions] ADD [ContributorsJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111183238_ComponentContributorAssignments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111183238_ComponentContributorAssignments', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113112921_SimplifyComponentDefinitions'
)
BEGIN
    UPDATE component
    SET [ContributorsJson] = (
        SELECT assignment.[UserId], assignment.[Role]
        FROM (VALUES (component.[PrimaryOfficerId], N'LeadPreparer'),
                     (NULLIF(component.[DeputyOfficerId], component.[PrimaryOfficerId]), N'BackupPreparer')) AS assignment([UserId], [Role])
        WHERE assignment.[UserId] IS NOT NULL
        FOR JSON PATH
    )
    FROM [tracker].[ComponentDefinitions] AS component
    WHERE component.[ContributorsJson] IS NULL AND component.[PrimaryOfficerId] IS NOT NULL;

    UPDATE component
    SET [PrimaryOfficerId] = lead.[UserId],
        [DeputyOfficerId] = NULL,
        [ContributorsJson] = (
            SELECT contributor.[UserId], contributor.[Role]
            FROM OPENJSON(component.[ContributorsJson])
            WITH ([UserId] uniqueidentifier '$.UserId', [Role] nvarchar(32) '$.Role') AS contributor
            FOR JSON PATH
        )
    FROM [tracker].[ComponentDefinitions] AS component
    CROSS APPLY (
        SELECT TOP (1) contributor.[UserId]
        FROM OPENJSON(CASE WHEN ISJSON(component.[ContributorsJson]) = 1
                          THEN component.[ContributorsJson] ELSE N'[]' END)
        WITH ([UserId] uniqueidentifier '$.UserId', [Role] nvarchar(32) '$.Role') AS contributor
        WHERE contributor.[Role] = N'LeadPreparer' AND contributor.[UserId] IS NOT NULL
        ORDER BY contributor.[UserId]
    ) AS lead;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113112921_SimplifyComponentDefinitions'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[tracker].[ComponentDefinitions]') AND [c].[name] = N'EvidenceRequired');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [tracker].[ComponentDefinitions] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [tracker].[ComponentDefinitions] DROP COLUMN [EvidenceRequired];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113112921_SimplifyComponentDefinitions'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[tracker].[ComponentDefinitions]') AND [c].[name] = N'IsRequired');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [tracker].[ComponentDefinitions] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [tracker].[ComponentDefinitions] DROP COLUMN [IsRequired];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113112921_SimplifyComponentDefinitions'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[tracker].[ComponentDefinitions]') AND [c].[name] = N'ReceiptConfirmationRequired');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [tracker].[ComponentDefinitions] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [tracker].[ComponentDefinitions] DROP COLUMN [ReceiptConfirmationRequired];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113112921_SimplifyComponentDefinitions'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[tracker].[ComponentDefinitions]') AND [c].[name] = N'Sequence');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [tracker].[ComponentDefinitions] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [tracker].[ComponentDefinitions] DROP COLUMN [Sequence];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113112921_SimplifyComponentDefinitions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261113112921_SimplifyComponentDefinitions', N'10.0.10');
END;

COMMIT;
GO

