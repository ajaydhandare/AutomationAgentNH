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
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE TABLE [AutomationConfig] (
        [Id] uniqueidentifier NOT NULL,
        [Module] nvarchar(50) NOT NULL,
        [EnableAgent] bit NOT NULL,
        [EnableModule] bit NOT NULL,
        [Mode] nvarchar(10) NOT NULL,
        [PollIntervalSeconds] int NOT NULL,
        [ReconcileIntervalMinutes] int NOT NULL,
        [WorkingHoursStart] time NULL,
        [WorkingHoursEnd] time NULL,
        [RetryCount] int NOT NULL,
        [ParallelWorkers] int NOT NULL,
        [LoggingLevel] nvarchar(20) NOT NULL,
        [IsLicensed] bit NOT NULL,
        [PayloadRetentionDays] int NOT NULL,
        [LogRetentionDays] int NOT NULL,
        [ErrorRetentionDays] int NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_AutomationConfig] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE TABLE [AutomationError] (
        [Id] uniqueidentifier NOT NULL,
        [JobId] uniqueidentifier NOT NULL,
        [StepId] uniqueidentifier NULL,
        [ErrorType] nvarchar(20) NOT NULL,
        [TechnicalMessage] nvarchar(max) NOT NULL,
        [LaymanMessage] nvarchar(1000) NOT NULL,
        [StackTrace] nvarchar(max) NULL,
        [ApiEndpoint] nvarchar(500) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AutomationError] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE TABLE [AutomationJob] (
        [Id] uniqueidentifier NOT NULL,
        [CorrelationId] nvarchar(64) NOT NULL,
        [WorkflowType] nvarchar(50) NOT NULL,
        [DocumentType] nvarchar(50) NOT NULL,
        [DocumentId] nvarchar(100) NOT NULL,
        [Mode] nvarchar(10) NOT NULL,
        [Priority] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CurrentStage] nvarchar(50) NULL,
        [RetryCount] int NOT NULL,
        [IdempotencyKey] nchar(64) NOT NULL,
        [ApprovedBy] nvarchar(100) NULL,
        [ApprovedAtUtc] datetimeoffset NULL,
        [CancelledBy] nvarchar(100) NULL,
        [CancellationReason] nvarchar(1000) NULL,
        [NotBeforeUtc] datetimeoffset NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [StartedAtUtc] datetimeoffset NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_AutomationJob] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE TABLE [AutomationLog] (
        [Id] uniqueidentifier NOT NULL,
        [JobId] uniqueidentifier NOT NULL,
        [StepId] uniqueidentifier NULL,
        [CorrelationId] nvarchar(64) NOT NULL,
        [Module] nvarchar(50) NULL,
        [ApiEndpoint] nvarchar(500) NULL,
        [StartedAtUtc] datetimeoffset NOT NULL,
        [CompletedAtUtc] datetimeoffset NOT NULL,
        [DurationMs] bigint NOT NULL,
        [Result] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_AutomationLog] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE TABLE [AutomationJobStep] (
        [Id] uniqueidentifier NOT NULL,
        [JobId] uniqueidentifier NOT NULL,
        [Stage] nvarchar(50) NOT NULL,
        [OperationName] nvarchar(100) NOT NULL,
        [Sequence] int NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [Target] nvarchar(50) NULL,
        [Status] nvarchar(20) NOT NULL,
        [RetryCount] int NOT NULL,
        [RequestPayload] nvarchar(max) NULL,
        [ResponsePayload] nvarchar(max) NULL,
        [ErpDocumentRef] nvarchar(100) NULL,
        [Remarks] nvarchar(1000) NULL,
        [ApprovedBy] nvarchar(100) NULL,
        [ApprovedAtUtc] datetimeoffset NULL,
        [StartedAtUtc] datetimeoffset NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_AutomationJobStep] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AutomationJobStep_AutomationJob_JobId] FOREIGN KEY ([JobId]) REFERENCES [AutomationJob] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_AutomationConfig_Module] ON [AutomationConfig] ([Module]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE INDEX [IX_AutomationError_CreatedAtUtc] ON [AutomationError] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE INDEX [IX_AutomationError_JobId] ON [AutomationError] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE INDEX [IX_AutomationJob_Claim] ON [AutomationJob] ([Status], [Priority], [CreatedAtUtc]) INCLUDE ([NotBeforeUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE INDEX [IX_AutomationJob_CreatedAtUtc] ON [AutomationJob] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE INDEX [IX_AutomationJob_DocumentId] ON [AutomationJob] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_AutomationJob_IdempotencyKey_Live] ON [AutomationJob] ([IdempotencyKey]) WHERE [Status] <> ''Cancelled''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE INDEX [IX_AutomationJobStep_Job_Status] ON [AutomationJobStep] ([JobId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_AutomationJobStep_Job_Sequence] ON [AutomationJobStep] ([JobId], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE INDEX [IX_AutomationLog_CorrelationId] ON [AutomationLog] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE INDEX [IX_AutomationLog_JobId] ON [AutomationLog] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    CREATE INDEX [IX_AutomationLog_StartedAtUtc] ON [AutomationLog] ([StartedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727164450_InitialAutomationSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260727164450_InitialAutomationSchema', N'10.0.10');
END;

COMMIT;
GO

