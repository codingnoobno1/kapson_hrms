using Vispl.HRManagement.BM.Employee;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Employee;
using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.VO.Admin;
using Vispl.HRManagement.VO.Employee;
using Xunit;

namespace Vispl.HRManagement.Tests;

public sealed class CandidateProvisioningTests
{
    [Fact]
    public async Task HireAsync_PopulatesAllEightEmployeeMasterSections_FromCandidateOnboarding()
    {
        var empRepo = new FakeEmployeeRepo();
        var wsRepo = new FakeWorkspaceRepo(["EMP-1025"]);
        var mediaRepo = new FakeMediaRepo();
        var sut = new EmployeeProvisioning(
            empRepo,
            wsRepo,
            new FakeMasters(),
            new FakeDesignations(),
            new FakeGradeSalaries(),
            mediaRepo,
            new FakeProtector());

        var forms = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["PERSONAL_DETAILS"] = new Dictionary<string, string>
            {
                ["legalName"] = "Nidhi Raj Chawla",
                ["dateOfBirth"] = "1995-07-18",
                ["gender"] = "FEMALE",
                ["maritalStatus"] = "MARRIED",
                ["bloodGroup"] = "O_POS",
                ["religion"] = "HINDUISM",
                ["nationality"] = "INDIAN",
                ["panNumber"] = "ABCDE1234F",
                ["aadhaarNumber"] = "489271005544",
                ["voterId"] = "CH0198812",
                ["fatherName"] = "Rajendra Chawla",
                ["fatherOccupation"] = "SELF_EMPLOYED",
                ["motherName"] = "Sunita Chawla",
                ["motherOccupation"] = "HOMEMAKER",
                ["spouseName"] = "Karan Chawla",
                ["spouseOccupation"] = "SALARIED"
            },
            ["ADDRESS_EMERGENCY"] = new Dictionary<string, string>
            {
                ["permanentLine1"] = "H.No. 402, Sector 35-B",
                ["permanentCity"] = "CHANDIGARH",
                ["permanentState"] = "CH_UT",
                ["permanentCountry"] = "IN",
                ["permanentPincode"] = "160022",
                ["sameAsPermanent"] = "true",
                ["emergencyContact"] = "Karan Chawla",
                ["emergencyRelationship"] = "Husband",
                ["emergencyPhone"] = "9815011223",
                ["emergencyEmail"] = "karan.chawla@example.com"
            },
            ["BANK_ACCOUNT"] = new Dictionary<string, string>
            {
                ["accountHolderName"] = "Nidhi Raj Chawla",
                ["bank"] = "HDFC Bank",
                ["accountNumber"] = "501008821234",
                ["ifsc"] = "HDFC0001204",
                ["branchName"] = "Sector 17, Chandigarh",
                ["accountType"] = "Savings",
                ["pfApplicable"] = "true",
                ["uanNumber"] = "101580009988",
                ["pfNumber"] = "PB/CHD/0048210/00099"
            },
            ["EDUCATION_HISTORY"] = new Dictionary<string, string>
            {
                ["educationLevel"] = "MBA",
                ["qualification"] = "MBA",
                ["specialisation"] = "Retail Management",
                ["institute"] = "University Business School",
                ["boardOrUniversity"] = "Panjab University",
                ["passingYear"] = "2018",
                ["cgpa"] = "8.7",
                ["gradeLetter"] = "A+"
            },
            ["PREVIOUS_EMPLOYMENT"] = new Dictionary<string, string>
            {
                ["companyName"] = "Lifestyle International",
                ["designation"] = "Assistant Store Manager",
                ["department"] = "Retail",
                ["employmentType"] = "Permanent",
                ["joinedOn"] = "2021-04-01",
                ["leftOn"] = "2026-09-30",
                ["lastAnnualCtc"] = "820000",
                ["leavingReason"] = "BETTER_CAREER",
                ["noticeServed"] = "true"
            },
            ["BACKGROUND_VERIFICATION"] = new Dictionary<string, string>
            {
                ["outcome"] = "Clear",
                ["divisionCode"] = "STORE",
                ["subDepartmentCode"] = "RETAIL_OPS",
                ["designationCode"] = "SM",
                ["reportingManagerId"] = "1",
                ["verifiedBy"] = "Simran Kaur"
            }
        };

