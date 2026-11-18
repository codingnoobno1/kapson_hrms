IF SCHEMA_ID(N'recruitment') IS NULL EXEC(N'CREATE SCHEMA [recruitment];');
GO


IF SCHEMA_ID(N'workforce') IS NULL EXEC(N'CREATE SCHEMA [workforce];');
GO


IF SCHEMA_ID(N'payroll') IS NULL EXEC(N'CREATE SCHEMA [payroll];');
GO


IF SCHEMA_ID(N'platform') IS NULL EXEC(N'CREATE SCHEMA [platform];');
GO


IF SCHEMA_ID(N'employee') IS NULL EXEC(N'CREATE SCHEMA [employee];');
GO


CREATE TABLE [recruitment].[ApplicationDocuments] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [ApplicationId] bigint NOT NULL,
    [CategoryCode] nvarchar(80) NOT NULL,
    [OriginalFileName] nvarchar(260) NOT NULL,
    [ContentType] nvarchar(120) NOT NULL,
    [SizeBytes] bigint NOT NULL,
    [StoragePath] nvarchar(400) NOT NULL,
    [ScanState] nvarchar(20) NOT NULL,
    [UploadedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ApplicationDocuments] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [recruitment].[ApplicationEvents] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [ApplicationId] bigint NOT NULL,
    [FromStage] nvarchar(40) NOT NULL,
    [ToStage] nvarchar(40) NOT NULL,
    [Reason] nvarchar(1000) NULL,
    [ActorId] nvarchar(450) NULL,
    [ActorKind] nvarchar(20) NOT NULL,
    [OccurredUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ApplicationEvents] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [recruitment].[Applications] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [JobOpeningId] bigint NOT NULL,
    [CandidateId] bigint NOT NULL,
    [Stage] nvarchar(40) NOT NULL,
    [Source] nvarchar(60) NOT NULL,
    [NoticePeriodDays] int NOT NULL,
    [ExpectedCtc] decimal(18,2) NULL,
    [AnswersJson] nvarchar(max) NOT NULL,
    [Rating] decimal(3,1) NULL,
    [RejectionReason] nvarchar(1000) NULL,
    [ConsentGranted] bit NOT NULL,
    [ConsentPolicyVersion] nvarchar(40) NULL,
    [ConsentUtc] datetimeoffset NULL,
    [AppliedUtc] datetimeoffset NOT NULL,
    [StageChangedUtc] datetimeoffset NOT NULL,
    [StageChangedBy] nvarchar(450) NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Applications] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[AttendanceCorrections] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [WorkDate] date NOT NULL,
    [ReasonCode] nvarchar(40) NOT NULL,
    [Comment] nvarchar(2000) NULL,
    [OriginalPunchesJson] nvarchar(max) NOT NULL,
    [RequestedPunchesJson] nvarchar(max) NOT NULL,
    [State] nvarchar(20) NOT NULL,
    [DecisionReasonCode] nvarchar(40) NULL,
    [DecisionComment] nvarchar(2000) NULL,
    [DecidedBy] nvarchar(450) NULL,
    [DecidedUtc] datetimeoffset NULL,
    [SubmittedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_AttendanceCorrections] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[AttendanceDays] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [WorkDate] date NOT NULL,
    [ShiftCode] nvarchar(40) NULL,
    [Status] nvarchar(20) NOT NULL,
    [WorkedMinutes] int NOT NULL,
    [ExpectedMinutes] int NOT NULL,
    [ExceptionsJson] nvarchar(max) NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_AttendanceDays] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[AttendanceExceptions] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [WorkDate] date NOT NULL,
    [ExceptionType] nvarchar(40) NOT NULL,
    [DurationMinutes] int NOT NULL,
    [State] nvarchar(20) NOT NULL,
    [PolicyCode] nvarchar(40) NULL,
    [EmployeeJustification] nvarchar(2000) NULL,
    [ApprovedMinutes] int NULL,
    [DecisionReasonCode] nvarchar(40) NULL,
    [DecisionComment] nvarchar(2000) NULL,
    [DecidedBy] nvarchar(450) NULL,
    [DecidedUtc] datetimeoffset NULL,
    [DetectedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_AttendanceExceptions] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[AttendancePunches] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [WorkDate] date NOT NULL,
    [Type] nvarchar(10) NOT NULL,
    [OccurredUtc] datetimeoffset NOT NULL,
    [Source] nvarchar(20) NOT NULL,
    [DeviceId] nvarchar(200) NULL,
    [LocationLabel] nvarchar(200) NULL,
    [Latitude] decimal(9,6) NULL,
    [Longitude] decimal(9,6) NULL,
    [ClientEventId] nvarchar(200) NULL,
    [SupersededByCorrectionId] bigint NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AttendancePunches] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [payroll].[BankTransferBatches] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [RunId] bigint NOT NULL,
    [ReferenceNo] nvarchar(40) NOT NULL,
    [CompanyBankAccountId] bigint NOT NULL,
    [TransferDate] date NOT NULL,
    [TransferMode] nvarchar(10) NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [EmployeeCount] int NOT NULL,
    [CreatedBy] nvarchar(450) NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_BankTransferBatches] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [recruitment].[Candidates] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [FullName] nvarchar(160) NOT NULL,
    [Phone] nvarchar(20) NOT NULL,
    [CurrentLocation] nvarchar(180) NULL,
    [TotalExperienceYears] decimal(4,1) NOT NULL,
    [AccountId] nvarchar(450) NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Candidates] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[CareerSites] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [DraftJson] nvarchar(max) NOT NULL,
    [PublishedJson] nvarchar(max) NULL,
    [PublishedVersion] int NOT NULL,
    [PublishedUtc] datetimeoffset NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_CareerSites] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[CompanyBankAccounts] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [BankName] nvarchar(80) NOT NULL,
    [AccountLabel] nvarchar(60) NOT NULL,
    [AccountNumberProtected] nvarchar(1000) NOT NULL,
    [AccountLast4] nchar(4) NOT NULL,
    [Ifsc] nvarchar(11) NOT NULL,
    [BranchName] nvarchar(120) NULL,
    [IsDefault] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_CompanyBankAccounts] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[Designations] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [Code] nvarchar(40) NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [GradeCode] nvarchar(80) NOT NULL,
    [DepartmentCode] nvarchar(80) NOT NULL,
    [Description] nvarchar(300) NULL,
    [SortOrder] int NOT NULL,
    [IsActive] bit NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Designations] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[EmployeeAppSettings] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [AllowAppPunch] bit NOT NULL,
    [RequireLocationForPunch] bit NOT NULL,
    [AllowGatePassRequests] bit NOT NULL,
    [AllowAttendanceCorrections] bit NOT NULL,
    [ShowSalaryDetails] bit NOT NULL,
    [ShowBankDetails] bit NOT NULL,
    [ShowOnboarding] bit NOT NULL,
    [AllowProfileChangeRequests] bit NOT NULL,
    [AttendanceTargetPercent] int NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(450) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeAppSettings] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [employee].[EmployeeBankAccounts] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [BankName] nvarchar(80) NOT NULL,
    [AccountHolderName] nvarchar(120) NOT NULL,
    [AccountNumberProtected] nvarchar(1000) NOT NULL,
    [AccountLast4] nchar(4) NOT NULL,
    [Ifsc] nvarchar(11) NOT NULL,
    [AccountType] nvarchar(20) NOT NULL,
    [UpdatedBy] nvarchar(450) NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeBankAccounts] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [employee].[EmployeeChangeRequests] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [Section] nvarchar(40) NOT NULL,
    [ChangesJson] nvarchar(max) NOT NULL,
    [Reason] nvarchar(1000) NULL,
    [Status] nvarchar(20) NOT NULL,
    [DecisionReason] nvarchar(1000) NULL,
    [RequestedBy] nvarchar(450) NULL,
    [DecidedBy] nvarchar(450) NULL,
    [SubmittedUtc] datetimeoffset NOT NULL,
    [DecidedUtc] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeChangeRequests] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [employee].[EmployeeContacts] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [Kind] nvarchar(20) NOT NULL,
    [Name] nvarchar(160) NOT NULL,
    [Relationship] nvarchar(60) NULL,
    [Phone] nvarchar(32) NULL,
    [Email] nvarchar(320) NULL,
    [DateOfBirth] date NULL,
    [IsPrimary] bit NOT NULL,
    [IsVerified] bit NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeContacts] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [employee].[EmployeeDependants] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [DisplayName] nvarchar(160) NOT NULL,
    [RelationCode] nvarchar(40) NOT NULL,
    [DateOfBirth] date NULL,
    [IsBenefitEligible] bit NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeDependants] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [employee].[Employees] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeNumber] nvarchar(60) NOT NULL,
    [LegalFirstName] nvarchar(120) NOT NULL,
    [LegalMiddleName] nvarchar(120) NULL,
    [LegalLastName] nvarchar(120) NOT NULL,
    [PreferredName] nvarchar(120) NULL,
    [DateOfBirth] date NULL,
    [GenderCode] nvarchar(40) NULL,
    [MaritalStatusCode] nvarchar(40) NULL,
    [NationalityCode] nvarchar(80) NULL,
    [Email] nvarchar(320) NULL,
    [Phone] nvarchar(32) NULL,
    [AvatarUrl] nvarchar(500) NULL,
    [JoinedOn] date NOT NULL,
    [EmploymentTypeCode] nvarchar(40) NOT NULL,
    [EmploymentStatusCode] nvarchar(40) NOT NULL,
    [GradeCode] nvarchar(40) NULL,
    [JobTitle] nvarchar(180) NULL,
    [ProbationEndsOn] date NULL,
    [LeavingOn] date NULL,
    [CompanyCode] nvarchar(64) NULL,
    [DivisionCode] nvarchar(64) NULL,
    [DepartmentCode] nvarchar(64) NOT NULL,
    [LocationCode] nvarchar(64) NOT NULL,
    [ManagerEmployeeId] bigint NULL,
    [CostCentre] nvarchar(64) NULL,
    [AccountId] nvarchar(450) NULL,
    [CandidateId] bigint NULL,
    [IsActive] bit NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [employee].[EmployeeTimeline] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [EventType] nvarchar(40) NOT NULL,
    [Title] nvarchar(180) NOT NULL,
    [Summary] nvarchar(1000) NOT NULL,
    [EffectiveOn] date NOT NULL,
    [ActorId] nvarchar(450) NULL,
    [ActorName] nvarchar(160) NULL,
    [OccurredUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeTimeline] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[GatePasses] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [PassType] nvarchar(20) NOT NULL,
    [FromDate] date NOT NULL,
    [ToDate] date NOT NULL,
    [FromTime] time NULL,
    [ToTime] time NULL,
    [Purpose] nvarchar(500) NOT NULL,
    [DestinationLabel] nvarchar(200) NULL,
    [State] nvarchar(20) NOT NULL,
    [VerificationToken] nvarchar(64) NULL,
    [DecidedByUserId] nvarchar(450) NULL,
    [DecidedByName] nvarchar(200) NULL,
    [DecidedUtc] datetimeoffset NULL,
    [DecisionComment] nvarchar(2000) NULL,
    [RequestedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_GatePasses] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[GatePassScans] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [GatePassId] bigint NOT NULL,
    [Direction] nvarchar(10) NOT NULL,
    [ScannedUtc] datetimeoffset NOT NULL,
    [ScannedBy] nvarchar(450) NULL,
    [GateLabel] nvarchar(120) NULL,
    CONSTRAINT [PK_GatePassScans] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[GradeSalaryStructures] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [GradeCode] nvarchar(80) NOT NULL,
    [BasicMonthly] decimal(18,2) NOT NULL,
    [ComponentsJson] nvarchar(max) NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_GradeSalaryStructures] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [recruitment].[InterviewEvaluations] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [InterviewId] bigint NOT NULL,
    [PanelMemberId] nvarchar(450) NOT NULL,
    [ScoresJson] nvarchar(max) NOT NULL,
    [Recommendation] nvarchar(40) NULL,
    [Notes] nvarchar(4000) NULL,
    [State] nvarchar(20) NOT NULL,
    [SubmittedUtc] datetimeoffset NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_InterviewEvaluations] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [recruitment].[InterviewPanelists] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [InterviewId] bigint NOT NULL,
    [PanelMemberId] nvarchar(450) NOT NULL,
    [DisplayName] nvarchar(160) NOT NULL,
    [IsLead] bit NOT NULL,
    [AddedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_InterviewPanelists] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [recruitment].[Interviews] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [ApplicationId] bigint NOT NULL,
    [CandidateId] bigint NOT NULL,
    [JobOpeningId] bigint NOT NULL,
    [RoundCode] nvarchar(40) NOT NULL,
    [RoundLabel] nvarchar(120) NOT NULL,
    [StartUtc] datetimeoffset NOT NULL,
    [DurationMinutes] int NOT NULL,
    [TimeZoneId] nvarchar(64) NOT NULL,
    [Mode] nvarchar(20) NOT NULL,
    [LocationOrLink] nvarchar(400) NULL,
    [Instructions] nvarchar(2000) NULL,
    [Status] nvarchar(20) NOT NULL,
    [CancellationReasonCode] nvarchar(40) NULL,
    [CancellationComment] nvarchar(1000) NULL,
    [ScheduledBy] nvarchar(450) NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Interviews] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [recruitment].[JobOpenings] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [Slug] nvarchar(180) NOT NULL,
    [Title] nvarchar(180) NOT NULL,
    [Department] nvarchar(120) NOT NULL,
    [Location] nvarchar(180) NOT NULL,
    [EmploymentType] nvarchar(80) NOT NULL,
    [WorkMode] nvarchar(80) NOT NULL,
    [Experience] nvarchar(80) NOT NULL,
    [Summary] nvarchar(1200) NOT NULL,
    [ResponsibilitiesJson] nvarchar(max) NOT NULL,
    [RequirementsJson] nvarchar(max) NOT NULL,
    [Status] nvarchar(40) NOT NULL DEFAULT N'Draft',
    [IsPublished] bit NOT NULL,
    [PublishedUtc] datetimeoffset NULL,
    [ExpiresOn] date NULL,
    [SalaryMinimum] decimal(18,2) NULL,
    [SalaryMaximum] decimal(18,2) NULL,
    [SalaryCurrencyCode] nvarchar(3) NULL,
    [RequiredDocumentCodesJson] nvarchar(max) NOT NULL,
    [ScreeningQuestionsJson] nvarchar(max) NOT NULL,
    [StatusChangedUtc] datetimeoffset NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_JobOpenings] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[LeaveAdjustments] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [Kind] nvarchar(20) NOT NULL,
    [CreditIdsJson] nvarchar(max) NOT NULL,
    [Days] decimal(9,2) NOT NULL,
    [Amount] decimal(18,2) NULL,
    [Currency] nvarchar(3) NULL,
    [State] nvarchar(20) NOT NULL,
    [Comment] nvarchar(2000) NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [DecidedUtc] datetimeoffset NULL,
    CONSTRAINT [PK_LeaveAdjustments] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[LeaveBalances] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [PolicyId] bigint NOT NULL,
    [Year] int NOT NULL,
    [Entitled] decimal(9,2) NOT NULL,
    [Accrued] decimal(9,2) NOT NULL,
    [CarriedForward] decimal(9,2) NOT NULL,
    [AdjustmentDays] decimal(9,2) NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_LeaveBalances] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[LeaveCredits] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [PolicyId] bigint NOT NULL,
    [SourceCode] nvarchar(20) NOT NULL,
    [EarnedOn] date NOT NULL,
    [ExpiresOn] date NOT NULL,
    [Days] decimal(9,2) NOT NULL,
    [IsEncashable] bit NOT NULL,
    [EncashmentAmount] decimal(18,2) NULL,
    [EncashmentCurrency] nvarchar(3) NULL,
    [ConsumedByAdjustmentId] bigint NULL,
    CONSTRAINT [PK_LeaveCredits] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[LeaveHolidays] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [Date] date NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [LocationCode] nvarchar(40) NULL,
    CONSTRAINT [PK_LeaveHolidays] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[LeavePolicies] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [Code] nvarchar(40) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [ToneKey] nvarchar(20) NOT NULL DEFAULT N'blue',
    [Emoji] nvarchar(16) NOT NULL DEFAULT N'🗓️',
    [Unit] nvarchar(20) NOT NULL,
    [Version] nvarchar(20) NOT NULL,
    [AnnualEntitlement] decimal(9,2) NOT NULL,
    [AccrualMode] nvarchar(20) NOT NULL,
    [CarryForwardCap] decimal(9,2) NOT NULL,
    [NoticeDays] int NOT NULL,
    [DocumentRequiredAboveDays] decimal(9,2) NULL,
    [AllowsHalfDay] bit NOT NULL,
    [AllowsNegativeBalance] bit NOT NULL,
    [CountsNonWorkingDays] bit NOT NULL,
    [EligibilityText] nvarchar(2000) NOT NULL,
    [AccrualRuleText] nvarchar(2000) NOT NULL,
    [CarryForwardRuleText] nvarchar(2000) NOT NULL,
    [NoticeRuleText] nvarchar(2000) NOT NULL,
    [DocumentsJson] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_LeavePolicies] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[LeaveRequestComments] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [RequestId] bigint NOT NULL,
    [Kind] nvarchar(20) NOT NULL,
    [AuthorUserId] nvarchar(450) NULL,
    [AuthorName] nvarchar(200) NOT NULL,
    [Comment] nvarchar(2000) NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_LeaveRequestComments] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[LeaveRequestDays] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [RequestId] bigint NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [Date] date NOT NULL,
    [Portion] decimal(4,2) NOT NULL,
    CONSTRAINT [PK_LeaveRequestDays] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[LeaveRequests] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [PolicyId] bigint NOT NULL,
    [LeaveTypeCode] nvarchar(40) NOT NULL,
    [FromDate] date NOT NULL,
    [ToDate] date NOT NULL,
    [IsFirstDayPartial] bit NOT NULL,
    [IsLastDayPartial] bit NOT NULL,
    [Days] decimal(9,2) NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    [State] nvarchar(20) NOT NULL,
    [AttachmentsJson] nvarchar(max) NOT NULL,
    [SubmittedUtc] datetimeoffset NOT NULL,
    [DecidedByUserId] nvarchar(450) NULL,
    [DecidedByName] nvarchar(200) NULL,
    [DecidedUtc] datetimeoffset NULL,
    [DecisionReasonCode] nvarchar(40) NULL,
    [DecisionComment] nvarchar(2000) NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_LeaveRequests] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [payroll].[LoanRepayments] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [LoanId] bigint NOT NULL,
    [Year] int NOT NULL,
    [Month] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Source] nvarchar(20) NOT NULL,
    [PayrollRunId] bigint NULL,
    [RecordedOn] date NOT NULL,
    [Note] nvarchar(300) NULL,
    [RecordedBy] nvarchar(450) NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_LoanRepayments] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [payroll].[Loans] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [VoucherNo] nvarchar(20) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [Type] nvarchar(20) NOT NULL,
    [Principal] decimal(18,2) NOT NULL,
    [InterestPercent] decimal(5,2) NOT NULL,
    [TotalRepayable] decimal(18,2) NOT NULL,
    [Installments] int NOT NULL,
    [MonthlyInstallment] decimal(18,2) NOT NULL,
    [StartYear] int NOT NULL,
    [StartMonth] int NOT NULL,
    [Purpose] nvarchar(300) NULL,
    [AdvanceTypeCode] nvarchar(40) NULL,
    [RequestedOn] date NOT NULL,
    [DisbursedOn] date NULL,
    [Status] nvarchar(20) NOT NULL,
    [RequestedBy] nvarchar(450) NULL,
    [DecidedBy] nvarchar(450) NULL,
    [DecidedUtc] datetimeoffset NULL,
    [DecisionNote] nvarchar(300) NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Loans] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[MailSettings] (
    [Id] bigint NOT NULL IDENTITY,
    [Enabled] bit NOT NULL,
    [SenderBaseUrl] nvarchar(400) NOT NULL,
    [ReceiverBaseUrl] nvarchar(400) NULL,
    [PublicBaseUrl] nvarchar(400) NOT NULL,
    [ProtectedApiKey] nvarchar(1000) NULL,
    [TimeoutSeconds] int NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [UpdatedBy] nvarchar(160) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_MailSettings] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[MasterOptions] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [Category] nvarchar(80) NOT NULL,
    [Code] nvarchar(80) NOT NULL,
    [Name] nvarchar(160) NOT NULL,
    [SortOrder] int NOT NULL,
    [ParentCode] nvarchar(80) NULL,
    [Description] nvarchar(500) NULL,
    [IsActive] bit NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_MasterOptions] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[MediaAssets] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [OriginalFileName] nvarchar(260) NOT NULL,
    [StoredFileName] nvarchar(260) NOT NULL,
    [StoragePath] nvarchar(600) NOT NULL,
    [ContentType] nvarchar(120) NOT NULL,
    [Length] bigint NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_MediaAssets] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[MovementRecords] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [Kind] nvarchar(20) NOT NULL,
    [WorkDate] date NOT NULL,
    [GoingOutUtc] datetimeoffset NOT NULL,
    [ExpectedReturnUtc] datetimeoffset NOT NULL,
    [ActualReturnUtc] datetimeoffset NULL,
    [Detail] nvarchar(500) NOT NULL,
    [DestinationLabel] nvarchar(200) NULL,
    [State] nvarchar(20) NOT NULL,
    [RecordedBy] nvarchar(450) NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_MovementRecords] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[Notifications] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [RecipientId] nvarchar(450) NOT NULL,
    [Category] nvarchar(40) NOT NULL,
    [Title] nvarchar(180) NOT NULL,
    [Body] nvarchar(2000) NOT NULL,
    [Priority] nvarchar(20) NOT NULL,
    [ActionRoute] nvarchar(400) NULL,
    [SubjectType] nvarchar(40) NULL,
    [SubjectId] nvarchar(64) NULL,
    [OccurredUtc] datetimeoffset NOT NULL,
    [ReadUtc] datetimeoffset NULL,
    [DeletedUtc] datetimeoffset NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [recruitment].[Onboardings] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [ApplicationId] bigint NOT NULL,
    [CandidateId] bigint NOT NULL,
    [Stage] nvarchar(40) NOT NULL,
    [JoiningOn] date NULL,
    [StartedUtc] datetimeoffset NOT NULL,
    [CompletedUtc] datetimeoffset NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Onboardings] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [recruitment].[OnboardingTasks] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [OnboardingId] bigint NOT NULL,
    [Code] nvarchar(80) NOT NULL,
    [Title] nvarchar(180) NOT NULL,
    [Instructions] nvarchar(1000) NOT NULL,
    [TaskType] nvarchar(20) NOT NULL,
    [OwnerKind] nvarchar(20) NOT NULL,
    [IsRequired] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [DocumentId] bigint NULL,
    [FormValuesJson] nvarchar(max) NULL,
    [DecisionReason] nvarchar(1000) NULL,
    [DecidedBy] nvarchar(450) NULL,
    [DecidedUtc] datetimeoffset NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_OnboardingTasks] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[OnDutyRequests] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [Purpose] nvarchar(200) NOT NULL,
    [FromDate] date NOT NULL,
    [ToDate] date NOT NULL,
    [Location] nvarchar(200) NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    [State] nvarchar(20) NOT NULL,
    [DecidedByUserId] nvarchar(450) NULL,
    [DecidedByName] nvarchar(200) NULL,
    [DecidedUtc] datetimeoffset NULL,
    [DecisionComment] nvarchar(2000) NULL,
    [RequestedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_OnDutyRequests] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [payroll].[PayrollLines] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [RunId] bigint NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [EmployeeNumber] nvarchar(60) NOT NULL,
    [DisplayName] nvarchar(160) NOT NULL,
    [DivisionCode] nvarchar(80) NOT NULL,
    [DepartmentCode] nvarchar(80) NOT NULL,
    [Designation] nvarchar(160) NOT NULL,
    [GradeCode] nvarchar(80) NOT NULL,
    [ShiftCode] nvarchar(40) NULL,
    [WorkedDays] int NOT NULL,
    [ExpectedDays] int NOT NULL,
    [PayableDays] int NOT NULL,
    [DaysInMonth] int NOT NULL,
    [Basic] decimal(18,2) NOT NULL,
    [Hra] decimal(18,2) NOT NULL,
    [Conveyance] decimal(18,2) NOT NULL,
    [OtherAllowances] decimal(18,2) NOT NULL,
    [Gross] decimal(18,2) NOT NULL,
    [Pf] decimal(18,2) NOT NULL,
    [Esi] decimal(18,2) NOT NULL,
    [Tds] decimal(18,2) NOT NULL,
    [OtherDeductions] decimal(18,2) NOT NULL,
    [Deductions] decimal(18,2) NOT NULL,
    [NetPayable] decimal(18,2) NOT NULL,
    [BreakdownJson] nvarchar(max) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [PaidOn] date NULL,
    [PaymentMode] nvarchar(30) NULL,
    [DocumentType] nvarchar(30) NULL,
    [TransferBatchId] bigint NULL,
    [UpdatedBy] nvarchar(450) NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_PayrollLines] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [payroll].[PayrollRuns] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [Year] int NOT NULL,
    [Month] int NOT NULL,
    [SalaryType] nvarchar(30) NOT NULL,
    [GeneratedUtc] datetimeoffset NOT NULL,
    [GeneratedBy] nvarchar(450) NULL,
    [ApprovedUtc] datetimeoffset NULL,
    [ApprovedBy] nvarchar(450) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_PayrollRuns] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[Policies] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [Code] nvarchar(40) NOT NULL,
    [Title] nvarchar(120) NOT NULL,
    [Icon] nvarchar(16) NOT NULL,
    [Excerpt] nvarchar(400) NOT NULL,
    [SectionsJson] nvarchar(max) NOT NULL,
    [Version] nvarchar(20) NOT NULL,
    [IsPublished] bit NOT NULL,
    [UpdatedOn] date NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Policies] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[PolicyAcknowledgements] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] nvarchar(80) NOT NULL,
    [PolicyId] bigint NOT NULL,
    [Version] nvarchar(20) NOT NULL,
    [AcknowledgedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PolicyAcknowledgements] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[ShiftAssignments] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [EmployeeId] bigint NOT NULL,
    [ShiftDefinitionId] bigint NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [AssignedUtc] datetimeoffset NOT NULL,
    [AssignedBy] nvarchar(450) NULL,
    [Note] nvarchar(500) NULL,
    CONSTRAINT [PK_ShiftAssignments] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [workforce].[ShiftDefinitions] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [Code] nvarchar(40) NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [BreakMinutes] int NOT NULL,
    [GraceMinutes] int NOT NULL,
    [WorkingDays] nvarchar(7) NOT NULL,
    [CrossesMidnight] bit NOT NULL,
    [IsDefault] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ShiftDefinitions] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[Tenants] (
    [Id] bigint NOT NULL IDENTITY,
    [TenantKey] nvarchar(80) NOT NULL,
    [DisplayName] nvarchar(160) NOT NULL,
    [PrimaryDomain] nvarchar(240) NULL,
    [TimeZoneId] nvarchar(80) NOT NULL,
    [RetirementAgeYears] int NOT NULL,
    [HrPortalEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [IsActive] bit NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [platform].[ThemeTemplates] (
    [TemplateKey] nvarchar(80) NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [Description] nvarchar(400) NOT NULL,
    [PrimaryColor] nvarchar(7) NOT NULL,
    [AccentColor] nvarchar(7) NOT NULL,
    [SurfaceColor] nvarchar(7) NOT NULL,
    [FontFamily] nvarchar(160) NOT NULL,
    [HeadingFontFamily] nvarchar(160) NOT NULL,
    [BorderRadius] int NOT NULL,
    [PreviewGradient] nvarchar(240) NOT NULL,
    [SortOrder] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_ThemeTemplates] PRIMARY KEY ([TemplateKey])
);
GO


CREATE INDEX [IX_ApplicationDocuments_TenantKey_ApplicationId] ON [recruitment].[ApplicationDocuments] ([TenantKey], [ApplicationId]);
GO


CREATE INDEX [IX_ApplicationEvents_TenantKey_ApplicationId] ON [recruitment].[ApplicationEvents] ([TenantKey], [ApplicationId]);
GO


CREATE INDEX [IX_Applications_TenantKey_CandidateId] ON [recruitment].[Applications] ([TenantKey], [CandidateId]);
GO


CREATE UNIQUE INDEX [IX_Applications_TenantKey_JobOpeningId_CandidateId] ON [recruitment].[Applications] ([TenantKey], [JobOpeningId], [CandidateId]);
GO


CREATE INDEX [IX_Applications_TenantKey_Stage] ON [recruitment].[Applications] ([TenantKey], [Stage]);
GO


CREATE INDEX [IX_AttendanceCorrections_TenantKey_EmployeeId_WorkDate] ON [workforce].[AttendanceCorrections] ([TenantKey], [EmployeeId], [WorkDate]);
GO


CREATE INDEX [IX_AttendanceCorrections_TenantKey_State] ON [workforce].[AttendanceCorrections] ([TenantKey], [State]);
GO


CREATE UNIQUE INDEX [IX_AttendanceDays_TenantKey_EmployeeId_WorkDate] ON [workforce].[AttendanceDays] ([TenantKey], [EmployeeId], [WorkDate]);
GO


CREATE INDEX [IX_AttendanceDays_TenantKey_WorkDate_Status] ON [workforce].[AttendanceDays] ([TenantKey], [WorkDate], [Status]);
GO


CREATE INDEX [IX_AttendanceExceptions_TenantKey_EmployeeId_WorkDate] ON [workforce].[AttendanceExceptions] ([TenantKey], [EmployeeId], [WorkDate]);
GO


CREATE UNIQUE INDEX [IX_AttendanceExceptions_TenantKey_EmployeeId_WorkDate_ExceptionType] ON [workforce].[AttendanceExceptions] ([TenantKey], [EmployeeId], [WorkDate], [ExceptionType]);
GO


CREATE INDEX [IX_AttendanceExceptions_TenantKey_State] ON [workforce].[AttendanceExceptions] ([TenantKey], [State]);
GO


CREATE UNIQUE INDEX [IX_AttendancePunches_TenantKey_EmployeeId_ClientEventId] ON [workforce].[AttendancePunches] ([TenantKey], [EmployeeId], [ClientEventId]) WHERE [ClientEventId] IS NOT NULL;
GO


CREATE INDEX [IX_AttendancePunches_TenantKey_EmployeeId_WorkDate] ON [workforce].[AttendancePunches] ([TenantKey], [EmployeeId], [WorkDate]);
GO


CREATE UNIQUE INDEX [IX_BankTransferBatches_TenantKey_ReferenceNo] ON [payroll].[BankTransferBatches] ([TenantKey], [ReferenceNo]);
GO


CREATE INDEX [IX_BankTransferBatches_TenantKey_RunId] ON [payroll].[BankTransferBatches] ([TenantKey], [RunId]);
GO


CREATE UNIQUE INDEX [IX_Candidates_TenantKey_Email] ON [recruitment].[Candidates] ([TenantKey], [Email]);
GO


CREATE UNIQUE INDEX [IX_CareerSites_TenantKey] ON [platform].[CareerSites] ([TenantKey]);
GO


CREATE UNIQUE INDEX [IX_CompanyBankAccounts_TenantKey_AccountLabel] ON [platform].[CompanyBankAccounts] ([TenantKey], [AccountLabel]);
GO


CREATE UNIQUE INDEX [IX_Designations_TenantKey_Code] ON [platform].[Designations] ([TenantKey], [Code]);
GO


CREATE INDEX [IX_Designations_TenantKey_SortOrder] ON [platform].[Designations] ([TenantKey], [SortOrder]);
GO


CREATE UNIQUE INDEX [IX_EmployeeAppSettings_TenantKey] ON [platform].[EmployeeAppSettings] ([TenantKey]);
GO


CREATE UNIQUE INDEX [IX_EmployeeBankAccounts_TenantKey_EmployeeId] ON [employee].[EmployeeBankAccounts] ([TenantKey], [EmployeeId]);
GO


CREATE INDEX [IX_EmployeeChangeRequests_TenantKey_EmployeeId] ON [employee].[EmployeeChangeRequests] ([TenantKey], [EmployeeId]);
GO


CREATE INDEX [IX_EmployeeChangeRequests_TenantKey_Status] ON [employee].[EmployeeChangeRequests] ([TenantKey], [Status]);
GO


CREATE INDEX [IX_EmployeeContacts_TenantKey_EmployeeId] ON [employee].[EmployeeContacts] ([TenantKey], [EmployeeId]);
GO


CREATE INDEX [IX_EmployeeDependants_TenantKey_EmployeeId] ON [employee].[EmployeeDependants] ([TenantKey], [EmployeeId]);
GO


CREATE INDEX [IX_Employees_CandidateId] ON [employee].[Employees] ([CandidateId]) WHERE [CandidateId] IS NOT NULL;
GO


CREATE INDEX [IX_Employees_TenantKey_DepartmentCode] ON [employee].[Employees] ([TenantKey], [DepartmentCode]);
GO


CREATE UNIQUE INDEX [IX_Employees_TenantKey_Email] ON [employee].[Employees] ([TenantKey], [Email]) WHERE [Email] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_Employees_TenantKey_EmployeeNumber] ON [employee].[Employees] ([TenantKey], [EmployeeNumber]);
GO


