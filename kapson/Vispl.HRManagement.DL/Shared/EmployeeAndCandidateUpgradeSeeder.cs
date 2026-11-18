using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.Admin;
using Vispl.HRManagement.DL.Employee;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Recruitment;

namespace Vispl.HRManagement.DL.Shared;

/// <summary>
/// Idempotently upgrades existing candidates, onboarding checklists, and employees to the new
/// 8-section Employee Master schema (Personal/Family/Health/Placement, Statutory &amp; PF/ESI,
/// Permanent &amp; Current Addresses, Emergency Contacts, Education, Previous Employment, Bank
/// Details, Grade-hydrated Salary Configs, and Documents).
/// </summary>
public static class EmployeeAndCandidateUpgradeSeeder
{
    private const string TenantKey = "kapson";
    private const string BankAccountPurpose = "EmployeeBankAccount.AccountNumber";
    private const string AadhaarPurpose = "EmployeeStatutory.Aadhaar";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly byte[] MinimalPdfBytes = Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj\n" +
        "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]>>endobj\nxref\n0 4\n0000000000 65535 f \n" +
        "0000000009 00000 n \n0000000052 00000 n \n0000000101 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n164\n%%EOF\n");

    public static async Task UpgradeExistingDataAsync(
        HrManagementDbContext dbContext,
        IMediaStorage mediaStorage,
        Func<string, string, string> protectValue,
        ILogger logger,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var employees = await dbContext.Employees
            .Where(e => e.TenantKey == TenantKey)
            .OrderBy(e => e.Id)
            .ToListAsync(ct);

        if (employees.Count == 0)
        {
            return;
        }

        // Ensure one shared downloadable sample PDF exists in media storage so every seeded
        // candidate and employee document row opens cleanly.
        var sharedPdfPath = await EnsureSamplePdfAsync(mediaStorage, ct);

        // ---- 1. Upgrade Previous Candidates, Applications & Onboardings ----
        await UpgradeCandidatesAndOnboardingsAsync(dbContext, employees, sharedPdfPath, now, today, logger, ct);

        // Reload employees in case Palak Arora (Candidate 10) was provisioned above.
        employees = await dbContext.Employees
            .Where(e => e.TenantKey == TenantKey)
            .OrderBy(e => e.Id)
            .ToListAsync(ct);

        // ---- 2. Backfill All 8 Employee Master Sections for Existing Employees ----
        await BackfillEmployeesMasterAsync(dbContext, employees, sharedPdfPath, protectValue, now, today, logger, ct);
    }

    private static async Task<string> EnsureSamplePdfAsync(IMediaStorage mediaStorage, CancellationToken ct)
    {
        const string sampleFileName = "sample-verified-document.pdf";
        const string expectedRelative = $"{TenantKey}/{sampleFileName}";
        try
        {
            await using var check = await mediaStorage.OpenReadAsync(expectedRelative, ct);
            return expectedRelative;
        }
        catch
        {
            await using var ms = new MemoryStream(MinimalPdfBytes);
            return await mediaStorage.SaveAsync(TenantKey, sampleFileName, ms, ct);
        }
    }

    private static async Task UpgradeCandidatesAndOnboardingsAsync(
        HrManagementDbContext dbContext,
        List<EmployeeEntity> employees,
        string sharedPdfPath,
        DateTimeOffset now,
        DateOnly today,
        ILogger logger,
        CancellationToken ct)
    {
        var candidates = await dbContext.Candidates
            .Where(c => c.TenantKey == TenantKey)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        var applications = await dbContext.Applications
            .Where(a => a.TenantKey == TenantKey)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

        var openings = await dbContext.JobOpenings
            .Where(o => o.TenantKey == TenantKey)
            .OrderBy(o => o.Id)
            .ToListAsync(ct);

        if (candidates.Count == 0 || applications.Count == 0)
        {
            return;
        }

        var empByNum = employees.ToDictionary(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase);

        // 1a. Fix duplicate / mismatched CandidateId=10 on EMP-1001 (Simran Kaur) and EMP-1024 (Aarav Sood)
        var palakCandidate = candidates.FirstOrDefault(c =>
            string.Equals(c.Email, "palak.arora@example.com", StringComparison.OrdinalIgnoreCase));

        if (palakCandidate is not null)
        {
            if (empByNum.TryGetValue("EMP-1001", out var simran) && simran.CandidateId == palakCandidate.Id)
            {
                simran.CandidateId = null;
                simran.UpdatedUtc = now;
            }

            if (empByNum.TryGetValue("EMP-1024", out var aarav) && aarav.CandidateId == palakCandidate.Id)
            {
                aarav.CandidateId = null;
                aarav.UpdatedUtc = now;
            }

            await dbContext.SaveChangesAsync(ct);

            // Provision Palak Arora (Candidate 10, Application Stage = Hired) as her own Employee record if missing
            var palakEmp = await dbContext.Employees.FirstOrDefaultAsync(
                e => e.TenantKey == TenantKey &&
                     (e.CandidateId == palakCandidate.Id || e.Email == "palak.arora@example.com" || e.Email == "palak.arora@kapson.in"),
                ct);

            if (palakEmp is null)
            {
                var mgrId = empByNum.GetValueOrDefault("EMP-1001")?.Id;
                var joinedOn = today.AddDays(-14);
                palakEmp = new EmployeeEntity
                {
                    TenantKey = TenantKey,
                    EmployeeNumber = "EMP-1026",
                    LegalFirstName = "Palak",
                    LegalLastName = "Arora",
                    PreferredName = "Palak Arora",
                    DateOfBirth = new DateOnly(1998, 5, 19),
                    GenderCode = "FEMALE",
                    MaritalStatusCode = "SINGLE",
                    BloodGroupCode = "B_POS",
                    ReligionCode = "HINDUISM",
                    NationalityCode = "INDIAN",
                    Email = "palak.arora@example.com",
                    Phone = palakCandidate.Phone,
                    FatherName = "Sanjeev Arora",
                    FatherOccupationCode = "SELF_EMPLOYED",
                    MotherName = "Neetu Arora",
                    MotherOccupationCode = "HOMEMAKER",
                    JoinedOn = joinedOn,
                    EmploymentTypeCode = "FULL_TIME",
                    EmploymentStatusCode = "PROBATION",
                    GradeCode = "EXECUTIVE",
                    JobTitle = "HR Executive",
                    ProbationEndsOn = joinedOn.AddMonths(6),
                    CompanyCode = "KAPSON-RETAIL",
                    DivisionCode = "HO",
                    DepartmentCode = "HR",
                    SubDepartmentCode = "HR_TALENT",
                    LocationCode = "CHANDIGARH",
                    ManagerEmployeeId = mgrId,
                    CostCentre = "CC-HR",
                    CandidateId = palakCandidate.Id,
                    IsDraft = false,
                    IsActive = true,
                    CreatedUtc = now,
                    UpdatedUtc = now
                };
                dbContext.Employees.Add(palakEmp);
                await dbContext.SaveChangesAsync(ct);

                dbContext.EmployeeTimeline.Add(new EmployeeTimelineEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = palakEmp.Id,
                    EventType = "Hired",
                    Title = "Joined as HR Executive",
                    Summary = "Hired through recruitment pipeline into Talent Acquisition & Onboarding.",
                    EffectiveOn = joinedOn,
                    ActorName = "Simran Kaur",
                    OccurredUtc = now.AddDays(-14)
                });
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation("Provisioned hired candidate Palak Arora as employee EMP-1026.");
            }
        }

        // 1b. Give EMP-1024 (Aarav Sood) his own dedicated Hired Candidate + Completed Onboarding checklist
        // so Mobile 'My Onboarding' displays Aarav Sood's own checklist without colliding with Palak Arora.
        if (empByNum.TryGetValue("EMP-1024", out var emp1024) && emp1024.CandidateId is null)
        {
            var aaravCand = candidates.FirstOrDefault(c =>
                string.Equals(c.Email, emp1024.Email, StringComparison.OrdinalIgnoreCase));

            if (aaravCand is null)
            {
                aaravCand = new CandidateEntity
                {
                    TenantKey = TenantKey,
                    Email = emp1024.Email ?? "aarav.sood@kapson.in",
                    FullName = "Aarav Sood",
                    Phone = emp1024.Phone ?? "+919876543233",
                    CurrentLocation = "Chandigarh",
                    TotalExperienceYears = 1.5m,
                    CreatedUtc = now.AddDays(-25),
                    UpdatedUtc = now.AddDays(-10)
                };
                dbContext.Candidates.Add(aaravCand);
                await dbContext.SaveChangesAsync(ct);
                candidates.Add(aaravCand);
            }

            var aaravApp = await dbContext.Applications.FirstOrDefaultAsync(
                a => a.TenantKey == TenantKey && a.CandidateId == aaravCand.Id, ct);

            if (aaravApp is null && openings.Count > 0)
            {
                var retailOpening = openings[0];
                aaravApp = new ApplicationEntity
                {
                    TenantKey = TenantKey,
                    JobOpeningId = retailOpening.Id,
                    CandidateId = aaravCand.Id,
                    Stage = "Hired",
                    Source = "CareerSite",
                    NoticePeriodDays = 15,
                    ExpectedCtc = 420000m,
                    AnswersJson = JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["workModePreference"] = "On-site",
                        ["retailExperience"] = "1.5 years luxury floor sales",
                        ["highestQualification"] = "BBA"
                    }, Json),
                    Rating = 4.6m,
                    ConsentGranted = true,
                    ConsentPolicyVersion = "1.0",
                    ConsentUtc = now.AddDays(-24),
                    AppliedUtc = now.AddDays(-24),
                    StageChangedUtc = now.AddDays(-10),
                    StageChangedBy = "EMP-1001",
                    UpdatedUtc = now.AddDays(-10)
                };
                dbContext.Applications.Add(aaravApp);
                await dbContext.SaveChangesAsync(ct);
                applications.Add(aaravApp);
            }

            emp1024.CandidateId = aaravCand.Id;
            await dbContext.SaveChangesAsync(ct);

            if (aaravApp is not null &&
                !await dbContext.Onboardings.AnyAsync(o => o.TenantKey == TenantKey && o.ApplicationId == aaravApp.Id, ct))
            {
                var aaravOnb = new OnboardingEntity
                {
                    TenantKey = TenantKey,
                    ApplicationId = aaravApp.Id,
                    CandidateId = aaravCand.Id,
                    Stage = "Completed",
                    JoiningOn = emp1024.JoinedOn,
                    StartedUtc = now.AddDays(-18),
                    CompletedUtc = now.AddDays(-10),
                    UpdatedUtc = now.AddDays(-10)
                };
                dbContext.Onboardings.Add(aaravOnb);
                await dbContext.SaveChangesAsync(ct);

                await SeedUpgradedOnboardingTasksAsync(
                    dbContext, aaravOnb, aaravCand, aaravApp, sharedPdfPath, completed: true, now, ct);
            }
        }

        // 1c. Backfill AnswersJson & Resume ApplicationDocuments for ALL existing applications
        var existingAppDocs = await dbContext.ApplicationDocuments
            .Where(d => d.TenantKey == TenantKey)
            .ToListAsync(ct);
        var appsWithResume = existingAppDocs
            .Where(d => string.Equals(d.CategoryCode, "Resume", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.ApplicationId)
            .ToHashSet();

        var candById = candidates.ToDictionary(c => c.Id);

        foreach (var app in applications)
        {
            if (!candById.TryGetValue(app.CandidateId, out var cand))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(app.AnswersJson) || app.AnswersJson == "{}")
            {
                app.AnswersJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["currentLocation"] = cand.CurrentLocation ?? "Chandigarh",
                    ["totalExperienceYears"] = cand.TotalExperienceYears.ToString("0.0", CultureInfo.InvariantCulture),
                    ["noticePeriodDays"] = app.NoticePeriodDays.ToString(CultureInfo.InvariantCulture),
                    ["willingToRelocate"] = "Yes",
                    ["highestQualification"] = cand.TotalExperienceYears >= 5m ? "MBA" : "Graduate"
                }, Json);
                app.UpdatedUtc = now;
            }

            if (!appsWithResume.Contains(app.Id))
            {
                var safeSlug = cand.FullName.Replace(' ', '_');
                dbContext.ApplicationDocuments.Add(new ApplicationDocumentEntity
                {
                    TenantKey = TenantKey,
                    ApplicationId = app.Id,
                    CategoryCode = "Resume",
                    OriginalFileName = $"{safeSlug}_CV.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = MinimalPdfBytes.Length,
                    StoragePath = sharedPdfPath,
                    ScanState = "Clean",
                    UploadedUtc = app.AppliedUtc
                });
            }
        }

        await dbContext.SaveChangesAsync(ct);

        // 1d. Upgrade existing Onboardings (Onboarding 1 for Palak Arora & Onboarding 2 for Nidhi Chawla)
        // from legacy 5-task codes (PAN_CARD, AADHAAR_ID, etc.) to the full 12-task 8-section template!
        var onboardings = await dbContext.Onboardings
            .Where(o => o.TenantKey == TenantKey)
            .ToListAsync(ct);

        foreach (var onb in onboardings)
        {
            var tasks = await dbContext.OnboardingTasks
                .Where(t => t.TenantKey == TenantKey && t.OnboardingId == onb.Id)
                .OrderBy(t => t.SortOrder)
                .ToListAsync(ct);

            var hasLegacyCodes = tasks.Any(t => t.Code is "PAN_CARD" or "AADHAAR_ID" or "BANK_PROOF" or "EMERGENCY_FORM" or "PREV_RELIEVING");
            if (!hasLegacyCodes && tasks.Count >= 10)
            {
                continue;
            }

            var app = applications.FirstOrDefault(a => a.Id == onb.ApplicationId);
            var cand = candById.GetValueOrDefault(onb.CandidateId);
            if (app is null || cand is null)
            {
                continue;
            }

            dbContext.OnboardingTasks.RemoveRange(tasks);
            await dbContext.SaveChangesAsync(ct);

            var isHired = string.Equals(app.Stage, "Hired", StringComparison.OrdinalIgnoreCase);
            if (isHired)
            {
                onb.Stage = "Completed";
                onb.CompletedUtc ??= now.AddDays(-12);
                onb.UpdatedUtc = now;
            }
            else
            {
                onb.Stage = "InProgress";
                onb.UpdatedUtc = now;
                if (string.Equals(app.Stage, "Offered", StringComparison.OrdinalIgnoreCase))
                {
                    // Nidhi Chawla has an active onboarding checklist, so her stage is Onboarding.
                    // Promote Riya Mehra (#2) from Shortlisted to Offered so an Offered candidate remains on the board.
                    app.Stage = "Onboarding";
                    app.StageChangedUtc = now.AddDays(-3);
                    app.UpdatedUtc = now;

                    var riyaApp = applications.FirstOrDefault(a =>
                        candById.TryGetValue(a.CandidateId, out var c) &&
                        string.Equals(c.Email, "riya.mehra@example.com", StringComparison.OrdinalIgnoreCase) &&
                        a.Stage == "Shortlisted");
                    if (riyaApp is not null)
                    {
                        riyaApp.Stage = "Offered";
                        riyaApp.StageChangedUtc = now.AddDays(-1);
                        riyaApp.UpdatedUtc = now;
                    }
                }
            }

            await SeedUpgradedOnboardingTasksAsync(
                dbContext, onb, cand, app, sharedPdfPath, completed: isHired, now, ct);

            logger.LogInformation("Upgraded onboarding checklist #{OnboardingId} for candidate '{Candidate}' to 8-section Employee Master schema.", onb.Id, cand.FullName);
        }
    }

    private static async Task SeedUpgradedOnboardingTasksAsync(
        HrManagementDbContext dbContext,
        OnboardingEntity onb,
        CandidateEntity cand,
        ApplicationEntity app,
        string sharedPdfPath,
        bool completed,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var safeSlug = cand.FullName.Replace(' ', '_');
        var panCode = $"KPSCA{1000 + (cand.Id % 8999):D4}M";
        var aadhaarDigits = $"48927100{1000 + (cand.Id % 8999):D4}";
        var accountDigits = $"50100882{1000 + (cand.Id % 8999):D4}";
        var uanDigits = $"10159000{1000 + (cand.Id % 8999):D4}";
        var isFemale = cand.FullName.StartsWith("Palak", StringComparison.OrdinalIgnoreCase) ||
                       cand.FullName.StartsWith("Nidhi", StringComparison.OrdinalIgnoreCase);

        async Task<long> AddDocAsync(string category, string fileSuffix)
        {
            var existing = await dbContext.ApplicationDocuments.FirstOrDefaultAsync(
                d => d.TenantKey == TenantKey && d.ApplicationId == app.Id && d.CategoryCode == category, ct);
            if (existing is not null)
            {
                return existing.Id;
            }

            var doc = new ApplicationDocumentEntity
            {
                TenantKey = TenantKey,
                ApplicationId = app.Id,
                CategoryCode = category,
                OriginalFileName = $"{safeSlug}_{fileSuffix}.pdf",
                ContentType = "application/pdf",
                SizeBytes = MinimalPdfBytes.Length,
                StoragePath = sharedPdfPath,
                ScanState = "Clean",
                UploadedUtc = now.AddDays(-6)
            };
            dbContext.ApplicationDocuments.Add(doc);
            await dbContext.SaveChangesAsync(ct);
            return doc.Id;
        }

        var panDocId = await AddDocAsync("PAN", "PAN_Card");
        var aadhaarDocId = await AddDocAsync("AADHAAR", "Aadhaar_Card");
        var eduDocId = await AddDocAsync("EDUCATION_CERTIFICATES", "Degree_Certificate");
        var expDocId = await AddDocAsync("EXPERIENCE_LETTERS", "Relieving_Letter");
        var offerDocId = await AddDocAsync("SIGNED_OFFER", "Signed_Offer_Letter");

        var personalJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["legalName"] = cand.FullName,
            ["dateOfBirth"] = isFemale ? "1996-08-14" : "1999-06-11",
            ["gender"] = isFemale ? "FEMALE" : "MALE",
            ["maritalStatus"] = cand.TotalExperienceYears >= 5m ? "MARRIED" : "SINGLE",
            ["bloodGroup"] = isFemale ? "B_POS" : "O_POS",
            ["religion"] = "HINDUISM",
            ["nationality"] = "INDIAN",
            ["panNumber"] = panCode,
            ["aadhaarNumber"] = aadhaarDigits,
            ["voterId"] = $"PB{20 + cand.Id:D2}88412{cand.Id:D2}",
            ["fatherName"] = $"Rajendra {cand.FullName.Split(' ').Last()}",
            ["fatherOccupation"] = "SELF_EMPLOYED",
            ["motherName"] = $"Sunita {cand.FullName.Split(' ').Last()}",
            ["motherOccupation"] = "HOMEMAKER"
        }, Json);

        var addressJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["permanentLine1"] = $"H.No. {210 + cand.Id}, Sector 35-B",
            ["permanentLine2"] = "Near Rose Garden",
            ["permanentCity"] = "CHANDIGARH",
            ["permanentState"] = "CH_UT",
            ["permanentCountry"] = "IN",
            ["permanentPincode"] = "160022",
            ["sameAsPermanent"] = "true",
            ["emergencyContact"] = $"Rajendra {cand.FullName.Split(' ').Last()}",
            ["emergencyRelationship"] = "Father",
            ["emergencyPhone"] = $"+9198159{10000 + cand.Id:D5}",
            ["emergencyEmail"] = $"family.{cand.Id}@example.com"
        }, Json);

        var bankJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["accountHolderName"] = cand.FullName,
            ["bank"] = "HDFC Bank",
            ["accountNumber"] = accountDigits,
            ["ifsc"] = "HDFC0001204",
            ["branchName"] = "Sector 17, Chandigarh",
            ["accountType"] = "Savings",
            ["pfApplicable"] = "true",
            ["uanNumber"] = uanDigits,
            ["pfNumber"] = $"PB/CHD/0048210/000{900 + cand.Id}"
        }, Json);

        var eduJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["educationLevel"] = cand.TotalExperienceYears >= 4m ? "MBA" : "GRADUATE",
            ["qualification"] = cand.TotalExperienceYears >= 4m ? "MBA" : "BBA",
            ["specialisation"] = cand.TotalExperienceYears >= 4m ? "Retail & Operations Management" : "Human Resources & Business",
            ["institute"] = "University Business School, Chandigarh",
            ["boardOrUniversity"] = "Panjab University",
            ["passingYear"] = (isFemale ? 2019 : 2021).ToString(CultureInfo.InvariantCulture),
            ["cgpa"] = "8.4",
            ["gradeLetter"] = "A"
        }, Json);

        var prevJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["companyName"] = "Lifestyle International Pvt. Ltd.",
            ["designation"] = cand.TotalExperienceYears >= 5m ? "Assistant Store Manager" : "Executive",
            ["department"] = cand.TotalExperienceYears >= 5m ? "Retail Operations" : "Human Resources",
            ["employmentType"] = "Permanent",
            ["joinedOn"] = "2022-04-01",
            ["leftOn"] = "2026-09-30",
            ["lastAnnualCtc"] = ((app.ExpectedCtc ?? 600000m) * 0.82m).ToString("0", CultureInfo.InvariantCulture),
            ["reportingManagerName"] = "Vivek Oberoi",
            ["leavingReason"] = "BETTER_CAREER",
            ["noticeServed"] = "true"
        }, Json);

        var candStatus = completed ? "Approved" : "Submitted";
        var hrStatus = completed ? "Approved" : "Pending";
        var decidedBy = completed ? "Simran Kaur" : null;
        DateTimeOffset? decidedUtc = completed ? now.AddDays(-10) : null;

        var rows = new List<OnboardingTaskEntity>
        {
            new()
            {
                TenantKey = TenantKey, OnboardingId = onb.Id, SortOrder = 0,
                Code = "PERSONAL_DETAILS", Title = "Personal, family & statutory details",
                Instructions = "Confirm your legal name, date of birth, gender, marital status, blood group, religion, nationality, family details, PAN and Aadhaar.",
                TaskType = "Form", OwnerKind = "Candidate", IsRequired = true,
                Status = "Approved", FormValuesJson = personalJson,
                DecidedBy = "Simran Kaur", DecidedUtc = now.AddDays(-5), UpdatedUtc = now.AddDays(-5)
            },
            new()
            {
                TenantKey = TenantKey, OnboardingId = onb.Id, SortOrder = 1,
                Code = "ADDRESS_EMERGENCY", Title = "Address and emergency contact",
                Instructions = "Provide your permanent and current residential address (city, state, country, pincode) and one emergency contact.",
                TaskType = "Form", OwnerKind = "Candidate", IsRequired = true,
                Status = "Approved", FormValuesJson = addressJson,
                DecidedBy = "Simran Kaur", DecidedUtc = now.AddDays(-5), UpdatedUtc = now.AddDays(-5)
            },
            new()
            {
                TenantKey = TenantKey, OnboardingId = onb.Id, SortOrder = 2,
                Code = "BANK_ACCOUNT", Title = "Bank account & PF / ESI details",
                Instructions = "Provide your salary bank account (holder name, bank, account number, IFSC, branch, account type) and existing PF / UAN / ESI details if applicable.",
                TaskType = "Form", OwnerKind = "Candidate", IsRequired = true,
                Status = candStatus, FormValuesJson = bankJson,
                DecidedBy = decidedBy, DecidedUtc = decidedUtc, UpdatedUtc = now.AddDays(-3)
            },
            new()
            {
                TenantKey = TenantKey, OnboardingId = onb.Id, SortOrder = 3,
                Code = "EDUCATION_HISTORY", Title = "Education & qualifications",
                Instructions = "Provide your highest academic qualification, specialisation, institute, board/university, passing year and CGPA or percentage.",
                TaskType = "Form", OwnerKind = "Candidate", IsRequired = false,
                Status = candStatus, FormValuesJson = eduJson,
                DecidedBy = decidedBy, DecidedUtc = decidedUtc, UpdatedUtc = now.AddDays(-3)
            },
            new()
            {
                TenantKey = TenantKey, OnboardingId = onb.Id, SortOrder = 4,
                Code = "PREVIOUS_EMPLOYMENT", Title = "Previous company experience",
                Instructions = "If you have prior work experience, provide your previous company, designation, tenure, last annual CTC and leaving reason.",
                TaskType = "Form", OwnerKind = "Candidate", IsRequired = false,
                Status = candStatus, FormValuesJson = prevJson,
                DecidedBy = decidedBy, DecidedUtc = decidedUtc, UpdatedUtc = now.AddDays(-3)
            },
            new()
            {
                TenantKey = TenantKey, OnboardingId = onb.Id, SortOrder = 5,
                Code = "PAN", Title = "PAN card",
                Instructions = "Upload a clear scan or photograph of your PAN card.",
                TaskType = "Document", OwnerKind = "Candidate", IsRequired = true,
                Status = "Approved", DocumentId = panDocId,
                DecidedBy = "Simran Kaur", DecidedUtc = now.AddDays(-5), UpdatedUtc = now.AddDays(-5)
            },
            new()
            {
                TenantKey = TenantKey, OnboardingId = onb.Id, SortOrder = 6,
                Code = "AADHAAR", Title = "Aadhaar",
                Instructions = "Upload a clear scan or photograph of your Aadhaar card.",
                TaskType = "Document", OwnerKind = "Candidate", IsRequired = true,
                Status = "Approved", DocumentId = aadhaarDocId,
                DecidedBy = "Simran Kaur", DecidedUtc = now.AddDays(-5), UpdatedUtc = now.AddDays(-5)
            },
            new()
            {
                TenantKey = TenantKey, OnboardingId = onb.Id, SortOrder = 7,
                Code = "EDUCATION_CERTIFICATES", Title = "Educational certificates",
                Instructions = "Upload your highest qualification certificate and mark sheet.",
                TaskType = "Document", OwnerKind = "Candidate", IsRequired = true,
                Status = candStatus, DocumentId = eduDocId,
                DecidedBy = decidedBy, DecidedUtc = decidedUtc, UpdatedUtc = now.AddDays(-2)
            },
            new()
            {
                TenantKey = TenantKey, OnboardingId = onb.Id, SortOrder = 8,
                Code = "EXPERIENCE_LETTERS", Title = "Previous experience letters",
                Instructions = "Upload relieving or experience letters from previous employers.",
                TaskType = "Document", OwnerKind = "Candidate", IsRequired = false,
                Status = candStatus, DocumentId = expDocId,
                DecidedBy = decidedBy, DecidedUtc = decidedUtc, UpdatedUtc = now.AddDays(-2)
            },
            new()
            {
                TenantKey = TenantKey, OnboardingId = onb.Id, SortOrder = 9,
                Code = "SIGNED_OFFER", Title = "Signed offer letter",
                Instructions = "Upload the offer letter with your signature.",
                TaskType = "Document", OwnerKind = "Candidate", IsRequired = true,
                Status = candStatus, DocumentId = offerDocId,
                DecidedBy = decidedBy, DecidedUtc = decidedUtc, UpdatedUtc = now.AddDays(-2)
            },
            new()
            {
                TenantKey = TenantKey, OnboardingId = onb.Id, SortOrder = 10,
                Code = "BACKGROUND_VERIFICATION", Title = "Background verification & placement",
                Instructions = "Record background verification outcome and confirm division, sub-department, designation and reporting manager.",
                TaskType = "Form", OwnerKind = "Hr", IsRequired = true,
                Status = hrStatus,
                FormValuesJson = completed ? "{\"outcome\":\"Clear\",\"verifiedBy\":\"Simran Kaur\"}" : null,
                DecidedBy = decidedBy, DecidedUtc = decidedUtc, UpdatedUtc = now.AddDays(-1)
            },
            new()
            {
                TenantKey = TenantKey, OnboardingId = onb.Id, SortOrder = 11,
                Code = "ASSET_ALLOCATION", Title = "Asset allocation",
                Instructions = "Record laptop, ID badge or store equipment issued to the new joiner.",
                TaskType = "Form", OwnerKind = "Hr", IsRequired = false,
                Status = hrStatus,
                FormValuesJson = completed ? "{\"notes\":\"Issued ID badge & welcome kit\"}" : null,
                DecidedBy = decidedBy, DecidedUtc = decidedUtc, UpdatedUtc = now.AddDays(-1)
            }
        };

        dbContext.OnboardingTasks.AddRange(rows);
        await dbContext.SaveChangesAsync(ct);
    }

    private static async Task BackfillEmployeesMasterAsync(
        HrManagementDbContext dbContext,
        List<EmployeeEntity> employees,
        string sharedPdfPath,
        Func<string, string, string> protectValue,
        DateTimeOffset now,
        DateOnly today,
        ILogger logger,
        CancellationToken ct)
    {
        var statutoryMap = await dbContext.EmployeeStatutory
            .Where(s => s.TenantKey == TenantKey)
            .ToDictionaryAsync(s => s.EmployeeId, ct);

        var addressGroups = (await dbContext.EmployeeAddresses
                .Where(a => a.TenantKey == TenantKey)
                .ToListAsync(ct))
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var contactIds = (await dbContext.EmployeeContacts
                .Where(c => c.TenantKey == TenantKey)
                .Select(c => c.EmployeeId)
                .ToListAsync(ct))
            .ToHashSet();

        var eduIds = (await dbContext.EmployeeEducation
                .Where(e => e.TenantKey == TenantKey)
                .Select(e => e.EmployeeId)
                .ToListAsync(ct))
            .ToHashSet();

        var prevIds = (await dbContext.EmployeePreviousEmployment
                .Where(p => p.TenantKey == TenantKey)
                .Select(p => p.EmployeeId)
                .ToListAsync(ct))
            .ToHashSet();

        var bankMap = await dbContext.EmployeeBankAccounts
            .Where(b => b.TenantKey == TenantKey)
            .ToDictionaryAsync(b => b.EmployeeId, ct);

        var salaryMap = await dbContext.EmployeeSalaryConfigs
            .Where(s => s.TenantKey == TenantKey)
            .ToDictionaryAsync(s => s.EmployeeId, ct);

        var docIds = (await dbContext.EmployeeDocuments
                .Where(d => d.TenantKey == TenantKey)
                .Select(d => d.EmployeeId)
                .ToListAsync(ct))
            .ToHashSet();

        var gradeStructures = await dbContext.GradeSalaryStructures
            .Where(g => g.TenantKey == TenantKey)
            .ToDictionaryAsync(g => g.GradeCode, StringComparer.OrdinalIgnoreCase, ct);

        var optionNames = (await dbContext.MasterOptions
                .Where(m => m.TenantKey == TenantKey && (m.Category == "AllowanceType" || m.Category == "DeductionType"))
                .ToListAsync(ct))
            .GroupBy(m => m.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

        var companyAccounts = await dbContext.CompanyBankAccounts
            .Where(c => c.TenantKey == TenantKey && c.IsActive)
            .OrderByDescending(c => c.IsDefault)
            .ToListAsync(ct);
        var defaultCompanyAccCode = companyAccounts.FirstOrDefault()?.AccountLast4 is { } l4 ? $"CORP-{l4}" : "CORP-1920";

        string[] bloodGroups = ["O_POS", "B_POS", "A_POS", "AB_POS", "O_NEG", "A_NEG", "B_NEG"];
        string[] fatherFirsts = ["Harbhajan", "Surinder", "Rajesh", "Davinder", "Mohan", "Satish", "Kuldeep", "Virender"];
        string[] motherFirsts = ["Paramjit", "Sunita", "Kamlesh", "Balwinder", "Meenakshi", "Anita", "Manjit", "Kiran"];
        string[] spouseMaleFirsts = ["Rohit", "Karan", "jaswinder", "Sandeep", "Nitin", "Aman"];
        string[] spouseFemaleFirsts = ["Neha", "Preeti", "Harpreet", "Pooja", "Shruti", "komal"];
        (string Bank, string Ifsc, string Branch)[] banks =
        [
            ("HDFC Bank", "HDFC0001204", "Sector 17, Chandigarh"),
            ("ICICI Bank", "ICIC0000816", "Phase 7, Mohali"),
            ("Axis Bank", "UTIB0000312", "Sector 35, Chandigarh"),
            ("State Bank of India", "SBIN0004519", "Connaught Place, New Delhi")
        ];

        var existingPans = statutoryMap.Values
            .Where(v => !string.IsNullOrWhiteSpace(v.PanNumber))
            .Select(v => v.PanNumber!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var updatedCount = 0;

        for (var idx = 0; idx < employees.Count; idx++)
        {
            var emp = employees[idx];
            var empChanged = false;

            // 1. Personal, Health, Family & Sub-Department columns on Employees
            if (string.IsNullOrWhiteSpace(emp.BloodGroupCode))
            {
                emp.BloodGroupCode = bloodGroups[idx % bloodGroups.Length];
                empChanged = true;
            }

            if (string.IsNullOrWhiteSpace(emp.ReligionCode))
            {
                emp.ReligionCode = IsSikhSurname(emp.LegalLastName) ? "SIKHISM" : "HINDUISM";
                empChanged = true;
            }

            if (string.IsNullOrWhiteSpace(emp.NationalityCode))
            {
                emp.NationalityCode = "INDIAN";
                empChanged = true;
            }

            if (string.IsNullOrWhiteSpace(emp.FatherName))
            {
                emp.FatherName = $"{fatherFirsts[idx % fatherFirsts.Length]} {emp.LegalLastName}";
                emp.FatherOccupationCode = idx % 3 == 0 ? "RETIRED" : idx % 2 == 0 ? "SELF_EMPLOYED" : "SALARIED";
                empChanged = true;
            }

            if (string.IsNullOrWhiteSpace(emp.MotherName))
            {
                emp.MotherName = $"{motherFirsts[idx % motherFirsts.Length]} {emp.LegalLastName}";
                emp.MotherOccupationCode = idx % 2 == 0 ? "HOMEMAKER" : "SALARIED";
                empChanged = true;
            }

            if (string.Equals(emp.MaritalStatusCode, "MARRIED", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(emp.SpouseName))
            {
                var spFirst = string.Equals(emp.GenderCode, "FEMALE", StringComparison.OrdinalIgnoreCase)
                    ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spouseMaleFirsts[idx % spouseMaleFirsts.Length])
                    : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spouseFemaleFirsts[idx % spouseFemaleFirsts.Length]);
                emp.SpouseName = $"{spFirst} {emp.LegalLastName}";
                emp.SpouseOccupationCode = idx % 2 == 0 ? "SALARIED" : "PROFESSIONAL";
                empChanged = true;
            }

            if (string.IsNullOrWhiteSpace(emp.SubDepartmentCode))
            {
                emp.SubDepartmentCode = InferSubDepartment(emp.DepartmentCode, emp.JobTitle ?? string.Empty);
                empChanged = true;
            }

            if (emp.DateOfBirth is null)
            {
                emp.DateOfBirth = new DateOnly(Math.Clamp(emp.JoinedOn.Year - 26, 1975, 2003), (idx % 12) + 1, (idx % 25) + 1);
                empChanged = true;
            }

            if (empChanged)
            {
                emp.UpdatedUtc = now;
                updatedCount++;
            }

            // 2. Statutory & PF/ESI
            var panCandidate = $"KPSEM{1000 + emp.Id:D4}{(char)('A' + (idx % 20))}";
            if (!statutoryMap.TryGetValue(emp.Id, out var stat))
            {
                var panToUse = existingPans.Contains(panCandidate) ? $"KPSHR{2000 + emp.Id:D4}Z" : panCandidate;
                existingPans.Add(panToUse);

                var aadhaarPlain = $"54918200{1000 + emp.Id:D4}";
                var esiEligible = string.Equals(emp.GradeCode, "EXECUTIVE", StringComparison.OrdinalIgnoreCase);

                stat = new EmployeeStatutoryEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = emp.Id,
                    PanNumber = panToUse,
                    AadhaarProtected = protectValue(AadhaarPurpose, aadhaarPlain),
                    AadhaarLast4 = aadhaarPlain[^4..],
                    VoterId = $"CH{(idx % 9) + 1:D2}9041{100 + emp.Id:D3}",
                    PfApplicable = true,
                    PfJoinedOn = emp.JoinedOn,
                    PfScheme = "Govt",
                    PfNumber = $"PB/CHD/0048210/000{1000 + emp.Id:D4}",
                    UanNumber = $"10158000{1000 + emp.Id:D4}",
                    EpfAccountCode = "EPF-CORP-01",
                    EsiApplicable = esiEligible,
                    EsiNumber = esiEligible ? $"3112098450001{1000 + emp.Id:D4}" : null,
                    EsiAccountCode = esiEligible ? "ESI-CORP-01" : null,
                    UpdatedBy = "System",
                    UpdatedUtc = now
                };
                dbContext.EmployeeStatutory.Add(stat);
                statutoryMap[emp.Id] = stat;
            }

            // 3. Permanent & Current Addresses
            if (!addressGroups.TryGetValue(emp.Id, out var empAddrs) || empAddrs.Count == 0)
            {
                var (city, state, pin, locality) = AddressSeedForLocation(emp.LocationCode, idx);
                var line1 = $"H.No. {101 + (idx * 17)}, {locality}";

                dbContext.EmployeeAddresses.AddRange(
                    new EmployeeAddressEntity
                    {
                        TenantKey = TenantKey,
                        EmployeeId = emp.Id,
                        AddressType = "Permanent",
                        Line1 = line1,
                        Line2 = "Main Residential Enclave",
                        CityCode = city,
                        StateCode = state,
                        CountryCode = "IN",
                        Pincode = pin,
                        UpdatedUtc = now
                    },
                    new EmployeeAddressEntity
                    {
                        TenantKey = TenantKey,
                        EmployeeId = emp.Id,
                        AddressType = "Current",
                        Line1 = line1,
                        Line2 = "Main Residential Enclave",
                        CityCode = city,
                        StateCode = state,
                        CountryCode = "IN",
                        Pincode = pin,
                        UpdatedUtc = now
                    });
            }

            // 4. Emergency Contact
            if (!contactIds.Contains(emp.Id))
            {
                var isMarried = string.Equals(emp.MaritalStatusCode, "MARRIED", StringComparison.OrdinalIgnoreCase);
                dbContext.EmployeeContacts.Add(new EmployeeContactEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = emp.Id,
                    Kind = "Emergency",
                    Name = isMarried && !string.IsNullOrWhiteSpace(emp.SpouseName)
                        ? emp.SpouseName!
                        : emp.FatherName ?? $"Rajesh {emp.LegalLastName}",
                    Relationship = isMarried ? "Spouse" : "Father",
                    Phone = $"+9198140{emp.Id:00000}",
                    Email = $"emergency.{emp.LegalLastName.ToLowerInvariant()}{emp.Id}@example.com",
                    IsPrimary = true,
                    IsVerified = true,
                    UpdatedUtc = now
                });
            }

            // 5. Education Details
            if (!eduIds.Contains(emp.Id))
            {
                var (level, qual, spec, inst, univ) = EducationSeedFor(emp.DepartmentCode, emp.GradeCode);
                var gradYear = Math.Clamp((emp.DateOfBirth?.Year ?? 1995) + 22, 1998, Math.Max(1998, emp.JoinedOn.Year));
                dbContext.EmployeeEducation.Add(new EmployeeEducationEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = emp.Id,
                    EducationLevelCode = level,
                    QualificationCode = qual,
                    Specialisation = spec,
                    Institute = inst,
                    BoardOrUniversity = univ,
                    PassingYear = gradYear,
                    Cgpa = Math.Round(7.6m + ((idx % 18) * 0.1m), 1),
                    Percentage = Math.Round(72.5m + ((idx % 18) * 1.1m), 1),
                    GradeLetter = idx % 3 == 0 ? "A+" : "A",
                    CertificationName = CertificationFor(emp.DepartmentCode),
                    CertificationValidTill = today.AddYears(2),
                    IsHighest = true,
                    UpdatedUtc = now
                });
            }

            // 6. Previous Employment Details (for non-fresher grades & experienced hires)
            if (!prevIds.Contains(emp.Id) && emp.JoinedOn > new DateOnly(2018, 1, 1) &&
                (emp.GradeCode is "MANAGER" or "LEAD" or "SENIOR" or "SPECIALIST" || emp.CandidateId is not null))
            {
                var (prevCo, prevDesig) = PreviousCompanySeedFor(emp.DepartmentCode, idx);
                var leftDate = emp.JoinedOn.AddDays(-10);
                var joinDate = leftDate.AddMonths(-(18 + (idx % 24)));

                dbContext.EmployeePreviousEmployment.Add(new EmployeePreviousEmploymentEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = emp.Id,
                    CompanyName = prevCo,
                    Designation = prevDesig,
                    Department = emp.DepartmentCode,
                    PreviousEmployeeId = $"PRV-{400 + emp.Id}",
                    JoinedOn = joinDate,
                    LeftOn = leftDate,
                    EmploymentType = "Permanent",
                    LastAnnualCtc = emp.GradeCode switch
                    {
                        "MANAGER" or "LEAD" => 920000m,
                        "SENIOR" => 780000m,
                        "SPECIALIST" => 580000m,
                        _ => 380000m
                    },
                    ReportingManagerName = "Vikramaditya Mehta",
                    LeavingReasonCode = idx % 2 == 0 ? "BETTER_CAREER" : "RELOCATION",
                    NoticeServed = true,
                    Remarks = "Relieved in good standing with clean background verification.",
                    UpdatedUtc = now
                });
            }

            // 7. Bank Details (upgrade legacy PROTECTED-... strings + fill new columns + create missing accounts)
            var bankInfo = banks[idx % banks.Length];
            if (!bankMap.TryGetValue(emp.Id, out var bankRow))
            {
                var plainAcc = $"50200891{4100 + emp.Id:D4}";
                bankRow = new EmployeeBankAccountEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = emp.Id,
                    BankName = bankInfo.Bank,
                    AccountHolderName = $"{emp.LegalFirstName} {emp.LegalLastName}".Trim(),
                    AccountNumberProtected = protectValue(BankAccountPurpose, plainAcc),
                    AccountLast4 = plainAcc[^4..],
                    Ifsc = bankInfo.Ifsc,
                    BranchName = bankInfo.Branch,
                    AccountType = "Savings",
                    TransferSalarySlipBy = "Email",
                    SalaryFormat = "NEFT",
                    SalaryAccountCode = defaultCompanyAccCode,
                    AdvancedAccountCode = defaultCompanyAccCode,
                    UpdatedBy = "Simran Kaur",
                    UpdatedUtc = now
                };
                dbContext.EmployeeBankAccounts.Add(bankRow);
            }
            else
            {
                if (bankRow.AccountNumberProtected.StartsWith("PROTECTED-", StringComparison.Ordinal))
                {
                    var plainAcc = $"50200891{bankRow.AccountLast4}";
                    bankRow.AccountNumberProtected = protectValue(BankAccountPurpose, plainAcc);
                }
                bankRow.BranchName ??= bankInfo.Branch;
                bankRow.TransferSalarySlipBy ??= "Email";
                bankRow.SalaryFormat ??= "NEFT";
                bankRow.SalaryAccountCode ??= defaultCompanyAccCode;
                bankRow.AdvancedAccountCode ??= defaultCompanyAccCode;
                bankRow.UpdatedUtc = now;
            }

            // 8. Salary Details (hydrated from GradeSalaryStructures)
            if (!salaryMap.ContainsKey(emp.Id))
            {
                var gradeKey = string.IsNullOrWhiteSpace(emp.GradeCode) ? "EXECUTIVE" : emp.GradeCode!;
                if (!gradeStructures.TryGetValue(gradeKey, out var gradeStruct))
                {
                    gradeStruct = gradeStructures.Values.FirstOrDefault();
                }

                var basic = gradeStruct?.BasicMonthly ?? 28000m;
                var components = BuildCalculatedSalaryRows(gradeStruct?.ComponentsJson, basic, optionNames);

                dbContext.EmployeeSalaryConfigs.Add(new EmployeeSalaryConfigEntity
                {
                    TenantKey = TenantKey,
                    EmployeeId = emp.Id,
                    BasicSalary = basic,
                    ApplyMaxLimit = true,
                    EpfEmployerShareRule = "Apply EPF Employer Share on Maxlimit",
                    EpfOnBasicOnly = true,
                    ComponentsJson = JsonSerializer.Serialize(components, Json),
                    UpdatedUtc = now
                });
            }

            // 9. Employee Documents (with valid MediaAsset rows)
            if (!docIds.Contains(emp.Id))
            {
                var empSlug = $"{emp.EmployeeNumber}_{emp.LegalFirstName}_{emp.LegalLastName}".Replace(' ', '_');
                var panDocAsset = new MediaAssetEntity
                {
                    TenantKey = TenantKey,
                    OriginalFileName = $"{empSlug}_PAN.pdf",
                    StoredFileName = Path.GetFileName(sharedPdfPath),
                    StoragePath = sharedPdfPath,
                    ContentType = "application/pdf",
                    Length = MinimalPdfBytes.Length,
                    CreatedUtc = now
                };
                var offerDocAsset = new MediaAssetEntity
                {
                    TenantKey = TenantKey,
                    OriginalFileName = $"{empSlug}_Appointment_Letter.pdf",
                    StoredFileName = Path.GetFileName(sharedPdfPath),
                    StoragePath = sharedPdfPath,
                    ContentType = "application/pdf",
                    Length = MinimalPdfBytes.Length,
                    CreatedUtc = now
                };
                dbContext.MediaAssets.AddRange(panDocAsset, offerDocAsset);
                await dbContext.SaveChangesAsync(ct);

                dbContext.EmployeeDocuments.AddRange(
                    new EmployeeDocumentEntity
                    {
                        TenantKey = TenantKey,
                        EmployeeId = emp.Id,
                        DocumentType = "PAN Card",
                        DocumentNumber = stat.PanNumber,
                        IssuedOn = emp.JoinedOn.AddYears(-2),
                        MediaAssetId = panDocAsset.Id,
                        OriginalFileName = panDocAsset.OriginalFileName,
                        PublicUrl = $"/media/{panDocAsset.Id}",
                        UpdatedUtc = now
                    },
                    new EmployeeDocumentEntity
                    {
                        TenantKey = TenantKey,
                        EmployeeId = emp.Id,
                        DocumentType = "Signed Offer Letter",
                        DocumentNumber = $"OFR-{emp.EmployeeNumber}",
                        IssuedOn = emp.JoinedOn,
                        MediaAssetId = offerDocAsset.Id,
                        OriginalFileName = offerDocAsset.OriginalFileName,
                        PublicUrl = $"/media/{offerDocAsset.Id}",
                        UpdatedUtc = now
                    });
            }
        }

        await dbContext.SaveChangesAsync(ct);
        if (updatedCount > 0)
        {
            logger.LogInformation("Backfilled 8-section Employee Master fields & child records for {Count} employee(s).", employees.Count);
        }
    }

    private static List<EmployeeSalaryAllowanceRowDto> BuildCalculatedSalaryRows(
        string? componentsJson,
        decimal basic,
        IReadOnlyDictionary<string, string> optionNames)
    {
        var dto = new EmployeeSalaryWorkspaceDto { BasicSalary = basic };
        if (!string.IsNullOrWhiteSpace(componentsJson))
        {
            try
            {
                var raw = JsonSerializer.Deserialize<List<GradeSalaryComponentDto>>(componentsJson, Json) ?? [];
                dto.Components = raw.Select(c => new EmployeeSalaryAllowanceRowDto
                {
                    Code = c.ComponentCode,
                    Name = optionNames.GetValueOrDefault(c.ComponentCode) ?? c.ComponentName ?? c.ComponentCode,
                    Kind = c.Kind,
                    IsPercentage = c.CalculationType != SalaryCalculationTypes.Fixed,
                    Amount = c.Value,
                    OnBasicOrGross = c.CalculationType == SalaryCalculationTypes.PercentOfGross ? "Gross" : "Basic",
                    VaryOnDays = true,
                    CalculationOn = "Salary"
                }).ToList();
                dto.Recalculate();
            }
            catch
            {
                dto.Components = [];
            }
        }
        return dto.Components;
    }

    private static bool IsSikhSurname(string lastName) =>
        lastName is "Kaur" or "Singh" or "Sandhu" or "Gill" or "Dhillon" or "Bains" or "Mann" or "Brar" or "Sidhu" or "Bajwa" or "Randhawa" or "Ahluwalia";

    private static string InferSubDepartment(string deptCode, string jobTitle) => deptCode.ToUpperInvariant() switch
    {
        "HR" => jobTitle.Contains("Manager", StringComparison.OrdinalIgnoreCase) ? "HR_PEOPLE_OPS" : "HR_TALENT",
        "FINOPS" => jobTitle.Contains("Analyst", StringComparison.OrdinalIgnoreCase) ? "FINOPS_FPNA" : "FINOPS_ACCOUNTS",
        "DIGITAL" => jobTitle.Contains("Engineer", StringComparison.OrdinalIgnoreCase) ? "DIGITAL_ENG" : "DIGITAL_CAT",
        "BRAND" => "BRAND_VM",
        "RETAIL" => jobTitle.Contains("Manager", StringComparison.OrdinalIgnoreCase) ? "RETAIL_OPS" : "RETAIL_FLOOR",
        _ => "RETAIL_FLOOR"
    };

    private static (string City, string State, string Pincode, string Locality) AddressSeedForLocation(string locCode, int idx) =>
        locCode.ToUpperInvariant() switch
        {
            "MOHALI" => ("MOHALI", "PB", "160062", $"Phase {(idx % 9) + 3}, SAS Nagar"),
            "CHD_MOH" => idx % 2 == 0
                ? ("CHANDIGARH", "CH_UT", "160036", "Sector 38-West")
                : ("PANCHKULA", "HR_ST", "134109", "Sector 12-A"),
            "DELHI_NCR" => idx % 2 == 0
                ? ("GURUGRAM", "HR_ST", "122002", "DLF Phase 2")
                : ("DELHI", "DL", "110024", "Lajpat Nagar III"),
            _ => ("CHANDIGARH", "CH_UT", "160017", $"Sector {15 + (idx % 22)}-C")
        };

    private static (string Level, string Qual, string Spec, string Inst, string Univ) EducationSeedFor(string deptCode, string? gradeCode) =>
        deptCode.ToUpperInvariant() switch
        {
            "DIGITAL" => ("GRADUATE", "BTECH", "Computer Science & Engineering", "PEC University of Technology, Chandigarh", "PEC Chandigarh"),
            "FINOPS" => gradeCode is "MANAGER" or "SPECIALIST"
                ? ("MBA", "MBA", "Finance & Corporate Accounting", "University Business School, Chandigarh", "Panjab University")
                : ("GRADUATE", "BCOM", "Accounting & Taxation", "DAV College, Sector 10, Chandigarh", "Panjab University"),
            "HR" => ("MBA", "MBA", "Human Resource Management", "LM Thapar School of Management", "Thapar Institute"),
            "BRAND" => ("GRADUATE", "BA", "Fashion Communication & Merchandising", "NIFT Campus, Hauz Khas", "NIFT New Delhi"),
            _ => gradeCode == "MANAGER"
                ? ("MBA", "MBA", "Retail & Marketing Management", "Chandigarh University School of Business", "Chandigarh University")
                : ("GRADUATE", "BBA", "Retail Operations & Marketing", "GGDSD College, Sector 32, Chandigarh", "Panjab University")
        };

    private static string CertificationFor(string deptCode) => deptCode.ToUpperInvariant() switch
    {
        "DIGITAL" => "AWS Certified Developer Associate",
        "FINOPS" => "NISM & GST Compliance Certification",
        "HR" => "SHRM-CP People Operations",
        "BRAND" => "Luxury Retail Visual Standards",
        _ => "Certified Retail Store Operations"
    };

    private static (string Company, string Designation) PreviousCompanySeedFor(string deptCode, int idx) =>
        deptCode.ToUpperInvariant() switch
        {
            "DIGITAL" => (idx % 2 == 0 ? "Myntra Designs Pvt. Ltd." : "Nykaa E-Retail Ltd.", "Software Engineer"),
            "FINOPS" => (idx % 2 == 0 ? "Deloitte Shared Services India" : "KPMG Global Services", "Senior Financial Associate"),
            "HR" => ("Shoppers Stop Ltd.", "HR Generalist"),
            "BRAND" => ("Reliance Brands Ltd.", "Visual Merchandising Specialist"),
            _ => (idx % 2 == 0 ? "Shoppers Stop Ltd." : "Marks & Spencer Reliance India", "Department Manager")
        };
}
