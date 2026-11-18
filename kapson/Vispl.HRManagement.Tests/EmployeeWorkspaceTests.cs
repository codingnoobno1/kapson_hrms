using Vispl.HRManagement.BM.Admin;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.VO.Admin;
using Xunit;

namespace Vispl.HRManagement.Tests;

public sealed class EmployeeWorkspaceTests
{
    private static readonly MobileCallerContext HrCaller = new(
        "kapson",
        "hr-user-1",
        null,
        ["EMPLOYEES.VIEW", "EMPLOYEES.CREATE", "EMPLOYEES.EMPLOYMENT_MANAGE"],
        "corr-1");

    [Fact]
    public async Task Draft_employee_saves_with_minimal_fields_and_shows_as_draft_in_explorer()
    {
        var repo = new FakeWorkspaceRepository();
        var service = BuildService(repo);

        var draft = await service.GetNewTemplateAsync(HrCaller);
        Assert.Equal("EMP-0001", draft.Personal.EmployeeNumber);

        draft.Personal.FirstName = "Aarav";
        var id = await service.SaveWorkspaceAsync(HrCaller, draft, asDraft: true);

        Assert.Equal("1", id);
        var explorer = await service.GetExplorerAsync(HrCaller, new EmployeeExplorerQuery { Status = "Draft" });
        Assert.Single(explorer);
        Assert.Equal("Draft", explorer[0].StatusLabel);
        Assert.True(explorer[0].IsDraft);
        Assert.False(explorer[0].IsActive);
    }

    [Fact]
    public async Task Full_eight_section_submission_encrypts_sensitive_numbers_and_recalculates_salary()
    {
        var repo = new FakeWorkspaceRepository();
        var service = BuildService(repo);

        var ws = await service.GetNewTemplateAsync(HrCaller);
        ws.Personal.FirstName = "Riya";
        ws.Personal.LastName = "Verma";
        ws.Personal.Email = "riya.verma@kapsons.com";
        ws.Personal.Mobile = "98765 43210";
        ws.Personal.DateOfBirth = new DateOnly(1998, 5, 14);
        ws.Personal.GenderCode = "FEMALE";
        ws.Personal.DepartmentCode = "IT";
        ws.Personal.SubDepartmentCode = "IT_DEV";
        ws.Personal.DesignationCode = "DES-1";
        ws.Personal.FatherName = "Rajesh Verma";
        ws.Personal.PanNumber = "abcde1234f";
        ws.Personal.AadhaarNumber = "1234 5678 9012";
        ws.Personal.PermanentAddress.Line1 = "42 Sector 17";
        ws.Personal.PermanentAddress.Pincode = "160017";
        ws.Personal.SameAsPermanentAddress = true;

        ws.Education.Add(new EmployeeEducationItemDto
        {
            QualificationCode = "BACHELORS",
            EducationLevelCode = "BCA",
            Specialisation = "Computer Applications",
            Institute = "ABC University",
            PassingYear = 2022,
            Cgpa = 8.1m,
            GradeLetter = "A+",
            Percentage = 70m,
            CertificationName = "AWS Cloud Practitioner",
            CertificationValidTill = new DateOnly(2028, 6, 1)
        });

        ws.PreviousCompanies.Add(new EmployeePreviousCompanyItemDto
        {
            CompanyName = "TechCorp",
            Designation = "Junior Developer",
            JoinedOn = new DateOnly(2023, 1, 10),
            LeftOn = new DateOnly(2025, 12, 31),
            EmploymentType = "Permanent",
            LastAnnualCtc = 600000m
        });

        ws.Bank.AccountHolderName = "Riya Verma";
        ws.Bank.BankName = "HDFC Bank";
        ws.Bank.AccountNumber = "123456789012";
        ws.Bank.Ifsc = "hdfc0001234";
        ws.Bank.TransferSalarySlipBy = "Email";
        ws.Bank.SalaryFormat = "NEFT";

        ws.PfEsi.PfApplicable = true;
        ws.PfEsi.PfScheme = "Govt";
        ws.PfEsi.PfNumber = "PB/CHD/12345";
        ws.PfEsi.UanNumber = "100200300400";
        ws.PfEsi.EsiApplicable = true;
        ws.PfEsi.EsiNumber = "31001234560001001";

        ws.Salary.GradeCode = "G1";
        ws.Salary.BasicSalary = 30000m;
        ws.Salary.Components.Add(new EmployeeSalaryAllowanceRowDto
        {
            Code = "HRA",
            Name = "HRA",
            Kind = SalaryComponentKinds.Allowance,
            IsPercentage = false,
            Amount = 15000m,
            OnBasicOrGross = "Basic"
        });
        ws.Salary.Components.Add(new EmployeeSalaryAllowanceRowDto
        {
            Code = "EPF",
            Name = "EPF",
            Kind = SalaryComponentKinds.Deduction,
            IsPercentage = true,
            Amount = 12m,
            OnBasicOrGross = "Basic"
        });

        var id = await service.SaveWorkspaceAsync(HrCaller, ws, asDraft: false);
        var saved = repo.LastSaved!;

        Assert.Equal("1", id);
        Assert.False(saved.IsDraft);
        Assert.True(saved.IsActive);
        Assert.Equal("+919876543210", saved.Personal.Mobile);
        Assert.Equal("ABCDE1234F", saved.Personal.PanNumber);
        Assert.Equal("9012", saved.Personal.AadhaarLast4);
        Assert.StartsWith("ENC:EmployeeStatutory.Aadhaar:", saved.AadhaarProtected);
        Assert.Equal("9012", saved.BankAccountLast4);
        Assert.StartsWith("ENC:EmployeeBankAccount.AccountNumber:", saved.BankAccountNumberProtected);
        Assert.Equal("HDFC0001234", saved.Bank.Ifsc);
        Assert.Equal(15000m, saved.Salary.Components[0].CalculatedAmount);
        Assert.Equal(3600m, saved.Salary.Components[1].CalculatedAmount);
    }

