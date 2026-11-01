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