CREATE INDEX [IX_Employees_TenantKey_EmploymentStatusCode] ON [employee].[Employees] ([TenantKey], [EmploymentStatusCode]);
GO


CREATE INDEX [IX_Employees_TenantKey_LocationCode] ON [employee].[Employees] ([TenantKey], [LocationCode]);
GO


CREATE INDEX [IX_Employees_TenantKey_ManagerEmployeeId] ON [employee].[Employees] ([TenantKey], [ManagerEmployeeId]);
GO


CREATE INDEX [IX_EmployeeTimeline_TenantKey_EmployeeId_OccurredUtc] ON [employee].[EmployeeTimeline] ([TenantKey], [EmployeeId], [OccurredUtc]);
GO


CREATE INDEX [IX_GatePasses_TenantKey_EmployeeId_FromDate] ON [workforce].[GatePasses] ([TenantKey], [EmployeeId], [FromDate]);
GO


CREATE INDEX [IX_GatePasses_TenantKey_State] ON [workforce].[GatePasses] ([TenantKey], [State]);
GO


CREATE UNIQUE INDEX [IX_GatePasses_TenantKey_VerificationToken] ON [workforce].[GatePasses] ([TenantKey], [VerificationToken]) WHERE [VerificationToken] IS NOT NULL;
GO