    [Fact]
    public async Task Submit_rejects_invalid_pan_or_missing_mandatory_fields()
    {
        var service = BuildService(new FakeWorkspaceRepository());
        var ws = new EmployeeWorkspaceDto();
        ws.Personal.FirstName = "Karan";

        var missingLast = await Assert.ThrowsAsync<MobileValidationException>(
            () => service.SaveWorkspaceAsync(HrCaller, ws, asDraft: false));
        Assert.Equal("LAST_NAME_REQUIRED", missingLast.Code);

        ws.Personal.LastName = "Gill";
        ws.Personal.Email = "karan@kapsons.com";
        ws.Personal.Mobile = "9811122233";
        ws.Personal.DateOfBirth = new DateOnly(1995, 3, 20);
        ws.Personal.GenderCode = "MALE";
        ws.Personal.DepartmentCode = "IT";
        ws.Personal.DesignationCode = "DES-1";
        ws.Personal.PanNumber = "BADPAN";

        var badPan = await Assert.ThrowsAsync<MobileValidationException>(
            () => service.SaveWorkspaceAsync(HrCaller, ws, asDraft: false));
        Assert.Equal("PAN_INVALID", badPan.Code);
    }

    private static EmployeeWorkspaceService BuildService(FakeWorkspaceRepository repo) =>
        new(
            repo,
            new FakeMasters(),
            new FakeDesignations(),
            new FakeGradeSalaries(),
            new FakeMediaRepo(),
            new FakeMediaStorage(),
            new FakeProtector());

    private sealed class FakeProtector : ISensitiveValueProtector
    {
        public string Protect(string purpose, string value) => $"ENC:{purpose}:{value}";
        public string Unprotect(string purpose, string protectedValue) => protectedValue.Split(':')[^1];
    }

    private sealed class FakeWorkspaceRepository : IEmployeeWorkspaceRepository
    {
        private readonly List<EmployeeWorkspaceAggregate> _items = [];
        public EmployeeWorkspaceAggregate? LastSaved { get; private set; }

