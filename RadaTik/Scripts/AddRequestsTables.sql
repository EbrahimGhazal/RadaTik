-- إنشاء جدول طلبات الصيانة
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MaintenanceRequests]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[MaintenanceRequests] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [ClientId] INT NOT NULL,
        [Type] INT NOT NULL DEFAULT 0,
        [Description] NVARCHAR(1000) NOT NULL,
        [Priority] INT NOT NULL DEFAULT 1,
        [Status] INT NOT NULL DEFAULT 0,
        [RequestDate] DATETIME2 NOT NULL DEFAULT GETDATE(),
        [AcceptedDate] DATETIME2 NULL,
        [CompletedDate] DATETIME2 NULL,
        [TechnicianNotes] NVARCHAR(1000) NULL,
        [RejectionReason] NVARCHAR(500) NULL,
        [AssignedToId] NVARCHAR(450) NULL,
        [ProcessedById] NVARCHAR(450) NULL,
        [ContactPhone] NVARCHAR(20) NULL,
        [PreferredContactTime] NVARCHAR(100) NULL,
        [Address] NVARCHAR(500) NULL,
        CONSTRAINT [PK_MaintenanceRequests] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_MaintenanceRequests_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MaintenanceRequests_AspNetUsers_AssignedToId] FOREIGN KEY ([AssignedToId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MaintenanceRequests_AspNetUsers_ProcessedById] FOREIGN KEY ([ProcessedById]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
    
    CREATE INDEX [IX_MaintenanceRequests_ClientId] ON [dbo].[MaintenanceRequests] ([ClientId]);
    CREATE INDEX [IX_MaintenanceRequests_AssignedToId] ON [dbo].[MaintenanceRequests] ([AssignedToId]);
    CREATE INDEX [IX_MaintenanceRequests_ProcessedById] ON [dbo].[MaintenanceRequests] ([ProcessedById]);
    CREATE INDEX [IX_MaintenanceRequests_Status] ON [dbo].[MaintenanceRequests] ([Status]);
    
    PRINT 'تم إنشاء جدول MaintenanceRequests بنجاح';
END
ELSE
BEGIN
    PRINT 'جدول MaintenanceRequests موجود مسبقاً';
END
GO

-- إنشاء جدول طلبات تغيير السرعة
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SpeedChangeRequests]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SpeedChangeRequests] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [ClientId] INT NOT NULL,
        [CurrentProfileId] INT NOT NULL,
        [RequestedProfileId] INT NOT NULL,
        [Reason] NVARCHAR(500) NULL,
        [Status] INT NOT NULL DEFAULT 0,
        [RequestDate] DATETIME2 NOT NULL DEFAULT GETDATE(),
        [ProcessedDate] DATETIME2 NULL,
        [ImplementedDate] DATETIME2 NULL,
        [RejectionReason] NVARCHAR(500) NULL,
        [AdminNotes] NVARCHAR(1000) NULL,
        [ProcessedById] NVARCHAR(450) NULL,
        [ImplementedById] NVARCHAR(450) NULL,
        [PriceDifference] DECIMAL(18,2) NULL,
        [IsPriceDifferencePaid] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_SpeedChangeRequests] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_SpeedChangeRequests_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SpeedChangeRequests_Profiles_CurrentProfileId] FOREIGN KEY ([CurrentProfileId]) REFERENCES [dbo].[Profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SpeedChangeRequests_Profiles_RequestedProfileId] FOREIGN KEY ([RequestedProfileId]) REFERENCES [dbo].[Profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SpeedChangeRequests_AspNetUsers_ProcessedById] FOREIGN KEY ([ProcessedById]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SpeedChangeRequests_AspNetUsers_ImplementedById] FOREIGN KEY ([ImplementedById]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
    
    CREATE INDEX [IX_SpeedChangeRequests_ClientId] ON [dbo].[SpeedChangeRequests] ([ClientId]);
    CREATE INDEX [IX_SpeedChangeRequests_CurrentProfileId] ON [dbo].[SpeedChangeRequests] ([CurrentProfileId]);
    CREATE INDEX [IX_SpeedChangeRequests_RequestedProfileId] ON [dbo].[SpeedChangeRequests] ([RequestedProfileId]);
    CREATE INDEX [IX_SpeedChangeRequests_ProcessedById] ON [dbo].[SpeedChangeRequests] ([ProcessedById]);
    CREATE INDEX [IX_SpeedChangeRequests_ImplementedById] ON [dbo].[SpeedChangeRequests] ([ImplementedById]);
    CREATE INDEX [IX_SpeedChangeRequests_Status] ON [dbo].[SpeedChangeRequests] ([Status]);
    
    PRINT 'تم إنشاء جدول SpeedChangeRequests بنجاح';
END
ELSE
BEGIN
    PRINT 'جدول SpeedChangeRequests موجود مسبقاً';
END
GO

PRINT '✅ تم إنشاء جداول الطلبات بنجاح!';