CREATE INDEX [IX_GatePassScans_TenantKey_GatePassId] ON [workforce].[GatePassScans] ([TenantKey], [GatePassId]);
GO


CREATE UNIQUE INDEX [IX_GradeSalaryStructures_TenantKey_GradeCode] ON [platform].[GradeSalaryStructures] ([TenantKey], [GradeCode]);
GO


CREATE UNIQUE INDEX [IX_InterviewEvaluations_InterviewId_PanelMemberId] ON [recruitment].[InterviewEvaluations] ([InterviewId], [PanelMemberId]);
GO


CREATE UNIQUE INDEX [IX_InterviewPanelists_InterviewId_PanelMemberId] ON [recruitment].[InterviewPanelists] ([InterviewId], [PanelMemberId]);
GO


CREATE INDEX [IX_InterviewPanelists_TenantKey_InterviewId] ON [recruitment].[InterviewPanelists] ([TenantKey], [InterviewId]);
GO


CREATE INDEX [IX_InterviewPanelists_TenantKey_PanelMemberId] ON [recruitment].[InterviewPanelists] ([TenantKey], [PanelMemberId]);
GO


CREATE INDEX [IX_Interviews_TenantKey_ApplicationId] ON [recruitment].[Interviews] ([TenantKey], [ApplicationId]);
GO


