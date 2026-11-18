using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vispl.HRManagement.DL.Admin;
using Vispl.HRManagement.DL.Employee;
using Vispl.HRManagement.DL.Recruitment;
using Vispl.HRManagement.DL.Workforce;

namespace Vispl.HRManagement.DL.Shared;

/// <summary>
/// Database-level integrity, adopted from the HrManagemtNewDb29062026 scope script without
/// changing how the app stores data: foreign keys so no row can point at a parent that does not
/// exist, and CHECK constraints for formats and ranges the services already validate.
///
/// Every foreign key is RESTRICT (NO ACTION): the app never deletes a parent that still has
/// children, and a mistaken delete should fail loudly rather than silently cascade.
/// </summary>
public sealed partial class HrManagementDbContext
{
    private const string Ifsc = "[A-Z][A-Z][A-Z][A-Z]0[A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9]";
    private const string Last4 = "[0-9][0-9][0-9][0-9]";

    private static void ConfigureEmployeeMaster(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmployeeStatutoryEntity>(entity =>
        {
            entity.ToTable("EmployeeStatutory", "employee", table =>
            {
                table.HasCheckConstraint("CK_EmployeeStatutory_Pan",
                    "[PanNumber] IS NULL OR [PanNumber] LIKE '[A-Z][A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9][A-Z]'");
                table.HasCheckConstraint("CK_EmployeeStatutory_AadhaarLast4", $"[AadhaarLast4] IS NULL OR [AadhaarLast4] LIKE '{Last4}'");
                table.HasCheckConstraint("CK_EmployeeStatutory_Uan", "[UanNumber] IS NULL OR ([UanNumber] NOT LIKE '%[^0-9]%' AND LEN([UanNumber]) = 12)");
                table.HasCheckConstraint("CK_EmployeeStatutory_Esi", "[EsiNumber] IS NULL OR ([EsiNumber] NOT LIKE '%[^0-9]%' AND LEN([EsiNumber]) = 17)");
                table.HasCheckConstraint("CK_EmployeeStatutory_PfScheme", "[PfScheme] IS NULL OR [PfScheme] IN ('Govt', 'Company')");
            });
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId }).IsUnique();
            entity.HasIndex(item => new { item.TenantKey, item.PanNumber }).IsUnique().HasFilter("[PanNumber] IS NOT NULL");
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.PanNumber).HasMaxLength(10).IsFixedLength();
            entity.Property(item => item.AadhaarProtected).HasMaxLength(1000);
            entity.Property(item => item.AadhaarLast4).HasMaxLength(4).IsFixedLength();
            entity.Property(item => item.VoterId).HasMaxLength(20);
            entity.Property(item => item.PfScheme).HasMaxLength(10);
            entity.Property(item => item.PfNumber).HasMaxLength(30);
            entity.Property(item => item.UanNumber).HasMaxLength(12);
            entity.Property(item => item.EpfAccountCode).HasMaxLength(80);
            entity.Property(item => item.EsiNumber).HasMaxLength(17);
            entity.Property(item => item.EsiAccountCode).HasMaxLength(80);
            entity.Property(item => item.UpdatedBy).HasMaxLength(450);
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.HasOne<EmployeeEntity>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmployeeEducationEntity>(entity =>
        {
            entity.ToTable("EmployeeEducation", "employee", table =>
            {
                table.HasCheckConstraint("CK_EmployeeEducation_Year", "[PassingYear] BETWEEN 1950 AND 2100");
                table.HasCheckConstraint("CK_EmployeeEducation_Percentage", "[Percentage] IS NULL OR [Percentage] BETWEEN 0 AND 100");
                table.HasCheckConstraint("CK_EmployeeEducation_Cgpa", "[Cgpa] IS NULL OR [Cgpa] BETWEEN 0 AND 10");
            });
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.EducationLevelCode).HasMaxLength(40);
            entity.Property(item => item.QualificationCode).HasMaxLength(40);
            entity.Property(item => item.Specialisation).HasMaxLength(120);
            entity.Property(item => item.Institute).HasMaxLength(200).IsRequired();
            entity.Property(item => item.BoardOrUniversity).HasMaxLength(200);
            entity.Property(item => item.Percentage).HasPrecision(5, 2);
            entity.Property(item => item.Cgpa).HasPrecision(4, 2);
            entity.Property(item => item.GradeLetter).HasMaxLength(5);
            entity.Property(item => item.CertificationName).HasMaxLength(200);
            entity.HasOne<EmployeeEntity>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmployeePreviousEmploymentEntity>(entity =>
        {
            entity.ToTable("EmployeePreviousEmployment", "employee", table =>
            {
                table.HasCheckConstraint("CK_EmployeePreviousEmployment_Dates", "[LeftOn] >= [JoinedOn]");
                table.HasCheckConstraint("CK_EmployeePreviousEmployment_Type",
                    "[EmploymentType] IS NULL OR [EmploymentType] IN ('Permanent', 'Contract', 'Intern', 'Consultant')");
                table.HasCheckConstraint("CK_EmployeePreviousEmployment_Ctc", "[LastAnnualCtc] IS NULL OR [LastAnnualCtc] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(120);
            entity.Property(item => item.Department).HasMaxLength(120);
            entity.Property(item => item.PreviousEmployeeId).HasMaxLength(30);
            entity.Property(item => item.EmploymentType).HasMaxLength(20);
            entity.Property(item => item.LastAnnualCtc).HasPrecision(18, 2);
            entity.Property(item => item.ReportingManagerName).HasMaxLength(120);
            entity.Property(item => item.LeavingReasonCode).HasMaxLength(40);
            entity.Property(item => item.Remarks).HasMaxLength(500);
            entity.HasOne<EmployeeEntity>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmployeeAddressEntity>(entity =>
        {
            entity.ToTable("EmployeeAddresses", "employee", table =>
            {
                table.HasCheckConstraint("CK_EmployeeAddresses_Type", "[AddressType] IN ('Permanent', 'Current')");
                table.HasCheckConstraint("CK_EmployeeAddresses_Pincode",
                    "[Pincode] IS NULL OR ([Pincode] NOT LIKE '%[^0-9]%' AND LEN([Pincode]) BETWEEN 4 AND 10)");
            });
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId, item.AddressType }).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.AddressType).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Line1).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Line2).HasMaxLength(200);
            entity.Property(item => item.CityCode).HasMaxLength(64);
            entity.Property(item => item.StateCode).HasMaxLength(64);
            entity.Property(item => item.CountryCode).HasMaxLength(64);
            entity.Property(item => item.Pincode).HasMaxLength(10);
            entity.HasOne<EmployeeEntity>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmployeeSalaryConfigEntity>(entity =>
        {
            entity.ToTable("EmployeeSalaryConfigs", "employee", table =>
            {
                table.HasCheckConstraint("CK_EmployeeSalaryConfigs_Basic", "[BasicSalary] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId }).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.BasicSalary).HasPrecision(18, 2);
            entity.Property(item => item.EpfEmployerShareRule).HasMaxLength(80);
            entity.Property(item => item.ComponentsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasOne<EmployeeEntity>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmployeeDocumentEntity>(entity =>
        {
            entity.ToTable("EmployeeDocuments", "employee");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId });
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.DocumentType).HasMaxLength(120).IsRequired();
            entity.Property(item => item.DocumentNumber).HasMaxLength(100);
            entity.Property(item => item.OriginalFileName).HasMaxLength(260);
            entity.Property(item => item.PublicUrl).HasMaxLength(500);
            entity.HasOne<EmployeeEntity>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmployeeLeavingEntity>(entity =>
        {
            entity.ToTable("EmployeeLeavingDetails", "employee");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantKey, item.EmployeeId }).IsUnique();
            entity.Property(item => item.TenantKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.LeavingReasonCode).HasMaxLength(80);
            entity.Property(item => item.LeavingRemarks).HasMaxLength(500);
            entity.Property(item => item.DismissRemarks).HasMaxLength(500);
            entity.HasOne<EmployeeEntity>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureIntegrity(ModelBuilder modelBuilder)
    {
        // ---- Foreign keys: child → employee --------------------------------------------------
        EmployeeKey<EmployeeContactEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<EmployeeDependantEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<EmployeeTimelineEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<EmployeeBankAccountEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<EmployeeChangeRequestEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<PayrollLineEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<LoanEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<LeaveBalanceEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<LeaveRequestEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<AttendanceDayEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<AttendancePunchEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<AttendanceCorrectionEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<AttendanceExceptionEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<ShiftAssignmentEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<MovementRecordEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<GatePassEntity>(modelBuilder, item => item.EmployeeId);
        EmployeeKey<OnDutyRequestEntity>(modelBuilder, item => item.EmployeeId);

        modelBuilder.Entity<EmployeeEntity>()
            .HasOne<EmployeeEntity>().WithMany().HasForeignKey(item => item.ManagerEmployeeId).OnDelete(DeleteBehavior.Restrict);

        // ---- Foreign keys: other parent/child pairs --------------------------------------------
        Key<PayrollLineEntity, PayrollRunEntity>(modelBuilder, item => item.RunId);
        Key<LoanRepaymentEntity, LoanEntity>(modelBuilder, item => item.LoanId);
        Key<LeaveBalanceEntity, LeavePolicyEntity>(modelBuilder, item => item.PolicyId);
        Key<LeaveRequestEntity, LeavePolicyEntity>(modelBuilder, item => item.PolicyId);
        Key<LeaveRequestDayEntity, LeaveRequestEntity>(modelBuilder, item => item.RequestId);
        Key<ShiftAssignmentEntity, ShiftDefinitionEntity>(modelBuilder, item => item.ShiftDefinitionId);
        Key<GatePassScanEntity, GatePassEntity>(modelBuilder, item => item.GatePassId);
        Key<ApplicationEntity, CandidateEntity>(modelBuilder, item => item.CandidateId);
        Key<ApplicationEntity, JobOpeningEntity>(modelBuilder, item => item.JobOpeningId);
        Key<ApplicationEventEntity, ApplicationEntity>(modelBuilder, item => item.ApplicationId);
        Key<OnboardingEntity, ApplicationEntity>(modelBuilder, item => item.ApplicationId);
        Key<OnboardingTaskEntity, OnboardingEntity>(modelBuilder, item => item.OnboardingId);
        Key<InterviewEntity, ApplicationEntity>(modelBuilder, item => item.ApplicationId);
        Key<InterviewPanelistEntity, InterviewEntity>(modelBuilder, item => item.InterviewId);
        Key<InterviewEvaluationEntity, InterviewEntity>(modelBuilder, item => item.InterviewId);

        // ---- CHECK constraints -----------------------------------------------------------------
        Check<EmployeeEntity>(modelBuilder, "Employees", "employee",
            ("CK_Employees_ProbationAfterJoining", "[ProbationEndsOn] IS NULL OR [ProbationEndsOn] >= [JoinedOn]"),
            ("CK_Employees_LeavingAfterJoining", "[LeavingOn] IS NULL OR [LeavingOn] >= [JoinedOn]"),
            ("CK_Employees_NotOwnManager", "[ManagerEmployeeId] IS NULL OR [ManagerEmployeeId] <> [Id]"));
        Check<EmployeeBankAccountEntity>(modelBuilder, "EmployeeBankAccounts", "employee",
            ("CK_EmployeeBankAccounts_Ifsc", $"[Ifsc] LIKE '{Ifsc}'"),
            ("CK_EmployeeBankAccounts_Last4", $"[AccountLast4] LIKE '{Last4}'"));
        Check<CompanyBankAccountEntity>(modelBuilder, "CompanyBankAccounts", "platform",
            ("CK_CompanyBankAccounts_Ifsc", $"[Ifsc] LIKE '{Ifsc}'"),
            ("CK_CompanyBankAccounts_Last4", $"[AccountLast4] LIKE '{Last4}'"));
        Check<GradeSalaryEntity>(modelBuilder, "GradeSalaryStructures", "platform",
            ("CK_GradeSalaryStructures_Basic", "[BasicMonthly] >= 0"));
        Check<LeaveRequestEntity>(modelBuilder, "LeaveRequests", "workforce",
            ("CK_LeaveRequests_Dates", "[ToDate] >= [FromDate]"),
            ("CK_LeaveRequests_Days", "[Days] > 0"));
        Check<ShiftAssignmentEntity>(modelBuilder, "ShiftAssignments", "workforce",
            ("CK_ShiftAssignments_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]"));
        Check<PayrollRunEntity>(modelBuilder, "PayrollRuns", "payroll",
            ("CK_PayrollRuns_Month", "[Month] BETWEEN 1 AND 12"));
        Check<PayrollLineEntity>(modelBuilder, "PayrollLines", "payroll",
            ("CK_PayrollLines_Amounts", "[Basic] >= 0 AND [Gross] >= 0 AND [Deductions] >= 0"),
            ("CK_PayrollLines_PayableDays", "[PayableDays] BETWEEN 0 AND [DaysInMonth]"));
        Check<LoanEntity>(modelBuilder, "Loans", "payroll",
            ("CK_Loans_Principal", "[Principal] > 0"),
            ("CK_Loans_Installments", "[Installments] > 0"),
            ("CK_Loans_StartMonth", "[StartMonth] BETWEEN 1 AND 12"),
            ("CK_Loans_Interest", "[InterestPercent] BETWEEN 0 AND 100"));
        Check<LoanRepaymentEntity>(modelBuilder, "LoanRepayments", "payroll",
            ("CK_LoanRepayments_Amount", "[Amount] > 0"),
            ("CK_LoanRepayments_Month", "[Month] BETWEEN 1 AND 12"));
        Check<JobOpeningEntity>(modelBuilder, "JobOpenings", "recruitment",
            ("CK_JobOpenings_SalaryBand", "[SalaryMinimum] IS NULL OR [SalaryMaximum] IS NULL OR [SalaryMaximum] >= [SalaryMinimum]"));
        Check<ApplicationEntity>(modelBuilder, "Applications", "recruitment",
            ("CK_Applications_ExpectedCtc", "[ExpectedCtc] IS NULL OR [ExpectedCtc] >= 0"),
            ("CK_Applications_Rating", "[Rating] IS NULL OR [Rating] BETWEEN 0 AND 5"));
        Check<CandidateEntity>(modelBuilder, "Candidates", "recruitment",
            ("CK_Candidates_Experience", "[TotalExperienceYears] BETWEEN 0 AND 60"));
    }

    private static void EmployeeKey<TChild>(ModelBuilder modelBuilder, System.Linq.Expressions.Expression<Func<TChild, object?>> key)
        where TChild : class =>
        Key<TChild, EmployeeEntity>(modelBuilder, key);

    private static void Key<TChild, TParent>(ModelBuilder modelBuilder, System.Linq.Expressions.Expression<Func<TChild, object?>> key)
        where TChild : class where TParent : class =>
        modelBuilder.Entity<TChild>().HasOne<TParent>().WithMany().HasForeignKey(key).OnDelete(DeleteBehavior.Restrict);

    // Restates the table name: ToTable with a builder must name the table it configures.
    private static void Check<TEntity>(ModelBuilder modelBuilder, string table, string schema, params (string Name, string Sql)[] checks)
        where TEntity : class =>
        modelBuilder.Entity<TEntity>().ToTable(table, schema, builder =>
        {
            foreach (var (name, sql) in checks)
            {
                builder.HasCheckConstraint(name, sql);
            }
        });
}
