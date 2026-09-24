using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.Admin;
using Vispl.HRManagement.DL.Employee;
using Vispl.HRManagement.DL.Workforce;
using Vispl.HRManagement.DL.Recruitment;

namespace Vispl.HRManagement.DL.Shared;

public sealed class HrManagementDbContext(DbContextOptions<HrManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<CareerSiteEntity> CareerSites => Set<CareerSiteEntity>();
    public DbSet<MailSettingsEntity> MailSettings => Set<MailSettingsEntity>();
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<MasterOptionEntity> MasterOptions => Set<MasterOptionEntity>();
    public DbSet<PolicyEntity> Policies => Set<PolicyEntity>();
    public DbSet<PolicyAcknowledgementEntity> PolicyAcknowledgements => Set<PolicyAcknowledgementEntity>();
    public DbSet<DesignationEntity> Designations => Set<DesignationEntity>();
    public DbSet<GradeSalaryEntity> GradeSalaryStructures => Set<GradeSalaryEntity>();
    public DbSet<CompanyBankAccountEntity> CompanyBankAccounts => Set<CompanyBankAccountEntity>();
    public DbSet<EmployeeBankAccountEntity> EmployeeBankAccounts => Set<EmployeeBankAccountEntity>();
    public DbSet<PayrollRunEntity> PayrollRuns => Set<PayrollRunEntity>();
    public DbSet<PayrollLineEntity> PayrollLines => Set<PayrollLineEntity>();
    public DbSet<BankTransferBatchEntity> BankTransferBatches => Set<BankTransferBatchEntity>();
    public DbSet<LoanEntity> Loans => Set<LoanEntity>();
    public DbSet<LoanRepaymentEntity> LoanRepayments => Set<LoanRepaymentEntity>();
    public DbSet<EmployeeAppSettingsEntity> EmployeeAppSettings => Set<EmployeeAppSettingsEntity>();
    public DbSet<ThemeTemplateEntity> ThemeTemplates => Set<ThemeTemplateEntity>();
    public DbSet<MediaAssetEntity> MediaAssets => Set<MediaAssetEntity>();
    public DbSet<JobOpeningEntity> JobOpenings => Set<JobOpeningEntity>();
    public DbSet<CandidateEntity> Candidates => Set<CandidateEntity>();
    public DbSet<ApplicationEntity> Applications => Set<ApplicationEntity>();
    public DbSet<ApplicationEventEntity> ApplicationEvents => Set<ApplicationEventEntity>();
    public DbSet<ApplicationDocumentEntity> ApplicationDocuments => Set<ApplicationDocumentEntity>();
    public DbSet<OnboardingEntity> Onboardings => Set<OnboardingEntity>();
    public DbSet<OnboardingTaskEntity> OnboardingTasks => Set<OnboardingTaskEntity>();
    public DbSet<EmployeeEntity> Employees => Set<EmployeeEntity>();
    public DbSet<EmployeeContactEntity> EmployeeContacts => Set<EmployeeContactEntity>();
    public DbSet<EmployeeDependantEntity> EmployeeDependants => Set<EmployeeDependantEntity>();
    public DbSet<EmployeeTimelineEntity> EmployeeTimeline => Set<EmployeeTimelineEntity>();
    public DbSet<EmployeeChangeRequestEntity> EmployeeChangeRequests => Set<EmployeeChangeRequestEntity>();
    public DbSet<InterviewEntity> Interviews => Set<InterviewEntity>();
    public DbSet<InterviewPanelistEntity> InterviewPanelists => Set<InterviewPanelistEntity>();
    public DbSet<InterviewEvaluationEntity> InterviewEvaluations => Set<InterviewEvaluationEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<AttendancePunchEntity> AttendancePunches => Set<AttendancePunchEntity>();
    public DbSet<AttendanceDayEntity> AttendanceDays => Set<AttendanceDayEntity>();
    public DbSet<AttendanceCorrectionEntity> AttendanceCorrections => Set<AttendanceCorrectionEntity>();
    public DbSet<AttendanceExceptionEntity> AttendanceExceptions => Set<AttendanceExceptionEntity>();

    public DbSet<LeavePolicyEntity> LeavePolicies => Set<LeavePolicyEntity>();
    public DbSet<LeaveBalanceEntity> LeaveBalances => Set<LeaveBalanceEntity>();
    public DbSet<LeaveRequestEntity> LeaveRequests => Set<LeaveRequestEntity>();
    public DbSet<LeaveRequestDayEntity> LeaveRequestDays => Set<LeaveRequestDayEntity>();
    public DbSet<LeaveRequestCommentEntity> LeaveRequestComments => Set<LeaveRequestCommentEntity>();
    public DbSet<LeaveHolidayEntity> LeaveHolidays => Set<LeaveHolidayEntity>();
    public DbSet<LeaveCreditEntity> LeaveCredits => Set<LeaveCreditEntity>();
    public DbSet<LeaveAdjustmentEntity> LeaveAdjustments => Set<LeaveAdjustmentEntity>();

    public DbSet<ShiftDefinitionEntity> ShiftDefinitions => Set<ShiftDefinitionEntity>();
    public DbSet<ShiftAssignmentEntity> ShiftAssignments => Set<ShiftAssignmentEntity>();

    public DbSet<MovementRecordEntity> MovementRecords => Set<MovementRecordEntity>();
    public DbSet<GatePassEntity> GatePasses => Set<GatePassEntity>();
    public DbSet<GatePassScanEntity> GatePassScans => Set<GatePassScanEntity>();
    public DbSet<OnDutyRequestEntity> OnDutyRequests => Set<OnDutyRequestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CareerSiteEntity>(entity =>
        {
            entity.ToTable("CareerSites", "platform");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TenantKey).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.DraftJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.PublishedJson).HasColumnType("nvarchar(max)");
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<MailSettingsEntity>(entity =>
        {
            entity.ToTable("MailSettings", "platform");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SenderBaseUrl).HasMaxLength(400).IsRequired();
            entity.Property(item => item.ReceiverBaseUrl).HasMaxLength(400);
            entity.Property(item => item.PublicBaseUrl).HasMaxLength(400).IsRequired();

            // Protected, not hashed: it has to be presented to the mail service, so it must be
            // readable back. Ciphertext is longer than the key that went in.
            entity.Property(item => item.ProtectedApiKey).HasMaxLength(1000);
            entity.Property(item => item.UpdatedBy).HasMaxLength(160);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<TenantEntity>(entity =>
        {
            entity.ToTable("Tenants", "platform");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TenantKey).IsUnique();
            entity.HasIndex(item => item.PrimaryDomain).IsUnique().HasFilter("[PrimaryDomain] IS NOT NULL");
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.PrimaryDomain).HasMaxLength(240);
            entity.Property(item => item.RowVersion).IsRowVersion();
                    entity.Property(item => item.TimeZoneId).HasMaxLength(80).IsRequired();
            // Defaults to enabled so an existing client is not locked out of the portal by the
            // upgrade that introduces the switch.
            entity.Property(item => item.HrPortalEnabled).HasDefaultValue(true);
});

        modelBuilder.Entity<MasterOptionEntity>(entity =>
        {
            entity.ToTable("MasterOptions", "platform");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.Category, item.Code }).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.Category, item.IsActive });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.ParentCode).HasMaxLength(80);
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<PolicyEntity>(entity =>
        {
            entity.ToTable("Policies", "platform");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.Code }).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.IsPublished });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Icon).HasMaxLength(16).IsRequired();
            entity.Property(item => item.Excerpt).HasMaxLength(400).IsRequired();
            entity.Property(item => item.SectionsJson).IsRequired();
            entity.Property(item => item.Version).HasMaxLength(20).IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<PolicyAcknowledgementEntity>(entity =>
        {
            entity.ToTable("PolicyAcknowledgements", "platform");
            entity.HasKey(item => item.Id);

            // One row per person per policy — the repository replaces rather than appends, so the
            // unique index is what keeps a double-click from recording two readings of one policy.
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.PolicyId }).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.EmployeeId).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Version).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<DesignationEntity>(entity =>
        {
            entity.ToTable("Designations", "platform");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.Code }).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.SortOrder });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.GradeCode).HasMaxLength(80).IsRequired();
            entity.Property(item => item.DepartmentCode).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(300);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<GradeSalaryEntity>(entity =>
        {
            entity.ToTable("GradeSalaryStructures", "platform");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.GradeCode }).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.GradeCode).HasMaxLength(80).IsRequired();
            entity.Property(item => item.BasicMonthly).HasPrecision(18, 2);
            entity.Property(item => item.ComponentsJson).IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<ThemeTemplateEntity>(entity =>
        {
            entity.ToTable("ThemeTemplates", "platform");
            entity.HasKey(item => item.TemplateKey);
            entity.Property(item => item.TemplateKey).HasMaxLength(80);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(400).IsRequired();
            entity.Property(item => item.PrimaryColor).HasMaxLength(7).IsRequired();
            entity.Property(item => item.AccentColor).HasMaxLength(7).IsRequired();
            entity.Property(item => item.SurfaceColor).HasMaxLength(7).IsRequired();
            entity.Property(item => item.FontFamily).HasMaxLength(160).IsRequired();
            entity.Property(item => item.HeadingFontFamily).HasMaxLength(160).IsRequired();
            entity.Property(item => item.PreviewGradient).HasMaxLength(240).IsRequired();
        });

        modelBuilder.Entity<MediaAssetEntity>(entity =>
        {
            entity.ToTable("MediaAssets", "platform");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.CreatedUtc });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.StoredFileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.StoragePath).HasMaxLength(600).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<JobOpeningEntity>(entity =>
        {
            entity.ToTable("JobOpenings", "recruitment");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.Slug }).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.IsPublished });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(180).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(180).IsRequired();
            entity.Property(item => item.Department).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Location).HasMaxLength(180).IsRequired();
            entity.Property(item => item.EmploymentType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.WorkMode).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Experience).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Summary).HasMaxLength(1200).IsRequired();
            entity.Property(item => item.ResponsibilitiesJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.RequirementsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired().HasDefaultValue("Draft");
            entity.Property(item => item.SalaryCurrencyCode).HasMaxLength(3);
            entity.Property(item => item.SalaryMinimum).HasPrecision(18, 2);
            entity.Property(item => item.SalaryMaximum).HasPrecision(18, 2);
            entity.Property(item => item.RequiredDocumentCodesJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.ScreeningQuestionsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasIndex(item => new { item.TenantKey, item.Status });
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<CandidateEntity>(entity =>
        {
            entity.ToTable("Candidates", "recruitment");
            entity.HasKey(item => item.Id);
            // Identity key within a tenant. Enforced here, not only in code.
            entity.HasIndex(item => new { item.TenantKey, item.Email }).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(256).IsRequired();
            entity.Property(item => item.FullName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Phone).HasMaxLength(20).IsRequired();
            entity.Property(item => item.CurrentLocation).HasMaxLength(180);
            entity.Property(item => item.TotalExperienceYears).HasPrecision(4, 1);
            entity.Property(item => item.AccountId).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<ApplicationEntity>(entity =>
        {
            entity.ToTable("Applications", "recruitment");
            entity.HasKey(item => item.Id);
            // One live application per candidate per opening.
            entity.HasIndex(item => new { item.TenantKey, item.JobOpeningId, item.CandidateId }).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.Stage });
            entity.HasIndex(item => new { item.TenantKey, item.CandidateId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Stage).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(60).IsRequired();
            entity.Property(item => item.AnswersJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.ExpectedCtc).HasPrecision(18, 2);
            entity.Property(item => item.Rating).HasPrecision(3, 1);
            entity.Property(item => item.RejectionReason).HasMaxLength(1000);
            entity.Property(item => item.ConsentPolicyVersion).HasMaxLength(40);
            entity.Property(item => item.StageChangedBy).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<ApplicationEventEntity>(entity =>
        {
            entity.ToTable("ApplicationEvents", "recruitment");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.ApplicationId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.FromStage).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ToStage).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(1000);
            entity.Property(item => item.ActorId).HasMaxLength(450);
            entity.Property(item => item.ActorKind).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<ApplicationDocumentEntity>(entity =>
        {
            entity.ToTable("ApplicationDocuments", "recruitment");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.ApplicationId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.CategoryCode).HasMaxLength(80).IsRequired();
            entity.Property(item => item.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(item => item.StoragePath).HasMaxLength(400).IsRequired();
            entity.Property(item => item.ScanState).HasMaxLength(20).IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<OnboardingEntity>(entity =>
        {
            entity.ToTable("Onboardings", "recruitment");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.ApplicationId }).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.Stage });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Stage).HasMaxLength(40).IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<OnboardingTaskEntity>(entity =>
        {
            entity.ToTable("OnboardingTasks", "recruitment");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.OnboardingId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(180).IsRequired();
            entity.Property(item => item.Instructions).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.TaskType).HasMaxLength(20).IsRequired();
            entity.Property(item => item.OwnerKind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.FormValuesJson).HasColumnType("nvarchar(max)");
            entity.Property(item => item.DecisionReason).HasMaxLength(1000);
            entity.Property(item => item.DecidedBy).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<EmployeeEntity>(entity =>
        {
            entity.ToTable("Employees", "employee");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeNumber }).IsUnique();
            // Matches the uniqueness candidates already carry. Filtered because the column is
            // optional, and several employees legitimately having no address on file must not
            // collide with one another.
            entity.HasIndex(item => new { item.TenantKey, item.Email })
                  .IsUnique()
                  .HasFilter("[Email] IS NOT NULL");
            entity.HasIndex(item => new { item.TenantKey, item.DepartmentCode });
            entity.HasIndex(item => new { item.TenantKey, item.LocationCode });
            entity.HasIndex(item => new { item.TenantKey, item.EmploymentStatusCode });
            entity.HasIndex(item => new { item.TenantKey, item.ManagerEmployeeId });
            entity.HasIndex(item => item.CandidateId).HasFilter("[CandidateId] IS NOT NULL");
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.EmployeeNumber).HasMaxLength(60).IsRequired();
            entity.Property(item => item.LegalFirstName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.LegalMiddleName).HasMaxLength(120);
            entity.Property(item => item.LegalLastName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.PreferredName).HasMaxLength(120);
            entity.Property(item => item.GenderCode).HasMaxLength(40);
            entity.Property(item => item.MaritalStatusCode).HasMaxLength(40);
            entity.Property(item => item.NationalityCode).HasMaxLength(80);
            entity.Property(item => item.Email).HasMaxLength(320);
            entity.Property(item => item.Phone).HasMaxLength(32);
            entity.Property(item => item.AvatarUrl).HasMaxLength(500);
            entity.Property(item => item.EmploymentTypeCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.EmploymentStatusCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.GradeCode).HasMaxLength(40);
            entity.Property(item => item.JobTitle).HasMaxLength(180);
            entity.Property(item => item.CompanyCode).HasMaxLength(64);
            entity.Property(item => item.DivisionCode).HasMaxLength(64);
            entity.Property(item => item.DepartmentCode).HasMaxLength(64).IsRequired();
            entity.Property(item => item.LocationCode).HasMaxLength(64).IsRequired();
            entity.Property(item => item.CostCentre).HasMaxLength(64);
            entity.Property(item => item.AccountId).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<EmployeeContactEntity>(entity =>
        {
            entity.ToTable("EmployeeContacts", "employee");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Kind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Relationship).HasMaxLength(60);
            entity.Property(item => item.Phone).HasMaxLength(32);
            entity.Property(item => item.Email).HasMaxLength(320);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<EmployeeDependantEntity>(entity =>
        {
            entity.ToTable("EmployeeDependants", "employee");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.RelationCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<EmployeeTimelineEntity>(entity =>
        {
            entity.ToTable("EmployeeTimeline", "employee");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.OccurredUtc });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.EventType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(180).IsRequired();
            entity.Property(item => item.Summary).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.ActorId).HasMaxLength(450);
            entity.Property(item => item.ActorName).HasMaxLength(160);
        });

        modelBuilder.Entity<CompanyBankAccountEntity>(entity =>
        {
            entity.ToTable("CompanyBankAccounts", "platform");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.AccountLabel }).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.BankName).HasMaxLength(80).IsRequired();
            entity.Property(item => item.AccountLabel).HasMaxLength(60).IsRequired();
            entity.Property(item => item.AccountNumberProtected).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.AccountLast4).HasMaxLength(4).IsFixedLength().IsRequired();
            entity.Property(item => item.Ifsc).HasMaxLength(11).IsRequired();
            entity.Property(item => item.BranchName).HasMaxLength(120);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<EmployeeBankAccountEntity>(entity =>
        {
            entity.ToTable("EmployeeBankAccounts", "employee");
            entity.HasKey(item => item.Id);
            // One salary account per employee.
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId }).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.BankName).HasMaxLength(80).IsRequired();
            entity.Property(item => item.AccountHolderName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.AccountNumberProtected).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.AccountLast4).HasMaxLength(4).IsFixedLength().IsRequired();
            entity.Property(item => item.Ifsc).HasMaxLength(11).IsRequired();
            entity.Property(item => item.AccountType).HasMaxLength(20).IsRequired();
            entity.Property(item => item.UpdatedBy).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<EmployeeAppSettingsEntity>(entity =>
        {
            entity.ToTable("EmployeeAppSettings", "platform");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TenantKey).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.UpdatedBy).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<PayrollRunEntity>(entity =>
        {
            entity.ToTable("PayrollRuns", "payroll");
            entity.HasKey(item => item.Id);
            // One run per tenant, month and salary type.
            entity.HasIndex(item => new { item.TenantKey, item.Year, item.Month, item.SalaryType }).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.SalaryType).HasMaxLength(30).IsRequired();
            entity.Property(item => item.GeneratedBy).HasMaxLength(450);
            entity.Property(item => item.ApprovedBy).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<PayrollLineEntity>(entity =>
        {
            entity.ToTable("PayrollLines", "payroll");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.RunId, item.EmployeeId }).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.RunId, item.Status });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.EmployeeNumber).HasMaxLength(60).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.DivisionCode).HasMaxLength(80).IsRequired();
            entity.Property(item => item.DepartmentCode).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(160).IsRequired();
            entity.Property(item => item.GradeCode).HasMaxLength(80).IsRequired();
            entity.Property(item => item.ShiftCode).HasMaxLength(40);
            entity.Property(item => item.BreakdownJson).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.PaymentMode).HasMaxLength(30);
            entity.Property(item => item.DocumentType).HasMaxLength(30);
            entity.Property(item => item.UpdatedBy).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();

            foreach (var money in new[]
                     {
                         nameof(PayrollLineEntity.Basic), nameof(PayrollLineEntity.Hra), nameof(PayrollLineEntity.Conveyance),
                         nameof(PayrollLineEntity.OtherAllowances), nameof(PayrollLineEntity.Gross), nameof(PayrollLineEntity.Pf),
                         nameof(PayrollLineEntity.Esi), nameof(PayrollLineEntity.Tds), nameof(PayrollLineEntity.OtherDeductions),
                         nameof(PayrollLineEntity.Deductions), nameof(PayrollLineEntity.NetPayable)
                     })
            {
                entity.Property<decimal>(money).HasPrecision(18, 2);
            }
        });

        modelBuilder.Entity<BankTransferBatchEntity>(entity =>
        {
            entity.ToTable("BankTransferBatches", "payroll");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.ReferenceNo }).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.RunId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.ReferenceNo).HasMaxLength(40).IsRequired();
            entity.Property(item => item.TransferMode).HasMaxLength(10).IsRequired();
            entity.Property(item => item.TotalAmount).HasPrecision(18, 2);
            entity.Property(item => item.CreatedBy).HasMaxLength(450);
        });

        modelBuilder.Entity<LoanEntity>(entity =>
        {
            entity.ToTable("Loans", "payroll");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.VoucherNo }).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.Status });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.VoucherNo).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Type).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Purpose).HasMaxLength(300);
            entity.Property(item => item.AdvanceTypeCode).HasMaxLength(40);
            entity.Property(item => item.DecisionNote).HasMaxLength(300);
            entity.Property(item => item.RequestedBy).HasMaxLength(450);
            entity.Property(item => item.DecidedBy).HasMaxLength(450);
            entity.Property(item => item.Principal).HasPrecision(18, 2);
            entity.Property(item => item.InterestPercent).HasPrecision(5, 2);
            entity.Property(item => item.TotalRepayable).HasPrecision(18, 2);
            entity.Property(item => item.MonthlyInstallment).HasPrecision(18, 2);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<LoanRepaymentEntity>(entity =>
        {
            entity.ToTable("LoanRepayments", "payroll");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.LoanId });
            // A payroll month recovers a loan at most once; manual repayments are not limited.
            entity.HasIndex(item => new { item.TenantKey, item.LoanId, item.Year, item.Month, item.Source })
                .IsUnique()
                .HasFilter("[Source] = 'Payroll'");
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Note).HasMaxLength(300);
            entity.Property(item => item.RecordedBy).HasMaxLength(450);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<EmployeeChangeRequestEntity>(entity =>
        {
            entity.ToTable("EmployeeChangeRequests", "employee");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId });
            entity.HasIndex(item => new { item.TenantKey, item.Status });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Section).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ChangesJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(1000);
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.DecisionReason).HasMaxLength(1000);
            entity.Property(item => item.RequestedBy).HasMaxLength(450);
            entity.Property(item => item.DecidedBy).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<InterviewEntity>(entity =>
        {
            entity.ToTable("Interviews", "recruitment");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.ApplicationId });
            entity.HasIndex(item => new { item.TenantKey, item.Status });
            // The schedule screen reads a date window, so the range scan is indexed.
            entity.HasIndex(item => new { item.TenantKey, item.StartUtc });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.RoundCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.RoundLabel).HasMaxLength(120).IsRequired();
            entity.Property(item => item.TimeZoneId).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Mode).HasMaxLength(20).IsRequired();
            entity.Property(item => item.LocationOrLink).HasMaxLength(400);
            entity.Property(item => item.Instructions).HasMaxLength(2000);
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.CancellationReasonCode).HasMaxLength(40);
            entity.Property(item => item.CancellationComment).HasMaxLength(1000);
            entity.Property(item => item.ScheduledBy).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<InterviewPanelistEntity>(entity =>
        {
            entity.ToTable("InterviewPanelists", "recruitment");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.InterviewId });
            // A panel member appears once per interview.
            entity.HasIndex(item => new { item.InterviewId, item.PanelMemberId }).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.PanelMemberId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.PanelMemberId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
        });

        modelBuilder.Entity<InterviewEvaluationEntity>(entity =>
        {
            entity.ToTable("InterviewEvaluations", "recruitment");
            entity.HasKey(item => item.Id);
            // One evaluation per panel member per interview, enforced by the database.
            entity.HasIndex(item => new { item.InterviewId, item.PanelMemberId }).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.PanelMemberId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.ScoresJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.Recommendation).HasMaxLength(40);
            entity.Property(item => item.Notes).HasMaxLength(4000);
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable("Notifications", "platform");
            entity.HasKey(item => item.Id);
            // The inbox query is "mine, newest first, not deleted", so that is the index.
            entity.HasIndex(item => new { item.TenantKey, item.RecipientId, item.OccurredUtc });
            entity.HasIndex(item => new { item.TenantKey, item.RecipientId, item.ReadUtc });
            // Lets a duplicate for the same event and recipient be found before inserting.
            entity.HasIndex(item => new { item.TenantKey, item.RecipientId, item.SubjectType, item.SubjectId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.RecipientId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(180).IsRequired();
            entity.Property(item => item.Body).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.Priority).HasMaxLength(20).IsRequired();
            entity.Property(item => item.ActionRoute).HasMaxLength(400);
            entity.Property(item => item.SubjectType).HasMaxLength(40);
            entity.Property(item => item.SubjectId).HasMaxLength(64);
            entity.Ignore(item => item.IsRead);
        });

        modelBuilder.Entity<AttendancePunchEntity>(entity =>
        {
            entity.ToTable("AttendancePunches", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.WorkDate });
            // A retried tap must not become two punches. Enforced by the database, because a
            // check in code loses a race between two concurrent retries.
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.ClientEventId })
                .IsUnique()
                .HasFilter("[ClientEventId] IS NOT NULL");
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Type).HasMaxLength(10).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(20).IsRequired();
            entity.Property(item => item.DeviceId).HasMaxLength(200);
            entity.Property(item => item.LocationLabel).HasMaxLength(200);
            entity.Property(item => item.ClientEventId).HasMaxLength(200);
            entity.Property(item => item.Latitude).HasPrecision(9, 6);
            entity.Property(item => item.Longitude).HasPrecision(9, 6);
        });

        modelBuilder.Entity<AttendanceDayEntity>(entity =>
        {
            entity.ToTable("AttendanceDays", "workforce");
            entity.HasKey(item => item.Id);
            // One computed row per employee per day.
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.WorkDate }).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.WorkDate, item.Status });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.ShiftCode).HasMaxLength(40);
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.ExceptionsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<AttendanceCorrectionEntity>(entity =>
        {
            entity.ToTable("AttendanceCorrections", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.WorkDate });
            entity.HasIndex(item => new { item.TenantKey, item.State });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.ReasonCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Comment).HasMaxLength(2000);
            entity.Property(item => item.OriginalPunchesJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.RequestedPunchesJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.DecisionReasonCode).HasMaxLength(40);
            entity.Property(item => item.DecisionComment).HasMaxLength(2000);
            entity.Property(item => item.DecidedBy).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<AttendanceExceptionEntity>(entity =>
        {
            entity.ToTable("AttendanceExceptions", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.WorkDate });
            entity.HasIndex(item => new { item.TenantKey, item.State });
            // One exception of a kind per employee-day, so recomputing a day does not stack
            // duplicates every time it is recalculated.
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.WorkDate, item.ExceptionType })
                .IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.ExceptionType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.PolicyCode).HasMaxLength(40);
            entity.Property(item => item.EmployeeJustification).HasMaxLength(2000);
            entity.Property(item => item.DecisionReasonCode).HasMaxLength(40);
            entity.Property(item => item.DecisionComment).HasMaxLength(2000);
            entity.Property(item => item.DecidedBy).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        ConfigureLeave(modelBuilder);
    }

    private static void ConfigureLeave(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeavePolicyEntity>(entity =>
        {
            entity.ToTable("LeavePolicies", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.Code }).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Unit).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Version).HasMaxLength(20).IsRequired();
            entity.Property(item => item.AccrualMode).HasMaxLength(20).IsRequired();
            entity.Property(item => item.AnnualEntitlement).HasPrecision(9, 2);
            entity.Property(item => item.CarryForwardCap).HasPrecision(9, 2);
            entity.Property(item => item.DocumentRequiredAboveDays).HasPrecision(9, 2);
            entity.Property(item => item.EligibilityText).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.AccrualRuleText).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.CarryForwardRuleText).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.NoticeRuleText).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.DocumentsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.ToneKey).HasMaxLength(20).IsRequired().HasDefaultValue("blue");
            entity.Property(item => item.Emoji).HasMaxLength(16).IsRequired().HasDefaultValue("🗓️");
        });

        modelBuilder.Entity<LeaveBalanceEntity>(entity =>
        {
            entity.ToTable("LeaveBalances", "workforce");
            entity.HasKey(item => item.Id);
            // One granted balance per employee, policy and year.
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.PolicyId, item.Year })
                .IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Entitled).HasPrecision(9, 2);
            entity.Property(item => item.Accrued).HasPrecision(9, 2);
            entity.Property(item => item.CarriedForward).HasPrecision(9, 2);
            entity.Property(item => item.AdjustmentDays).HasPrecision(9, 2);
        });

        modelBuilder.Entity<LeaveRequestEntity>(entity =>
        {
            entity.ToTable("LeaveRequests", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.FromDate });
            entity.HasIndex(item => new { item.TenantKey, item.State });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.LeaveTypeCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Days).HasPrecision(9, 2);
            entity.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.AttachmentsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.DecidedByUserId).HasMaxLength(450);
            entity.Property(item => item.DecidedByName).HasMaxLength(200);
            entity.Property(item => item.DecisionReasonCode).HasMaxLength(40);
            entity.Property(item => item.DecisionComment).HasMaxLength(2000);
        });

        modelBuilder.Entity<LeaveRequestDayEntity>(entity =>
        {
            entity.ToTable("LeaveRequestDays", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.Date });
            entity.HasIndex(item => new { item.TenantKey, item.RequestId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Portion).HasPrecision(4, 2);
        });

        modelBuilder.Entity<LeaveRequestCommentEntity>(entity =>
        {
            entity.ToTable("LeaveRequestComments", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.RequestId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Kind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.AuthorUserId).HasMaxLength(450);
            entity.Property(item => item.AuthorName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Comment).HasMaxLength(2000);
        });

        modelBuilder.Entity<LeaveHolidayEntity>(entity =>
        {
            entity.ToTable("LeaveHolidays", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.Date, item.LocationCode }).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.LocationCode).HasMaxLength(40);
        });

        modelBuilder.Entity<LeaveCreditEntity>(entity =>
        {
            entity.ToTable("LeaveCredits", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.ExpiresOn });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.SourceCode).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Days).HasPrecision(9, 2);
            entity.Property(item => item.EncashmentAmount).HasPrecision(18, 2);
            entity.Property(item => item.EncashmentCurrency).HasMaxLength(3);
        });

        modelBuilder.Entity<MovementRecordEntity>(entity =>
        {
            entity.ToTable("MovementRecords", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.WorkDate });
            entity.HasIndex(item => new { item.TenantKey, item.State });
            // At most one open movement per person: going out twice without returning would
            // leave two records both claiming to be current.
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId })
                .IsUnique()
                .HasFilter("[State] = 'Open' AND [ActualReturnUtc] IS NULL")
                .HasDatabaseName("IX_MovementRecords_OneOpenPerEmployee");
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Kind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Detail).HasMaxLength(500).IsRequired();
            entity.Property(item => item.DestinationLabel).HasMaxLength(200);
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.RecordedBy).HasMaxLength(450);
        });

        modelBuilder.Entity<GatePassEntity>(entity =>
        {
            entity.ToTable("GatePasses", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.FromDate });
            entity.HasIndex(item => new { item.TenantKey, item.State });
            // A scanned token must resolve to exactly one pass.
            entity.HasIndex(item => new { item.TenantKey, item.VerificationToken })
                .IsUnique()
                .HasFilter("[VerificationToken] IS NOT NULL");
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.PassType).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Purpose).HasMaxLength(500).IsRequired();
            entity.Property(item => item.DestinationLabel).HasMaxLength(200);
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.VerificationToken).HasMaxLength(64);
            entity.Property(item => item.DecidedByUserId).HasMaxLength(450);
            entity.Property(item => item.DecidedByName).HasMaxLength(200);
            entity.Property(item => item.DecisionComment).HasMaxLength(2000);
        });

        modelBuilder.Entity<GatePassScanEntity>(entity =>
        {
            entity.ToTable("GatePassScans", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.GatePassId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Direction).HasMaxLength(10).IsRequired();
            entity.Property(item => item.ScannedBy).HasMaxLength(450);
            entity.Property(item => item.GateLabel).HasMaxLength(120);
        });

        modelBuilder.Entity<OnDutyRequestEntity>(entity =>
        {
            entity.ToTable("OnDutyRequests", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.FromDate });
            entity.HasIndex(item => new { item.TenantKey, item.State });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Purpose).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Location).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.DecidedByUserId).HasMaxLength(450);
            entity.Property(item => item.DecidedByName).HasMaxLength(200);
            entity.Property(item => item.DecisionComment).HasMaxLength(2000);
        });

        modelBuilder.Entity<ShiftDefinitionEntity>(entity =>
        {
            entity.ToTable("ShiftDefinitions", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.Code }).IsUnique();
            // At most one default per tenant, enforced by the database rather than by the
            // service alone: two defaults would make expected time ambiguous.
            entity.HasIndex(item => item.TenantKey)
                .IsUnique()
                .HasFilter("[IsDefault] = 1")
                .HasDatabaseName("IX_ShiftDefinitions_OneDefaultPerTenant");
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.WorkingDays).HasMaxLength(7).IsRequired();
        });

        modelBuilder.Entity<ShiftAssignmentEntity>(entity =>
        {
            entity.ToTable("ShiftAssignments", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.EffectiveFrom });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.AssignedBy).HasMaxLength(450);
            entity.Property(item => item.Note).HasMaxLength(500);
        });

        modelBuilder.Entity<LeaveAdjustmentEntity>(entity =>
        {
            entity.ToTable("LeaveAdjustments", "workforce");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.State });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Kind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.CreditIdsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.Days).HasPrecision(9, 2);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Currency).HasMaxLength(3);
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Comment).HasMaxLength(2000);
        });
    }
}