CREATE INDEX [IX_Interviews_TenantKey_StartUtc] ON [recruitment].[Interviews] ([TenantKey], [StartUtc]);
GO


CREATE INDEX [IX_Interviews_TenantKey_Status] ON [recruitment].[Interviews] ([TenantKey], [Status]);
GO


CREATE INDEX [IX_JobOpenings_TenantKey_IsPublished] ON [recruitment].[JobOpenings] ([TenantKey], [IsPublished]);
GO


CREATE UNIQUE INDEX [IX_JobOpenings_TenantKey_Slug] ON [recruitment].[JobOpenings] ([TenantKey], [Slug]);
GO


CREATE INDEX [IX_JobOpenings_TenantKey_Status] ON [recruitment].[JobOpenings] ([TenantKey], [Status]);
GO


CREATE INDEX [IX_LeaveAdjustments_TenantKey_EmployeeId_State] ON [workforce].[LeaveAdjustments] ([TenantKey], [EmployeeId], [State]);
GO


CREATE UNIQUE INDEX [IX_LeaveBalances_TenantKey_EmployeeId_PolicyId_Year] ON [workforce].[LeaveBalances] ([TenantKey], [EmployeeId], [PolicyId], [Year]);
GO


CREATE INDEX [IX_LeaveCredits_TenantKey_EmployeeId_ExpiresOn] ON [workforce].[LeaveCredits] ([TenantKey], [EmployeeId], [ExpiresOn]);
GO