        var docs = new List<HiredCandidateDocument>
        {
            new("PAN", "Nidhi_PAN.pdf", "application/pdf", 1024, "kapson/onboarding/pan.pdf", DateTimeOffset.UtcNow),
            new("AADHAAR", "Nidhi_Aadhaar.pdf", "application/pdf", 1024, "kapson/onboarding/aadhaar.pdf", DateTimeOffset.UtcNow),
            new("SIGNED_OFFER", "Nidhi_Offer.pdf", "application/pdf", 2048, "kapson/onboarding/offer.pdf", DateTimeOffset.UtcNow)
        };

        var id = await sut.HireAsync(new HireRequest(
            "kapson",
            4,
            "Nidhi Chawla",
            "nidhi.chawla@example.com",
            "9815100104",
            new DateOnly(2026, 11, 1),
            "Retail Operations",
            "Chandigarh, Mohali",
            "Store Manager",
            "Full Time",
            "Chandigarh",
            7.0m,
            950000m,
            forms,
            docs));

        Assert.True(id > 0);
        var saved = wsRepo.LastSaved!;
        Assert.Equal(4L, saved.CandidateId);
        Assert.Equal("EMP-1026", saved.Personal.EmployeeNumber);
        Assert.Equal("Nidhi", saved.Personal.FirstName);
        Assert.Equal("Raj", saved.Personal.MiddleName);
        Assert.Equal("Chawla", saved.Personal.LastName);
        Assert.Equal("+919815100104", saved.Personal.Mobile);
        Assert.Equal("RETAIL", saved.Personal.DepartmentCode);
        Assert.Equal("RETAIL_OPS", saved.Personal.SubDepartmentCode);
        Assert.Equal("STORE", saved.Personal.DivisionCode);
        Assert.Equal("CHD_MOH", saved.Personal.LocationCode);
        Assert.Equal("SM", saved.Personal.DesignationCode);
        Assert.Equal("O_POS", saved.Personal.BloodGroupCode);
        Assert.Equal("HINDUISM", saved.Personal.ReligionCode);
        Assert.Equal("Rajendra Chawla", saved.Personal.FatherName);
        Assert.Equal("Karan Chawla", saved.Personal.SpouseName);
        Assert.Equal("ABCDE1234F", saved.Personal.PanNumber);
        Assert.Equal("5544", saved.Personal.AadhaarLast4);
        Assert.Contains("EmployeeStatutory.Aadhaar", saved.AadhaarProtected);

        // Addresses + Emergency Contact
        Assert.Equal("160022", saved.Personal.PermanentAddress.Pincode);
        Assert.Equal("CHANDIGARH", saved.Personal.PermanentAddress.CityCode);
        Assert.Single(empRepo.Contacts);
        Assert.Equal("Karan Chawla", empRepo.Contacts[0].Name);
        Assert.Equal("+919815011223", empRepo.Contacts[0].Phone);

        // Education + Previous Employment
        Assert.Single(saved.Education);
        Assert.Equal("MBA", saved.Education[0].QualificationCode);
        Assert.Equal(8.7m, saved.Education[0].Cgpa);
        Assert.Single(saved.PreviousCompanies);
        Assert.Equal("Lifestyle International", saved.PreviousCompanies[0].CompanyName);
        Assert.Equal("BETTER_CAREER", saved.PreviousCompanies[0].LeavingReasonCode);

        // Bank + PF/ESI + Salary + Documents
        Assert.Equal("HDFC Bank", saved.Bank.BankName);
        Assert.Equal("1234", saved.BankAccountLast4);
        Assert.Contains("EmployeeBankAccount.AccountNumber", saved.BankAccountNumberProtected);
        Assert.True(saved.PfEsi.PfApplicable);
        Assert.Equal("101580009988", saved.PfEsi.UanNumber);
        Assert.Equal("MANAGER", saved.Salary.GradeCode);
        Assert.Equal(55000m, saved.Salary.BasicSalary);
        Assert.NotEmpty(saved.Salary.Components);
        Assert.Equal(3, saved.Documents.Count);
        Assert.Contains(saved.Documents, d => d.DocumentType == "PAN Card" && d.DocumentNumber == "ABCDE1234F");
    }

    [Fact]
    public async Task HireAsync_ResolvesDisplaySynonyms_ForMinimalApiVerificationPayload()
    {
        var empRepo = new FakeEmployeeRepo();
        var wsRepo = new FakeWorkspaceRepo(["EMP-1010"]);
        var sut = new EmployeeProvisioning(
            empRepo,
            wsRepo,
            new FakeMasters(),
            new FakeDesignations(),
            new FakeGradeSalaries(),
            new FakeMediaRepo(),
            new FakeProtector());

        // Mirrors scripts/verify-hiring-chain.py payload exactly
        var forms = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["PERSONAL_DETAILS"] = new Dictionary<string, string>
            {
                ["legalName"] = "Nikhil Rao",
                ["dateOfBirth"] = "1990-04-12",
                ["gender"] = "Male",
                ["maritalStatus"] = "Married"
            },
            ["ADDRESS_EMERGENCY"] = new Dictionary<string, string>
            {
                ["address"] = "14 Mall Road, Chandigarh",
                ["emergencyContact"] = "Meera Rao",
                ["emergencyPhone"] = "+919820011224"
            },
            ["BANK_ACCOUNT"] = new Dictionary<string, string>
            {
                ["bank"] = "HDFC Bank",
                ["accountNumber"] = "50100234567890",
                ["ifsc"] = "HDFC0000123"
            }
        };

        var id = await sut.HireAsync(new HireRequest(
            "kapson",
            99,
            "Nikhil Rao",
            "nikhil.rao@example.com",
            "+919820011223",
            new DateOnly(2026, 11, 15),
            "Retail Operations",
            "Chandigarh, Mohali",
            "Store Manager",
            "Full Time",
            TaskForms: forms));

        Assert.True(id > 0);
        var saved = wsRepo.LastSaved!;
        Assert.Equal("MALE", saved.Personal.GenderCode);
        Assert.Equal("MARRIED", saved.Personal.MaritalStatusCode);
        Assert.Equal("RETAIL", saved.Personal.DepartmentCode);
        Assert.Equal("CHD_MOH", saved.Personal.LocationCode);
        Assert.Equal("CHANDIGARH", saved.Personal.PermanentAddress.CityCode);
        Assert.Equal("CH_UT", saved.Personal.PermanentAddress.StateCode);
        Assert.Equal("7890", saved.BankAccountLast4);
        Assert.Single(empRepo.Contacts);
    }

    private sealed class FakeProtector : ISensitiveValueProtector
    {
        public string Protect(string purpose, string value) => $"ENC:{purpose}:{value}";
        public string Unprotect(string purpose, string protectedValue) => protectedValue.Split(':')[^1];
    }

    private sealed class FakeEmployeeRepo : IEmployeeRepository
    {
        public List<EmployeeContactRecord> Contacts { get; } = [];

        public Task<EmployeeRecord?> GetByCandidateAsync(string tenantKey, long candidateId, CancellationToken ct = default) =>
            Task.FromResult<EmployeeRecord?>(null);

        public Task<IReadOnlyList<EmployeeRecord>> FindPossibleDuplicatesAsync(
            string tenantKey, string? email, string? phone, string? employeeNumber, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EmployeeRecord>>([]);

        public Task<long> UpsertContactAsync(EmployeeContactRecord contact, CancellationToken ct = default)
        {
            Contacts.Add(contact);
            return Task.FromResult((long)Contacts.Count);
        }

        public Task<IReadOnlyList<EmployeeRecord>> QueryAsync(string tenantKey, EmployeeQueryFilter filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EmployeeRecord?> GetAsync(string tenantKey, long employeeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EmployeeRecord?> GetByNumberAsync(string tenantKey, string employeeNumber, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EmployeeRecord?> GetByAccountAsync(string tenantKey, string accountId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountAsync(string tenantKey, EmployeeQueryFilter filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<long>> GetIdsAsync(string tenantKey, EmployeeQueryFilter filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PersonMomentRecord>> GetMomentsAsync(string tenantKey, EmployeeQueryFilter filter, DateOnly asOf, int belatedDays, int upcomingDays, bool anniversaries, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<ScopeOptionRecord> Companies, IReadOnlyList<ScopeOptionRecord> Units)> GetScopeOptionsAsync(string tenantKey, DateOnly? joinedOnOrBefore = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<long> UpsertAsync(EmployeeRecord employee, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EmployeeContactRecord>> GetContactsAsync(string tenantKey, long employeeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveContactAsync(string tenantKey, long employeeId, long contactId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EmployeeDependantRecord>> GetDependantsAsync(string tenantKey, long employeeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EmployeeTimelineRecord>> GetTimelineAsync(string tenantKey, long employeeId, string? eventType, DateOnly? from, DateOnly? to, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddTimelineAsync(EmployeeTimelineRecord entry, CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> AddChangeRequestAsync(EmployeeChangeRequestRecord request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EmployeeChangeRequestRecord?> GetChangeRequestAsync(string tenantKey, long changeRequestId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EmployeeChangeRequestRecord>> QueryChangeRequestsAsync(string tenantKey, string? status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateChangeRequestStatusAsync(string tenantKey, long changeRequestId, string status, string? decisionReason, string? decidedBy, DateTimeOffset decidedUtc, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeWorkspaceRepo(IReadOnlyList<string> existingNumbers) : IEmployeeWorkspaceRepository
    {
        public EmployeeWorkspaceAggregate? LastSaved { get; private set; }

        public Task<IReadOnlyList<string>> GetAllEmployeeNumbersAsync(string tenantKey, CancellationToken ct = default) =>
            Task.FromResult(existingNumbers);

        public Task<string?> FindConflictAsync(string tenantKey, long? excludeEmployeeId, string employeeNumber, string? email, string? normalizedPhone, string? panNumber, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task<long> SaveAsync(EmployeeWorkspaceAggregate aggregate, CancellationToken ct = default)
        {
            aggregate.EmployeeId = 42;
            LastSaved = aggregate;
            return Task.FromResult(42L);
        }

        public Task<IReadOnlyList<EmployeeExplorerItemRecord>> ListExplorerAsync(string tenantKey, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EmployeeWorkspaceAggregate?> GetAsync(string tenantKey, long employeeId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeMasters : IMasterOptionRepository
    {
        public Task<IReadOnlyList<MasterOptionRecord>> GetAllAsync(string tenantKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MasterOptionRecord>>([
                Opt(MasterCategories.Department, "RETAIL", "Retail Operations"),
                Opt(MasterCategories.Department, "RETAIL_OPS", "Store Inventory & Operations", "RETAIL"),
                Opt(MasterCategories.Division, "STORE", "Store / Retail Unit"),
                Opt(MasterCategories.Location, "CHD_MOH", "Chandigarh, Mohali"),
                Opt(MasterCategories.JobType, "FULL_TIME", "Full Time"),
                Opt(MasterCategories.Gender, "MALE", "Male"),
                Opt(MasterCategories.Gender, "FEMALE", "Female"),
                Opt(MasterCategories.MaritalStatus, "MARRIED", "Married"),
                Opt(MasterCategories.BloodGroup, "O_POS", "O+"),
                Opt(MasterCategories.Religion, "HINDUISM", "Hinduism"),
                Opt(MasterCategories.Nationality, "INDIAN", "Indian"),
                Opt(MasterCategories.Occupation, "SELF_EMPLOYED", "Self Employed"),
                Opt(MasterCategories.Occupation, "HOMEMAKER", "Homemaker"),
                Opt(MasterCategories.Occupation, "SALARIED", "Salaried"),
                Opt(MasterCategories.City, "CHANDIGARH", "Chandigarh"),
                Opt(MasterCategories.State, "CH_UT", "Chandigarh (UT)"),
                Opt(MasterCategories.Country, "IN", "India"),
                Opt(MasterCategories.EducationLevel, "MBA", "MBA"),
                Opt(MasterCategories.Qualification, "MBA", "MBA"),
                Opt(MasterCategories.LeavingReason, "BETTER_CAREER", "Better Career Opportunity"),
                Opt(MasterCategories.Grade, "MANAGER", "Manager")
            ]);

        private static MasterOptionRecord Opt(string cat, string code, string name, string? parent = null) =>
            new() { Id = 1, TenantKey = "kapson", Category = cat, Code = code, Name = name, ParentCode = parent, IsActive = true };

        public Task<IReadOnlyList<MasterOptionRecord>> GetActiveAsync(string tenantKey, string category, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MasterOptionRecord>>([]);
        public Task<long> UpsertAsync(MasterOptionRecord option, CancellationToken ct = default) => Task.FromResult(1L);
    }

    private sealed class FakeDesignations : IDesignationService
    {
        public Task<IReadOnlyList<DesignationDto>> GetAllAsync(string tenantKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DesignationDto>>([
                new DesignationDto("1", "SM", "Store Manager", "MANAGER", "Manager", "RETAIL", "Retail Operations", null, 1, true, DateTimeOffset.UtcNow)
            ]);

        public Task<string> SaveAsync(string tenantKey, SaveDesignationRequest request, CancellationToken ct = default) => Task.FromResult("1");
        public Task<bool> DeleteAsync(string tenantKey, string designationId, CancellationToken ct = default) => Task.FromResult(true);
        public Task ReorderAsync(string tenantKey, IReadOnlyList<string> orderedIds, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeGradeSalaries : IGradeSalaryService
    {
        public Task<IReadOnlyList<GradeSalaryStructureDto>> GetAllAsync(string tenantKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GradeSalaryStructureDto>>([
                new GradeSalaryStructureDto
                {
                    GradeCode = "MANAGER",
                    GradeName = "Manager",
                    BasicMonthly = 55000m,
                    IsConfigured = true,
                    Components =
                    [
                        new GradeSalaryComponentDto { Kind = SalaryComponentKinds.Allowance, ComponentCode = "HRA", ComponentName = "HRA", CalculationType = SalaryCalculationTypes.PercentOfBasic, Value = 40m },
                        new GradeSalaryComponentDto { Kind = SalaryComponentKinds.Deduction, ComponentCode = "PF", ComponentName = "PF", CalculationType = SalaryCalculationTypes.PercentOfBasic, Value = 12m }
                    ]
                }
            ]);

        public Task SaveAsync(string tenantKey, GradeSalaryStructureDto structure, CancellationToken ct = default) => Task.CompletedTask;
        public Task<SalaryBreakdownDto> CalculateAsync(string tenantKey, GradeSalaryStructureDto structure, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeMediaRepo : IMediaAssetRepository
    {
        private long _next = 100;
        public Task<long> AddAsync(MediaAssetRecord asset, CancellationToken ct = default) => Task.FromResult(++_next);
        public Task<IReadOnlyList<MediaAssetRecord>> GetAllAsync(string tenantKey, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaAssetRecord>>([]);
        public Task<MediaAssetRecord?> GetByIdAsync(long id, CancellationToken ct = default) => Task.FromResult<MediaAssetRecord?>(null);
    }
}