        public Task<IReadOnlyList<EmployeeExplorerItemRecord>> ListExplorerAsync(string tenantKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EmployeeExplorerItemRecord>>(_items.Select(i => new EmployeeExplorerItemRecord(
                i.EmployeeId, i.Personal.EmployeeNumber, i.Personal.FirstName, i.Personal.MiddleName, i.Personal.LastName,
                null, i.Personal.AvatarUrl, i.Personal.JobTitle, i.Personal.DepartmentCode, i.Personal.SubDepartmentCode,
                i.Personal.DivisionCode, i.Personal.LocationCode, i.Salary.GradeCode, i.Personal.JoinedOn,
                i.Personal.Email, i.Personal.Mobile, null, i.EmploymentStatusCode, i.IsActive, i.IsDraft)).ToList());

        public Task<IReadOnlyList<string>> GetAllEmployeeNumbersAsync(string tenantKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(_items.Select(i => i.Personal.EmployeeNumber).ToList());

        public Task<EmployeeWorkspaceAggregate?> GetAsync(string tenantKey, long employeeId, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(i => i.EmployeeId == employeeId));

        public Task<string?> FindConflictAsync(
            string tenantKey, long? excludeEmployeeId, string employeeNumber, string? email, string? normalizedPhone, string? panNumber, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task<long> SaveAsync(EmployeeWorkspaceAggregate aggregate, CancellationToken ct = default)
        {
            if (aggregate.EmployeeId <= 0)
            {
                aggregate.EmployeeId = _items.Count + 1;
                _items.Add(aggregate);
            }
            LastSaved = aggregate;
            return Task.FromResult(aggregate.EmployeeId);
        }
    }

    private sealed class FakeMasters : IMasterOptionRepository
    {
        public Task<IReadOnlyList<MasterOptionRecord>> GetAllAsync(string tenantKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MasterOptionRecord>>([
                new MasterOptionRecord { Id = 1, TenantKey = tenantKey, Category = MasterCategories.Location, Code = "HQ", Name = "Head Office", IsActive = true },
                new MasterOptionRecord { Id = 2, TenantKey = tenantKey, Category = MasterCategories.Department, Code = "IT", Name = "Information Technology", IsActive = true }
            ]);

        public Task<IReadOnlyList<MasterOptionRecord>> GetActiveAsync(string tenantKey, string category, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MasterOptionRecord>>([]);

        public Task<long> UpsertAsync(MasterOptionRecord option, CancellationToken ct = default) => Task.FromResult(1L);
    }

    private sealed class FakeDesignations : IDesignationService
    {
        public Task<IReadOnlyList<DesignationDto>> GetAllAsync(string tenantKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DesignationDto>>([
                new DesignationDto("1", "DES-1", "Software Engineer", "G1", "Grade 1", "IT", "Information Technology", null, 1, true, DateTimeOffset.UtcNow)
            ]);

        public Task<string> SaveAsync(string tenantKey, SaveDesignationRequest request, CancellationToken ct = default) => Task.FromResult("1");
        public Task<bool> DeleteAsync(string tenantKey, string designationId, CancellationToken ct = default) => Task.FromResult(true);
        public Task ReorderAsync(string tenantKey, IReadOnlyList<string> orderedIds, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeGradeSalaries : IGradeSalaryService
    {
        public Task<IReadOnlyList<GradeSalaryStructureDto>> GetAllAsync(string tenantKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GradeSalaryStructureDto>>([]);
        public Task SaveAsync(string tenantKey, GradeSalaryStructureDto structure, CancellationToken ct = default) => Task.CompletedTask;
        public Task<SalaryBreakdownDto> CalculateAsync(string tenantKey, GradeSalaryStructureDto structure, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeMediaRepo : IMediaAssetRepository
    {
        public Task<IReadOnlyList<MediaAssetRecord>> GetAllAsync(string tenantKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MediaAssetRecord>>([]);
        public Task<MediaAssetRecord?> GetByIdAsync(long id, CancellationToken ct = default) => Task.FromResult<MediaAssetRecord?>(null);
        public Task<long> AddAsync(MediaAssetRecord record, CancellationToken ct = default) => Task.FromResult(100L);
    }

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public Task<string> SaveAsync(string tenantKey, string storedFileName, Stream content, CancellationToken ct = default) =>
            Task.FromResult($"{tenantKey}/{storedFileName}");
        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default) => Task.FromResult<Stream>(Stream.Null);
        public Task DeleteAsync(string storagePath, CancellationToken ct = default) => Task.CompletedTask;
    }
}