CREATE UNIQUE INDEX [IX_LeaveHolidays_TenantKey_Date_LocationCode] ON [workforce].[LeaveHolidays] ([TenantKey], [Date], [LocationCode]) WHERE [LocationCode] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_LeavePolicies_TenantKey_Code] ON [workforce].[LeavePolicies] ([TenantKey], [Code]);
GO


CREATE INDEX [IX_LeaveRequestComments_TenantKey_RequestId] ON [workforce].[LeaveRequestComments] ([TenantKey], [RequestId]);
GO


CREATE INDEX [IX_LeaveRequestDays_TenantKey_EmployeeId_Date] ON [workforce].[LeaveRequestDays] ([TenantKey], [EmployeeId], [Date]);
GO


CREATE INDEX [IX_LeaveRequestDays_TenantKey_RequestId] ON [workforce].[LeaveRequestDays] ([TenantKey], [RequestId]);
GO


CREATE INDEX [IX_LeaveRequests_TenantKey_EmployeeId_FromDate] ON [workforce].[LeaveRequests] ([TenantKey], [EmployeeId], [FromDate]);
GO


CREATE INDEX [IX_LeaveRequests_TenantKey_State] ON [workforce].[LeaveRequests] ([TenantKey], [State]);
GO


CREATE INDEX [IX_LoanRepayments_TenantKey_LoanId] ON [payroll].[LoanRepayments] ([TenantKey], [LoanId]);
GO


