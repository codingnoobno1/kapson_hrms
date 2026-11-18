IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA [identity];');
GO


CREATE TABLE [identity].[CandidateActivations] (
    [Id] bigint NOT NULL IDENTITY,
    [TokenHash] nvarchar(64) NOT NULL,
    [TenantKey] nvarchar(80) NOT NULL,
    [CandidateId] bigint NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [ExpiresUtc] datetimeoffset NOT NULL,
    [ConsumedUtc] datetimeoffset NULL,
    [InvalidatedUtc] datetimeoffset NULL,
    CONSTRAINT [PK_CandidateActivations] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [identity].[IdentityAudit] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NULL,
    [ActorUserId] nvarchar(450) NULL,
    [Action] nvarchar(80) NOT NULL,
    [SubjectType] nvarchar(40) NOT NULL,
    [SubjectId] nvarchar(450) NOT NULL,
    [DetailJson] nvarchar(2000) NULL,
    [OccurredUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_IdentityAudit] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [identity].[RefreshTokens] (
    [Id] bigint NOT NULL IDENTITY,
    [TokenHash] nvarchar(64) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [TenantKey] nvarchar(80) NULL,
    [FamilyId] nvarchar(64) NOT NULL,
    [DeviceId] nvarchar(200) NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [ExpiresUtc] datetimeoffset NOT NULL,
    [ConsumedUtc] datetimeoffset NULL,
    [RevokedUtc] datetimeoffset NULL,
    [RevokedReason] nvarchar(200) NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [identity].[Roles] (
    [Id] nvarchar(450) NOT NULL,
    [TenantKey] nvarchar(80) NULL,
    [Portal] int NOT NULL,
    [RoleCode] nvarchar(60) NOT NULL,
    [DisplayName] nvarchar(100) NOT NULL,
    [Description] nvarchar(300) NULL,
    [IsSystem] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [identity].[Users] (
    [Id] nvarchar(450) NOT NULL,
    [DisplayName] nvarchar(160) NOT NULL,
    [TenantKey] nvarchar(80) NULL,
    [Portal] int NOT NULL,
    [EmployeeNumber] nvarchar(60) NULL,
    [CandidateId] bigint NULL,
    [IsActive] bit NOT NULL,
    [MobileAccessEnabled] bit NOT NULL,
    [MustChangePassword] bit NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [PasswordChangedUtc] datetimeoffset NULL,
    [LastLoginUtc] datetimeoffset NULL,
    [PreviousLoginUtc] datetimeoffset NULL,
    [CreatedByUserId] nvarchar(450) NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [identity].[RoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_RoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [identity].[Roles] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [identity].[RolePermissions] (
    [Id] bigint NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [PermissionKey] nvarchar(100) NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [identity].[Roles] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [identity].[UserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [identity].[UserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [identity].[UserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [identity].[Roles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [identity].[UserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_CandidateActivations_TenantKey_CandidateId] ON [identity].[CandidateActivations] ([TenantKey], [CandidateId]);
GO


CREATE UNIQUE INDEX [IX_CandidateActivations_TokenHash] ON [identity].[CandidateActivations] ([TokenHash]);
GO


CREATE INDEX [IX_IdentityAudit_TenantKey_OccurredUtc] ON [identity].[IdentityAudit] ([TenantKey], [OccurredUtc]);
GO


CREATE INDEX [IX_RefreshTokens_FamilyId] ON [identity].[RefreshTokens] ([FamilyId]);
GO


CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [identity].[RefreshTokens] ([TokenHash]);
GO


CREATE INDEX [IX_RefreshTokens_UserId_ExpiresUtc] ON [identity].[RefreshTokens] ([UserId], [ExpiresUtc]);
GO


CREATE INDEX [IX_RoleClaims_RoleId] ON [identity].[RoleClaims] ([RoleId]);
GO


CREATE UNIQUE INDEX [IX_RolePermissions_RoleId_PermissionKey] ON [identity].[RolePermissions] ([RoleId], [PermissionKey]);
GO


CREATE UNIQUE INDEX [IX_Roles_TenantKey_Portal_RoleCode] ON [identity].[Roles] ([TenantKey], [Portal], [RoleCode]) WHERE [TenantKey] IS NOT NULL;
GO


CREATE UNIQUE INDEX [RoleNameIndex] ON [identity].[Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO


CREATE INDEX [IX_UserClaims_UserId] ON [identity].[UserClaims] ([UserId]);
GO


CREATE INDEX [IX_UserLogins_UserId] ON [identity].[UserLogins] ([UserId]);
GO


CREATE INDEX [IX_UserRoles_RoleId] ON [identity].[UserRoles] ([RoleId]);
GO


CREATE INDEX [EmailIndex] ON [identity].[Users] ([NormalizedEmail]);
GO


CREATE INDEX [IX_Users_CandidateId] ON [identity].[Users] ([CandidateId]) WHERE [CandidateId] IS NOT NULL;
GO


CREATE INDEX [IX_Users_TenantKey_EmployeeNumber] ON [identity].[Users] ([TenantKey], [EmployeeNumber]) WHERE [EmployeeNumber] IS NOT NULL;
GO


CREATE INDEX [IX_Users_TenantKey_Portal] ON [identity].[Users] ([TenantKey], [Portal]);
GO


CREATE UNIQUE INDEX [UserNameIndex] ON [identity].[Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO


