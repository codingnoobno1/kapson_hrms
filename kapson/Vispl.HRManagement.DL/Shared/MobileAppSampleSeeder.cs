using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vispl.HRManagement.DL.Admin;
using Vispl.HRManagement.DL.Employee;
using Vispl.HRManagement.DL.Recruitment;
using Vispl.HRManagement.DL.Workforce;

namespace Vispl.HRManagement.DL.Shared;

/// <summary>
/// Idempotently seeds mobile-app and employee self-service tables for the "kapson" tenant so
/// every mobile screen (Home KPIs, Attendance Regularisation, On-Duty, Payslips, Salary &amp; Bank
/// Details, My Onboarding, Profile Change Requests, Employee Timeline, Interviews, and Policy
/// Acknowledgements) has realistic, internally consistent data.
/// </summary>
internal static class MobileAppSampleSeeder
{
    private const string TenantKey = "kapson";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task SeedIfEmptyAsync(
        HrManagementDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var employees = await dbContext.Employees
            .Where(e => e.TenantKey == TenantKey)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);

        if (employees.Count == 0)
        {
            return;
        }

        var empByNum = employees.ToDictionary(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase);

        // 1. Employee App Settings
        if (!await dbContext.EmployeeAppSettings.AnyAsync(x => x.TenantKey == TenantKey, cancellationToken))
        {
            logger.LogInformation("Seeding EmployeeAppSettings for tenant '{TenantKey}'.", TenantKey);
            dbContext.EmployeeAppSettings.Add(new EmployeeAppSettingsEntity
            {
                TenantKey = TenantKey,
                AllowAppPunch = true,
                RequireLocationForPunch = false,
                AllowGatePassRequests = true,
                AllowAttendanceCorrections = true,
                ShowSalaryDetails = true,
                ShowBankDetails = true,
                ShowOnboarding = true,
                AllowProfileChangeRequests = true,
                AttendanceTargetPercent = 90,
                UpdatedBy = "Simran Kaur",
                UpdatedUtc = now
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // 2. Company & Employee Bank Accounts
        if (!await dbContext.CompanyBankAccounts.AnyAsync(x => x.TenantKey == TenantKey, cancellationToken))
        {
            dbContext.CompanyBankAccounts.AddRange(
                new CompanyBankAccountEntity
                {
                    TenantKey = TenantKey,
                    BankName = "HDFC Bank",
                    AccountLabel = "Kapson Corporate Payroll — Chandigarh",
                    AccountNumberProtected = "PROTECTED-HDFC-502000881920",
                    AccountLast4 = "1920",
                    Ifsc = "HDFC0001204",
                    BranchName = "Sector 17, Chandigarh",
                    IsDefault = true,
                    IsActive = true,
                    UpdatedUtc = now
                },
                new CompanyBankAccountEntity
                {
                    TenantKey = TenantKey,
                    BankName = "ICICI Bank",
                    AccountLabel = "Kapson Retail Operations — Mohali",
                    AccountNumberProtected = "PROTECTED-ICICI-008105004412",
                    AccountLast4 = "4412",
                    Ifsc = "ICIC0000816",
                    BranchName = "Phase 7, Mohali",
                    IsDefault = false,
                    IsActive = true,
                    UpdatedUtc = now
                });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.EmployeeBankAccounts.AnyAsync(x => x.TenantKey == TenantKey, cancellationToken))
        {
            (string Bank, string Ifsc)[] banks =
            [
                ("HDFC Bank", "HDFC0001204"),
                ("ICICI Bank", "ICIC0000816"),
                ("Axis Bank", "UTIB0000312"),
                ("State Bank of India", "SBIN0004519")
            ];

            var empBanks = new List<EmployeeBankAccountEntity>();
            var idx = 0;
            foreach (var emp in employees.Where(e => e.JoinedOn <= today))
            {
                var bank = banks[idx % banks.Length];
                var last4 = (4101 + idx).ToString();
                empBanks.Add(new EmployeeBankAccountEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = emp.Id,
                    BankName = bank.Bank,
                    AccountHolderName = $"{emp.LegalFirstName} {emp.LegalLastName}".Trim(),
                    AccountNumberProtected = $"PROTECTED-{emp.EmployeeNumber}-{last4}",
                    AccountLast4 = last4,
                    Ifsc = bank.Ifsc,
                    AccountType = "Savings",
                    UpdatedBy = "Simran Kaur",
                    UpdatedUtc = now.AddDays(-30)
                });
                idx++;
            }

            dbContext.EmployeeBankAccounts.AddRange(empBanks);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // 3. Approved Monthly Payroll Runs, Payslips & Loan Repayments (last 3 completed months)
        if (!await dbContext.PayrollRuns.AnyAsync(x => x.TenantKey == TenantKey, cancellationToken))
        {
            logger.LogInformation("Seeding approved monthly payroll runs and payslips for tenant '{TenantKey}'.", TenantKey);

            var companyBank = await dbContext.CompanyBankAccounts
                .FirstAsync(x => x.TenantKey == TenantKey && x.IsDefault, cancellationToken);

            var activeLoan = await dbContext.Loans
                .FirstOrDefaultAsync(x => x.TenantKey == TenantKey && x.Status == "Disbursed", cancellationToken);

            for (var monthsBack = 3; monthsBack >= 1; monthsBack--)
            {
                var periodDate = new DateOnly(today.Year, today.Month, 1).AddMonths(-monthsBack);
                var daysInMonth = DateTime.DaysInMonth(periodDate.Year, periodDate.Month);
                var periodEnd = new DateOnly(periodDate.Year, periodDate.Month, daysInMonth);
                var generatedUtc = new DateTimeOffset(periodEnd.AddDays(1).ToDateTime(new TimeOnly(11, 0)), TimeSpan.Zero);
                var approvedUtc = new DateTimeOffset(periodEnd.AddDays(2).ToDateTime(new TimeOnly(15, 30)), TimeSpan.Zero);
                var paidOn = periodEnd.AddDays(3);

                var run = new PayrollRunEntity
                {
                    TenantKey = TenantKey,
                    Year = periodDate.Year,
                    Month = periodDate.Month,
                    SalaryType = "Monthly",
                    GeneratedUtc = generatedUtc,
                    GeneratedBy = "Simran Kaur",
                    ApprovedUtc = approvedUtc,
                    ApprovedBy = "Simran Kaur"
                };
                dbContext.PayrollRuns.Add(run);
                await dbContext.SaveChangesAsync(cancellationToken);

                var eligibleEmployees = employees
                    .Where(e => e.JoinedOn <= periodEnd)
                    .ToList();

                var lines = new List<PayrollLineEntity>();
                decimal batchTotal = 0m;

                foreach (var emp in eligibleEmployees)
                {
                    var (basic, hra, conveyance, otherAllowances, pf, esi, tds) = GradePayFor(emp.GradeCode);
                    var otherDeductions = activeLoan is not null && activeLoan.EmployeeId == emp.Id
                        ? activeLoan.MonthlyInstallment
                        : 0m;
                    var gross = basic + hra + conveyance + otherAllowances;
                    var deductions = pf + esi + tds + otherDeductions;
                    var netPayable = gross - deductions;
                    batchTotal += netPayable;

                    lines.Add(new PayrollLineEntity
                    {
                        TenantKey = TenantKey,
                        RunId = run.Id,
                        EmployeeId = emp.Id,
                        EmployeeNumber = emp.EmployeeNumber,
                        DisplayName = $"{emp.LegalFirstName} {emp.LegalLastName}".Trim(),
                        DivisionCode = emp.DivisionCode ?? "CORP",
                        DepartmentCode = emp.DepartmentCode,
                        Designation = emp.JobTitle ?? "Associate",
                        GradeCode = emp.GradeCode ?? "G3",
                        ShiftCode = "GENERAL",
                        WorkedDays = 22,
                        ExpectedDays = 22,
                        PayableDays = daysInMonth,
                        DaysInMonth = daysInMonth,
                        Basic = basic,
                        Hra = hra,
                        Conveyance = conveyance,
                        OtherAllowances = otherAllowances,
                        Gross = gross,
                        Pf = pf,
                        Esi = esi,
                        Tds = tds,
                        OtherDeductions = otherDeductions,
                        Deductions = deductions,
                        NetPayable = netPayable,
                        BreakdownJson = "{}",
                        Status = "Paid",
                        PaidOn = paidOn,
                        PaymentMode = "Bank transfer",
                        DocumentType = "Payslip",
                        UpdatedBy = "Simran Kaur",
                        UpdatedUtc = approvedUtc
                    });
                }

                dbContext.PayrollLines.AddRange(lines);
                await dbContext.SaveChangesAsync(cancellationToken);

                var batch = new BankTransferBatchEntity
                {
                    TenantKey = TenantKey,
                    RunId = run.Id,
                    ReferenceNo = $"KPS-SAL-{periodDate:yyyyMM}-01",
                    CompanyBankAccountId = companyBank.Id,
                    TransferDate = paidOn,
                    TransferMode = "NEFT",
                    TotalAmount = batchTotal,
                    EmployeeCount = lines.Count,
                    CreatedBy = "Simran Kaur",
                    CreatedUtc = approvedUtc.AddHours(2)
                };
                dbContext.BankTransferBatches.Add(batch);
                await dbContext.SaveChangesAsync(cancellationToken);

                foreach (var line in lines)
                {
                    line.TransferBatchId = batch.Id;
                }
                await dbContext.SaveChangesAsync(cancellationToken);

                if (activeLoan is not null && monthsBack <= 2)
                {
                    dbContext.LoanRepayments.Add(new LoanRepaymentEntity
                    {
                        TenantKey = TenantKey,
                        LoanId = activeLoan.Id,
                        Year = periodDate.Year,
                        Month = periodDate.Month,
                        Amount = activeLoan.MonthlyInstallment,
                        Source = "Payroll",
                        PayrollRunId = run.Id,
                        RecordedOn = paidOn,
                        Note = batch.ReferenceNo,
                        RecordedBy = "Simran Kaur",
                        CreatedUtc = approvedUtc
                    });
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }

        // 4. Attendance Regularisation / Corrections
        if (!await dbContext.AttendanceCorrections.AnyAsync(x => x.TenantKey == TenantKey, cancellationToken))
        {
            string MakeOrigJson(DateOnly d, int h, int m) =>
                JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        id = "1",
                        type = "In",
                        occurredUtc = new DateTimeOffset(d.ToDateTime(new TimeOnly(h, m)), TimeSpan.Zero),
                        source = "Device",
                        locationLabel = "Chandigarh HQ"
                    }
                }, JsonOptions);

            string MakeReqJson(DateOnly d, int inH, int inM, int outH, int outM) =>
                JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        type = "In",
                        occurredUtc = new DateTimeOffset(d.ToDateTime(new TimeOnly(inH, inM)), TimeSpan.Zero)
                    },
                    new
                    {
                        type = "Out",
                        occurredUtc = new DateTimeOffset(d.ToDateTime(new TimeOnly(outH, outM)), TimeSpan.Zero)
                    }
                }, JsonOptions);

            var d1 = today.AddDays(-2);
            var d2 = today.AddDays(-3);
            var d3 = today.AddDays(-5);
            var d4 = today.AddDays(-6);
            var d5 = today.AddDays(-8);

            dbContext.AttendanceCorrections.AddRange(
                new AttendanceCorrectionEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1001"].Id,
                    WorkDate = d2,
                    ReasonCode = "FORGOT_CHECKOUT",
                    Comment = "Left directly from leadership review meeting; missed biometric check-out.",
                    OriginalPunchesJson = MakeOrigJson(d2, 9, 1),
                    RequestedPunchesJson = MakeReqJson(d2, 9, 1, 18, 15),
                    State = "Pending",
                    SubmittedUtc = now.AddHours(-18),
                    UpdatedUtc = now.AddHours(-18)
                },
                new AttendanceCorrectionEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1004"].Id,
                    WorkDate = d1,
                    ReasonCode = "FORGOT_CHECKOUT",
                    Comment = "Missed evening swipe at Sector 17 terminal while closing audit files.",
                    OriginalPunchesJson = MakeOrigJson(d1, 9, 1),
                    RequestedPunchesJson = MakeReqJson(d1, 9, 1, 18, 10),
                    State = "Pending",
                    SubmittedUtc = now.AddHours(-10),
                    UpdatedUtc = now.AddHours(-10)
                },
                new AttendanceCorrectionEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1009"].Id,
                    WorkDate = d3,
                    ReasonCode = "BIOMETRIC_READER_ISSUE",
                    Comment = "Evening punch did not sync from Mohali Digital Bay terminal.",
                    OriginalPunchesJson = MakeOrigJson(d3, 8, 55),
                    RequestedPunchesJson = MakeReqJson(d3, 8, 55, 18, 5),
                    State = "Pending",
                    SubmittedUtc = now.AddDays(-1),
                    UpdatedUtc = now.AddDays(-1)
                },
                new AttendanceCorrectionEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1008"].Id,
                    WorkDate = d4,
                    ReasonCode = "CLIENT_VISIT",
                    Comment = "Morning vendor integration call taken from transit before arriving at office.",
                    OriginalPunchesJson = MakeOrigJson(d4, 10, 12),
                    RequestedPunchesJson = MakeReqJson(d4, 9, 0, 18, 15),
                    State = "Approved",
                    DecisionReasonCode = "VERIFIED",
                    DecisionComment = "Confirmed with E-commerce Category Lead.",
                    DecidedBy = "Simran Kaur",
                    DecidedUtc = now.AddDays(-3),
                    SubmittedUtc = now.AddDays(-4),
                    UpdatedUtc = now.AddDays(-3)
                },
                new AttendanceCorrectionEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1018"].Id,
                    WorkDate = d5,
                    ReasonCode = "TRAFFIC_DELAY",
                    Comment = "Requested waiver for late arrival due to highway congestion.",
                    OriginalPunchesJson = MakeOrigJson(d5, 10, 42),
                    RequestedPunchesJson = MakeReqJson(d5, 10, 0, 19, 5),
                    State = "Rejected",
                    DecisionReasonCode = "POLICY_LIMIT",
                    DecisionComment = "Grace period already exhausted for the month.",
                    DecidedBy = "Simran Kaur",
                    DecidedUtc = now.AddDays(-5),
                    SubmittedUtc = now.AddDays(-6),
                    UpdatedUtc = now.AddDays(-5)
                });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // 5. On-Duty Requests
        if (!await dbContext.OnDutyRequests.AnyAsync(x => x.TenantKey == TenantKey, cancellationToken))
        {
            dbContext.OnDutyRequests.AddRange(
                new OnDutyRequestEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1001"].Id,
                    Purpose = "Regional Retail HR Audit & Store Walkthrough",
                    FromDate = today.AddDays(-4),
                    ToDate = today.AddDays(-3),
                    Location = "Ludhiana & Jalandhar Stores",
                    Reason = "Quarterly HR compliance check and store staff townhall.",
                    State = "Approved",
                    DecidedByName = "Simran Kaur",
                    DecidedUtc = now.AddDays(-5),
                    DecisionComment = "Approved per Q4 HR audit schedule.",
                    RequestedUtc = now.AddDays(-6),
                    UpdatedUtc = now.AddDays(-5)
                },
                new OnDutyRequestEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1002"].Id,
                    Purpose = "Campus Recruitment Drive — Retail & Tech",
                    FromDate = today.AddDays(3),
                    ToDate = today.AddDays(4),
                    Location = "Chandigarh University Campus",
                    Reason = "Representing Kapson HR panel for graduate trainee hiring.",
                    State = "Pending",
                    RequestedUtc = now.AddHours(-14),
                    UpdatedUtc = now.AddHours(-14)
                },
                new OnDutyRequestEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1008"].Id,
                    Purpose = "Payment Gateway Partner Workshop",
                    FromDate = today.AddDays(5),
                    ToDate = today.AddDays(5),
                    Location = "Gurugram CyberHub",
                    Reason = "Technical workshop on checkout tokenization and instant refunds.",
                    State = "Pending",
                    RequestedUtc = now.AddHours(-6),
                    UpdatedUtc = now.AddHours(-6)
                },
                new OnDutyRequestEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1007"].Id,
                    Purpose = "Autumn-Winter Brand Partner Showcase",
                    FromDate = today.AddDays(-7),
                    ToDate = today.AddDays(-6),
                    Location = "New Delhi — Pragati Maidan",
                    Reason = "Catalog selection and digital lookbook shoot coordination.",
                    State = "Approved",
                    DecidedByName = "Simran Kaur",
                    DecidedUtc = now.AddDays(-8),
                    DecisionComment = "Approved for seasonal buying cycle.",
                    RequestedUtc = now.AddDays(-9),
                    UpdatedUtc = now.AddDays(-8)
                },
                new OnDutyRequestEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1013"].Id,
                    Purpose = "External Packaging Expo",
                    FromDate = today.AddDays(-10),
                    ToDate = today.AddDays(-9),
                    Location = "Mumbai",
                    Reason = "Attending retail fixture exhibition.",
                    State = "Rejected",
                    DecidedByName = "Aditya Nanda",
                    DecidedUtc = now.AddDays(-11),
                    DecisionComment = "Deferred to next quarter due to Diwali visual merchandising rollout.",
                    RequestedUtc = now.AddDays(-12),
                    UpdatedUtc = now.AddDays(-11)
                });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // 6. Employee Timeline & Profile Change Requests
        if (!await dbContext.EmployeeTimeline.AnyAsync(x => x.TenantKey == TenantKey, cancellationToken))
        {
            var timelineRows = new List<EmployeeTimelineEntity>();
            foreach (var emp in employees)
            {
                timelineRows.Add(new EmployeeTimelineEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = emp.Id,
                    EventType = "Hired",
                    Title = $"Joined as {emp.JobTitle}",
                    Summary = $"Onboarded in {emp.DepartmentCode} ({emp.LocationCode}) under grade {emp.GradeCode}.",
                    EffectiveOn = emp.JoinedOn,
                    ActorName = "Simran Kaur",
                    OccurredUtc = new DateTimeOffset(emp.JoinedOn.ToDateTime(new TimeOnly(9, 30)), TimeSpan.Zero)
                });
            }

            foreach (var mgrNum in new[] { "EMP-1001", "EMP-1004", "EMP-1007", "EMP-1011", "EMP-1015" })
            {
                var mgr = empByNum[mgrNum];
                var promoDate = mgr.JoinedOn.AddYears(1) < today ? mgr.JoinedOn.AddYears(1) : today.AddMonths(-4);
                timelineRows.Add(new EmployeeTimelineEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = mgr.Id,
                    EventType = "Promoted",
                    Title = $"Confirmed as {mgr.JobTitle} ({mgr.GradeCode})",
                    Summary = "Annual leadership elevation following strong operational review.",
                    EffectiveOn = promoDate,
                    ActorName = "Simran Kaur",
                    OccurredUtc = new DateTimeOffset(promoDate.ToDateTime(new TimeOnly(11, 0)), TimeSpan.Zero)
                });
            }

            dbContext.EmployeeTimeline.AddRange(timelineRows);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.EmployeeChangeRequests.AnyAsync(x => x.TenantKey == TenantKey, cancellationToken))
        {
            string MakeFieldChange(string field, string currentVal, string requestedVal) =>
                JsonSerializer.Serialize(new[]
                {
                    new { field, currentValue = currentVal, requestedValue = requestedVal }
                }, JsonOptions);

            dbContext.EmployeeChangeRequests.AddRange(
                new EmployeeChangeRequestEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1008"].Id,
                    Section = "Personal",
                    ChangesJson = MakeFieldChange("Phone", "+91 98765 41008", "+91 98765 88008"),
                    Reason = "Updated primary mobile number for official OTPs and call routing.",
                    Status = "Pending",
                    RequestedBy = "Ananya Gupta",
                    SubmittedUtc = now.AddHours(-9)
                },
                new EmployeeChangeRequestEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1012"].Id,
                    Section = "Personal",
                    ChangesJson = MakeFieldChange("MaritalStatus", "SINGLE", "MARRIED"),
                    Reason = "Updating marital status for family floater insurance nomination.",
                    Status = "Pending",
                    RequestedBy = "Meera Chopra",
                    SubmittedUtc = now.AddDays(-1)
                },
                new EmployeeChangeRequestEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = empByNum["EMP-1002"].Id,
                    Section = "Personal",
                    ChangesJson = MakeFieldChange("PreferredName", "Rohan", "Rohan V."),
                    Reason = "Preferred display name on ID badge.",
                    Status = "Approved",
                    DecisionReason = "Verified and updated.",
                    RequestedBy = "Rohan Verma",
                    DecidedBy = "Simran Kaur",
                    SubmittedUtc = now.AddDays(-7),
                    DecidedUtc = now.AddDays(-6)
                });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // 7. Recruitment Interviews, Panelists, Evaluations & Onboarding Checklists
        var candidates = await dbContext.Candidates
            .Where(c => c.TenantKey == TenantKey)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var applications = await dbContext.Applications
            .Where(a => a.TenantKey == TenantKey)
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);

        if (candidates.Count > 0 && applications.Count > 0)
        {
            // Link EMP-1024 and EMP-1001 to Hired/Offered candidate records so the mobile My Onboarding
            // screen has rich checklist data for both the new joiner and the dev HR account.
            var onboardingApps = applications
                .Where(a => a.Stage is "Hired" or "Offered")
                .Take(2)
                .ToList();

            if (onboardingApps.Count > 0)
            {
                var emp1024 = empByNum["EMP-1024"];
                if (emp1024.CandidateId is null)
                {
                    emp1024.CandidateId = onboardingApps[0].CandidateId;
                }

                if (onboardingApps.Count > 1)
                {
                    var emp1001 = empByNum["EMP-1001"];
                    if (emp1001.CandidateId is null)
                    {
                        emp1001.CandidateId = onboardingApps[1].CandidateId;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var existingOnboardedCandidates = (await dbContext.Onboardings
                    .Where(x => x.TenantKey == TenantKey)
                    .Select(x => x.CandidateId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            foreach (var (app, idx) in onboardingApps.Select((a, i) => (a, i)))
            {
                if (existingOnboardedCandidates.Contains(app.CandidateId))
                {
                    continue;
                }

                var onboarding = new OnboardingEntity
                {
                    TenantKey = TenantKey,
                    ApplicationId = app.Id,
                    CandidateId = app.CandidateId,
                    Stage = "InProgress",
                    JoiningOn = idx == 0 ? empByNum["EMP-1024"].JoinedOn : empByNum["EMP-1001"].JoinedOn,
                    StartedUtc = now.AddDays(-12),
                    UpdatedUtc = now.AddDays(-1)
                };
                dbContext.Onboardings.Add(onboarding);
                await dbContext.SaveChangesAsync(cancellationToken);

                dbContext.OnboardingTasks.AddRange(
                    new OnboardingTaskEntity
                    {
                        TenantKey = TenantKey,
                        OnboardingId = onboarding.Id,
                        Code = "PAN_CARD",
                        Title = "Upload PAN Card",
                        Instructions = "Provide a clear scan of your Permanent Account Number card for payroll tax setup.",
                        TaskType = "Document",
                        OwnerKind = "Candidate",
                        IsRequired = true,
                        SortOrder = 1,
                        Status = "Approved",
                        DecidedBy = "Simran Kaur",
                        DecidedUtc = now.AddDays(-8),
                        UpdatedUtc = now.AddDays(-8)
                    },
                    new OnboardingTaskEntity
                    {
                        TenantKey = TenantKey,
                        OnboardingId = onboarding.Id,
                        Code = "AADHAAR_ID",
                        Title = "Upload Aadhaar / Address Proof",
                        Instructions = "Upload front and back copy of government-issued identity and address proof.",
                        TaskType = "Document",
                        OwnerKind = "Candidate",
                        IsRequired = true,
                        SortOrder = 2,
                        Status = "Approved",
                        DecidedBy = "Simran Kaur",
                        DecidedUtc = now.AddDays(-7),
                        UpdatedUtc = now.AddDays(-7)
                    },
                    new OnboardingTaskEntity
                    {
                        TenantKey = TenantKey,
                        OnboardingId = onboarding.Id,
                        Code = "BANK_PROOF",
                        Title = "Cancelled Cheque / Bank Passbook",
                        Instructions = "Upload a cancelled cheque showing account number and IFSC for salary credit.",
                        TaskType = "Document",
                        OwnerKind = "Candidate",
                        IsRequired = true,
                        SortOrder = 3,
                        Status = "Approved",
                        DecidedBy = "Simran Kaur",
                        DecidedUtc = now.AddDays(-5),
                        UpdatedUtc = now.AddDays(-5)
                    },
                    new OnboardingTaskEntity
                    {
                        TenantKey = TenantKey,
                        OnboardingId = onboarding.Id,
                        Code = "EMERGENCY_FORM",
                        Title = "Emergency Contact & Blood Group Form",
                        Instructions = "Complete emergency contact and medical insurance nomination details.",
                        TaskType = "Form",
                        OwnerKind = "Candidate",
                        IsRequired = true,
                        SortOrder = 4,
                        Status = idx == 0 ? "Submitted" : "Approved",
                        FormValuesJson = "{\"bloodGroup\":\"O+\",\"emergencyContact\":\"+91 98150 11001\"}",
                        DecidedBy = idx == 0 ? null : "Simran Kaur",
                        DecidedUtc = idx == 0 ? null : now.AddDays(-4),
                        UpdatedUtc = now.AddDays(-2)
                    },
                    new OnboardingTaskEntity
                    {
                        TenantKey = TenantKey,
                        OnboardingId = onboarding.Id,
                        Code = "PREV_RELIEVING",
                        Title = "Previous Employer Relieving Letter",
                        Instructions = "Upload final experience and relieving certificate from your previous organisation.",
                        TaskType = "Document",
                        OwnerKind = "Candidate",
                        IsRequired = true,
                        SortOrder = 5,
                        Status = "Pending",
                        UpdatedUtc = now.AddDays(-1)
                    });
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (!await dbContext.Interviews.AnyAsync(x => x.TenantKey == TenantKey, cancellationToken))
            {
                var interviewApps = applications
                    .Where(a => a.Stage is "Interview" or "Shortlisted" or "Offered")
                    .Take(5)
                    .ToList();

                var scheduledDefs = new (int DayOffset, int HourUtc, string RoundCode, string RoundLabel, string Mode, string LocOrLink, string Status)[]
                {
                    (0, 11, "TECHNICAL", "Round 2 - Technical & Domain Review", "Online", "https://meet.kapson.in/hr-round-tech-101", "Scheduled"),
                    (1, 10, "SCREENING", "Round 1 - HR & Culture Fit", "Online", "https://meet.kapson.in/hr-screening-102", "Scheduled"),
                    (2, 14, "MANAGERIAL", "Round 2 - Store Operations Assessment", "Onsite", "Kapson Flagship Store, Sector 17, Chandigarh", "Scheduled"),
                    (-2, 11, "TECHNICAL", "Round 1 - Merchandising Portfolio Review", "Online", "https://meet.kapson.in/brand-review-104", "Completed"),
                    (-4, 15, "FINAL", "Final Leadership Panel", "Onsite", "Kapson Corporate HQ, Conference Room B", "Completed")
                };

                for (var i = 0; i < Math.Min(interviewApps.Count, scheduledDefs.Length); i++)
                {
                    var app = interviewApps[i];
                    var def = scheduledDefs[i];
                    var interviewDate = today.AddDays(def.DayOffset);
                    var startUtc = new DateTimeOffset(interviewDate.ToDateTime(new TimeOnly(def.HourUtc, 0)), TimeSpan.Zero);

                    var interview = new InterviewEntity
                    {
                        TenantKey = TenantKey,
                        ApplicationId = app.Id,
                        CandidateId = app.CandidateId,
                        JobOpeningId = app.JobOpeningId,
                        RoundCode = def.RoundCode,
                        RoundLabel = def.RoundLabel,
                        StartUtc = startUtc,
                        DurationMinutes = 45,
                        TimeZoneId = "UTC",
                        Mode = def.Mode,
                        LocationOrLink = def.LocOrLink,
                        Instructions = "Please review candidate portfolio and screening notes 10 minutes prior to the session.",
                        Status = def.Status,
                        ScheduledBy = "Simran Kaur",
                        CreatedUtc = now.AddDays(-5),
                        UpdatedUtc = now.AddDays(-1)
                    };
                    dbContext.Interviews.Add(interview);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    var leadAccountId = empByNum["EMP-1001"].AccountId ?? "hrdev";
                    dbContext.InterviewPanelists.Add(new InterviewPanelistEntity
                    {
                        TenantKey = TenantKey,
                        InterviewId = interview.Id,
                        PanelMemberId = leadAccountId,
                        DisplayName = "Simran Kaur",
                        IsLead = true,
                        AddedUtc = now.AddDays(-5)
                    });

                    if (def.Status == "Completed")
                    {
                        var scoresJson = JsonSerializer.Serialize(new[]
                        {
                            new { criterionCode = "DOMAIN", criterionName = "Domain Expertise", score = 4, maxScore = 5, comment = "Strong retail and merchandising fundamentals." },
                            new { criterionCode = "COMM", criterionName = "Communication", score = 5, maxScore = 5, comment = "Clear, structured responses." },
                            new { criterionCode = "CULTURE", criterionName = "Culture Fit", score = 4, maxScore = 5, comment = "Aligned with Kapson store standards." }
                        }, JsonOptions);

                        dbContext.InterviewEvaluations.Add(new InterviewEvaluationEntity
                        {
                            TenantKey = TenantKey,
                            InterviewId = interview.Id,
                            PanelMemberId = leadAccountId,
                            ScoresJson = scoresJson,
                            Recommendation = "StrongHire",
                            Notes = "Recommended to proceed to offer stage.",
                            State = "Submitted",
                            SubmittedUtc = startUtc.AddHours(2),
                            UpdatedUtc = startUtc.AddHours(2)
                        });
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }

        // 8. Policy Acknowledgements (2 policies pre-acknowledged so both Read and Unread states show)
        if (!await dbContext.PolicyAcknowledgements.AnyAsync(x => x.TenantKey == TenantKey, cancellationToken))
        {
            var policies = await dbContext.Policies
                .Where(p => p.TenantKey == TenantKey && p.IsPublished)
                .OrderBy(p => p.Id)
                .ToListAsync(cancellationToken);

            var preAckPolicies = policies
                .Where(p => p.Code is "CODE_OF_CONDUCT" or "LEAVE_POLICY")
                .ToList();

            var ackRows = new List<PolicyAcknowledgementEntity>();
            foreach (var pol in preAckPolicies)
            {
                foreach (var empNum in new[] { "EMP-1001", "EMP-1002", "EMP-1004", "EMP-1007", "EMP-1008", "EMP-1011", "EMP-1015" })
                {
                    ackRows.Add(new PolicyAcknowledgementEntity
                    {
                        TenantKey = TenantKey,
                        EmployeeId = empNum,
                        PolicyId = pol.Id,
                        Version = pol.Version,
                        AcknowledgedUtc = now.AddDays(-5)
                    });
                }
            }

            if (ackRows.Count > 0)
            {
                dbContext.PolicyAcknowledgements.AddRange(ackRows);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private static (decimal Basic, decimal Hra, decimal Conveyance, decimal OtherAllowances, decimal Pf, decimal Esi, decimal Tds)
        GradePayFor(string? gradeCode) => (gradeCode?.ToUpperInvariant()) switch
        {
            "G5" => (55000m, 22000m, 5000m, 13000m, 6600m, 0m, 7500m),
            "G4" => (42000m, 16800m, 4000m, 9200m, 5040m, 0m, 4200m),
            "G3" => (32000m, 12800m, 3200m, 6000m, 3840m, 0m, 2100m),
            "G2" => (22000m, 8800m, 2200m, 3000m, 2640m, 0m, 800m),
            _ => (15000m, 6000m, 1600m, 1400m, 1800m, 180m, 0m)
        };
}