CREATE UNIQUE INDEX [IX_LoanRepayments_TenantKey_LoanId_Year_Month_Source] ON [payroll].[LoanRepayments] ([TenantKey], [LoanId], [Year], [Month], [Source]) WHERE [Source] = 'Payroll';
GO


CREATE INDEX [IX_Loans_TenantKey_EmployeeId_Status] ON [payroll].[Loans] ([TenantKey], [EmployeeId], [Status]);
GO


CREATE UNIQUE INDEX [IX_Loans_TenantKey_VoucherNo] ON [payroll].[Loans] ([TenantKey], [VoucherNo]);
GO


CREATE UNIQUE INDEX [IX_MasterOptions_TenantKey_Category_Code] ON [platform].[MasterOptions] ([TenantKey], [Category], [Code]);
GO


CREATE INDEX [IX_MasterOptions_TenantKey_Category_IsActive] ON [platform].[MasterOptions] ([TenantKey], [Category], [IsActive]);
GO


CREATE INDEX [IX_MediaAssets_TenantKey_CreatedUtc] ON [platform].[MediaAssets] ([TenantKey], [CreatedUtc]);
GO


CREATE UNIQUE INDEX [IX_MovementRecords_OneOpenPerEmployee] ON [workforce].[MovementRecords] ([TenantKey], [EmployeeId]) WHERE [State] = 'Open' AND [ActualReturnUtc] IS NULL;
GO


CREATE INDEX [IX_MovementRecords_TenantKey_EmployeeId_WorkDate] ON [workforce].[MovementRecords] ([TenantKey], [EmployeeId], [WorkDate]);
GO


CREATE INDEX [IX_MovementRecords_TenantKey_State] ON [workforce].[MovementRecords] ([TenantKey], [State]);
GO


CREATE INDEX [IX_Notifications_TenantKey_RecipientId_OccurredUtc] ON [platform].[Notifications] ([TenantKey], [RecipientId], [OccurredUtc]);
GO


CREATE INDEX [IX_Notifications_TenantKey_RecipientId_ReadUtc] ON [platform].[Notifications] ([TenantKey], [RecipientId], [ReadUtc]);
GO


CREATE INDEX [IX_Notifications_TenantKey_RecipientId_SubjectType_SubjectId] ON [platform].[Notifications] ([TenantKey], [RecipientId], [SubjectType], [SubjectId]);
GO


CREATE UNIQUE INDEX [IX_Onboardings_TenantKey_ApplicationId] ON [recruitment].[Onboardings] ([TenantKey], [ApplicationId]);
GO


CREATE INDEX [IX_Onboardings_TenantKey_Stage] ON [recruitment].[Onboardings] ([TenantKey], [Stage]);
GO


CREATE INDEX [IX_OnboardingTasks_TenantKey_OnboardingId] ON [recruitment].[OnboardingTasks] ([TenantKey], [OnboardingId]);
GO


CREATE INDEX [IX_OnDutyRequests_TenantKey_EmployeeId_FromDate] ON [workforce].[OnDutyRequests] ([TenantKey], [EmployeeId], [FromDate]);
GO


CREATE INDEX [IX_OnDutyRequests_TenantKey_State] ON [workforce].[OnDutyRequests] ([TenantKey], [State]);
GO


CREATE UNIQUE INDEX [IX_PayrollLines_TenantKey_RunId_EmployeeId] ON [payroll].[PayrollLines] ([TenantKey], [RunId], [EmployeeId]);
GO


CREATE INDEX [IX_PayrollLines_TenantKey_RunId_Status] ON [payroll].[PayrollLines] ([TenantKey], [RunId], [Status]);
GO


CREATE UNIQUE INDEX [IX_PayrollRuns_TenantKey_Year_Month_SalaryType] ON [payroll].[PayrollRuns] ([TenantKey], [Year], [Month], [SalaryType]);
GO


CREATE UNIQUE INDEX [IX_Policies_TenantKey_Code] ON [platform].[Policies] ([TenantKey], [Code]);
GO


CREATE INDEX [IX_Policies_TenantKey_IsPublished] ON [platform].[Policies] ([TenantKey], [IsPublished]);
GO


CREATE UNIQUE INDEX [IX_PolicyAcknowledgements_TenantKey_EmployeeId_PolicyId] ON [platform].[PolicyAcknowledgements] ([TenantKey], [EmployeeId], [PolicyId]);
GO


CREATE INDEX [IX_ShiftAssignments_TenantKey_EmployeeId_EffectiveFrom] ON [workforce].[ShiftAssignments] ([TenantKey], [EmployeeId], [EffectiveFrom]);
GO


CREATE UNIQUE INDEX [IX_ShiftDefinitions_OneDefaultPerTenant] ON [workforce].[ShiftDefinitions] ([TenantKey]) WHERE [IsDefault] = 1;
GO


CREATE UNIQUE INDEX [IX_ShiftDefinitions_TenantKey_Code] ON [workforce].[ShiftDefinitions] ([TenantKey], [Code]);
GO


CREATE UNIQUE INDEX [IX_Tenants_PrimaryDomain] ON [platform].[Tenants] ([PrimaryDomain]) WHERE [PrimaryDomain] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_Tenants_TenantKey] ON [platform].[Tenants] ([TenantKey]);
GO


