using System.Globalization;
using System.Text.RegularExpressions;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.VO.Admin;
using Vispl.HRManagement.VO.Employee;

namespace Vispl.HRManagement.BM.Admin;

public sealed class EmployeeWorkspaceService(
    IEmployeeWorkspaceRepository repository,
    IMasterOptionRepository masters,
    IDesignationService designations,
    IGradeSalaryService gradeSalaries,
    IMediaAssetRepository mediaAssets,
    IMediaStorage mediaStorage,
    ISensitiveValueProtector protector) : IEmployeeWorkspaceService
{
    private const string ViewPermission = "EMPLOYEES.VIEW";
    private const string CreatePermission = "EMPLOYEES.CREATE";
    private const string ManageEmploymentPermission = "EMPLOYEES.EMPLOYMENT_MANAGE";
    private const string ManageOrganisationPermission = "EMPLOYEES.ORGANISATION_MANAGE";

    private const string BankAccountPurpose = "EmployeeBankAccount.AccountNumber";
    private const string AadhaarPurpose = "EmployeeStatutory.Aadhaar";

    private static readonly Regex PanRegex = new("^[A-Z]{5}[0-9]{4}[A-Z]$", RegexOptions.Compiled);
    private static readonly Regex IfscRegex = new("^[A-Z]{4}0[A-Z0-9]{6}$", RegexOptions.Compiled);
    private static readonly Regex DigitsOnlyRegex = new("^[0-9]+$", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> ImageExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif"
        };

    private static readonly IReadOnlyDictionary<string, string> DocumentExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = ".pdf",
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["image/webp"] = ".webp",
            ["application/msword"] = ".doc",
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx"
        };

    public async Task<IReadOnlyList<EmployeeExplorerRowDto>> GetExplorerAsync(
        MobileCallerContext caller, EmployeeExplorerQuery query, CancellationToken cancellationToken = default)
    {
        if (!caller.Has(ViewPermission) && !caller.Has(CreatePermission))
        {
            throw new MobileAccessDeniedException(ViewPermission);
        }

        var items = await repository.ListExplorerAsync(caller.TenantKey, cancellationToken);
        var options = await masters.GetAllAsync(caller.TenantKey, cancellationToken);

        var deptNames = NamesFor(options, MasterCategories.Department);
        var divNames = NamesFor(options, MasterCategories.Division);
        var locNames = NamesFor(options, MasterCategories.Location);
        var gradeNames = NamesFor(options, MasterCategories.Grade);

        var byId = items.ToDictionary(item => item.Id);
        var rows = items.Select(item =>
        {
            var displayName = string.IsNullOrWhiteSpace(item.PreferredName)
                ? $"{item.FirstName} {item.LastName}".Trim()
                : item.PreferredName!;

            var managerName = item.ManagerEmployeeId is { } mgrId && byId.TryGetValue(mgrId, out var mgr)
                ? $"{mgr.FirstName} {mgr.LastName}".Trim()
                : null;

            var (statusLabel, statusTone) = ResolveStatus(item);
            var divisionCode = item.DivisionCode ?? string.Empty;

            return new EmployeeExplorerRowDto(
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.EmployeeNumber,
                string.IsNullOrWhiteSpace(displayName) ? item.EmployeeNumber : displayName,
                item.AvatarUrl,
                item.JobTitle,
                item.DepartmentCode,
                NameOr(deptNames, item.DepartmentCode, "Unassigned"),
                string.IsNullOrWhiteSpace(item.SubDepartmentCode) ? null : NameOr(deptNames, item.SubDepartmentCode, item.SubDepartmentCode),
                divisionCode,
                NameOr(divNames, divisionCode, "Unassigned"),
                NameOr(locNames, item.LocationCode, "—"),
                item.GradeCode,
                item.GradeCode is null ? null : NameOr(gradeNames, item.GradeCode, item.GradeCode),
                item.JoinedOn,
                item.Email,
                item.Phone,
                managerName,
                statusLabel,
                statusTone,
                item.IsActive,
                item.IsDraft);
        });

        var search = query.Search?.Trim();

        return rows
            .Where(row => string.IsNullOrWhiteSpace(query.DivisionCode) || Same(row.DivisionCode, query.DivisionCode))
            .Where(row => string.IsNullOrWhiteSpace(query.DepartmentCode) || Same(row.DepartmentCode, query.DepartmentCode))
            .Where(row => MatchesStatus(query.Status, row))
            .Where(row => string.IsNullOrWhiteSpace(search) ||
                          row.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                          row.EmployeeNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                          (row.Designation?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                          (row.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                          (row.Phone?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
    }

    public async Task<EmployeeWorkspaceDto> GetNewTemplateAsync(
        MobileCallerContext caller, CancellationToken cancellationToken = default)
    {
        if (!caller.Has(CreatePermission) && !caller.Has(ViewPermission))
        {
            throw new MobileAccessDeniedException(CreatePermission);
        }

        var numbers = await repository.GetAllEmployeeNumbersAsync(caller.TenantKey, cancellationToken);
        var options = await masters.GetAllAsync(caller.TenantKey, cancellationToken);
        var defaultLocation = options
            .Where(o => o.Category == MasterCategories.Location && o.IsActive)
            .OrderBy(o => o.SortOrder)
            .Select(o => o.Code)
            .FirstOrDefault() ?? string.Empty;

        return new EmployeeWorkspaceDto
        {
            Personal = new EmployeePersonalWorkspaceDto
            {
                EmployeeNumber = SuggestEmployeeNumber(numbers),
                JoinedOn = DateOnly.FromDateTime(DateTime.Today),
                LocationCode = defaultLocation
            },
            Documents = [new EmployeeDocumentItemDto()]
        };
    }

    public async Task<EmployeeWorkspaceDto?> GetWorkspaceAsync(
        MobileCallerContext caller, string employeeId, CancellationToken cancellationToken = default)
    {
        if (!caller.Has(ViewPermission) && !caller.Has(CreatePermission))
        {
            throw new MobileAccessDeniedException(ViewPermission);
        }

        if (!long.TryParse(employeeId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
        {
            return null;
        }

        var agg = await repository.GetAsync(caller.TenantKey, parsedId, cancellationToken);
        if (agg is null)
        {
            return null;
        }

        var allDesignations = await designations.GetAllAsync(caller.TenantKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(agg.Personal.JobTitle) && string.IsNullOrWhiteSpace(agg.Personal.DesignationCode))
        {
            agg.Personal.DesignationCode = allDesignations
                .FirstOrDefault(d => string.Equals(d.Name, agg.Personal.JobTitle, StringComparison.OrdinalIgnoreCase))
                ?.Code;
        }

        // Hydrate Salary Details from the Grade Salary master when no employee-specific components were saved yet.
        if (agg.Salary.BasicSalary == 0m && agg.Salary.Components.Count == 0 && !string.IsNullOrWhiteSpace(agg.Salary.GradeCode))
        {
            var structures = await gradeSalaries.GetAllAsync(caller.TenantKey, cancellationToken);
            var grade = structures.FirstOrDefault(g => Same(g.GradeCode, agg.Salary.GradeCode));
            if (grade is not null && grade.IsConfigured)
            {
                agg.Salary.BasicSalary = grade.BasicMonthly;
                agg.Salary.Components = MapGradeComponents(grade);
            }
        }

        if (agg.Documents.Count == 0)
        {
            agg.Documents.Add(new EmployeeDocumentItemDto());
        }

        return new EmployeeWorkspaceDto
        {
            EmployeeId = agg.EmployeeId.ToString(CultureInfo.InvariantCulture),
            IsDraft = agg.IsDraft,
            Personal = agg.Personal,
            Education = agg.Education,
            PreviousCompanies = agg.PreviousCompanies,
            Bank = agg.Bank,
            PfEsi = agg.PfEsi,
            Salary = agg.Salary,
            Documents = agg.Documents,
            Leaving = agg.Leaving
        };
    }

    public async Task<string> SaveWorkspaceAsync(
        MobileCallerContext caller,
        EmployeeWorkspaceDto workspace,
        bool asDraft,
        CancellationToken cancellationToken = default)
    {
        long? existingId = long.TryParse(workspace.EmployeeId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId) && parsedId > 0
            ? parsedId
            : null;

        if (existingId is null)
        {
            if (!caller.Has(CreatePermission))
            {
                throw new MobileAccessDeniedException(CreatePermission);
            }
        }
        else if (!caller.Has(CreatePermission) && !caller.Has(ManageEmploymentPermission) && !caller.Has(ManageOrganisationPermission))
        {
            throw new MobileAccessDeniedException(ManageEmploymentPermission);
        }

        var p = workspace.Personal;
        var firstName = p.FirstName.Trim();
        var lastName = p.LastName.Trim();

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new MobileValidationException("FIRST_NAME_REQUIRED", "First Name is required.");
        }

        if (!asDraft)
        {
            if (string.IsNullOrWhiteSpace(lastName))
            {
                throw new MobileValidationException("LAST_NAME_REQUIRED", "Last Name is required to submit the employee.");
            }
            if (string.IsNullOrWhiteSpace(p.Email))
            {
                throw new MobileValidationException("EMAIL_REQUIRED", "Email is required to submit the employee.");
            }
            if (string.IsNullOrWhiteSpace(p.Mobile))
            {
                throw new MobileValidationException("MOBILE_REQUIRED", "Mobile number is required to submit the employee.");
            }
            if (p.DateOfBirth is null)
            {
                throw new MobileValidationException("DOB_REQUIRED", "Date of Birth is required to submit the employee.");
            }
            if (string.IsNullOrWhiteSpace(p.GenderCode))
            {
                throw new MobileValidationException("GENDER_REQUIRED", "Gender is required to submit the employee.");
            }
            if (string.IsNullOrWhiteSpace(p.DepartmentCode))
            {
                throw new MobileValidationException("DEPARTMENT_REQUIRED", "Department is required to submit the employee.");
            }
            if (string.IsNullOrWhiteSpace(p.DesignationCode) && string.IsNullOrWhiteSpace(p.JobTitle))
            {
                throw new MobileValidationException("DESIGNATION_REQUIRED", "Designation is required to submit the employee.");
            }
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            lastName = "—";
        }

        if (p.DateOfBirth is { } dob && dob >= DateOnly.FromDateTime(DateTime.Today))
        {
            throw new MobileValidationException("DOB_INVALID", "Date of Birth must be in the past.");
        }

        var numbers = await repository.GetAllEmployeeNumbersAsync(caller.TenantKey, cancellationToken);
        var employeeNumber = string.IsNullOrWhiteSpace(p.EmployeeNumber)
            ? SuggestEmployeeNumber(numbers)
            : p.EmployeeNumber.Trim();

        var email = string.IsNullOrWhiteSpace(p.Email) ? null : p.Email.Trim();
        if (email is not null && !email.Contains('@'))
        {
            throw new MobileValidationException("EMAIL_INVALID", "Enter a valid email address.");
        }

        string? normalizedPhone = null;
        if (!string.IsNullOrWhiteSpace(p.Mobile))
        {
            try
            {
                normalizedPhone = EmployeePhone.Normalize(p.Mobile);
            }
            catch (FormatException ex)
            {
                throw new MobileValidationException("PHONE_INVALID", ex.Message);
            }
        }

        var pan = string.IsNullOrWhiteSpace(p.PanNumber) ? null : p.PanNumber.Trim().ToUpperInvariant();
        if (pan is not null && !PanRegex.IsMatch(pan))
        {
            throw new MobileValidationException("PAN_INVALID", "PAN must match 5 letters, 4 digits, and 1 letter (for example ABCDE1234F).");
        }

        string? aadhaarProtected = null;
        var aadhaarLast4 = p.AadhaarLast4;
        if (!string.IsNullOrWhiteSpace(p.AadhaarNumber))
        {
            var aadhaarDigits = new string(p.AadhaarNumber.Where(char.IsAsciiDigit).ToArray());
            if (aadhaarDigits.Length != 12)
            {
                throw new MobileValidationException("AADHAAR_INVALID", "Aadhaar number must be 12 digits.");
            }
            aadhaarProtected = protector.Protect(AadhaarPurpose, aadhaarDigits);
            aadhaarLast4 = aadhaarDigits[^4..];
        }

        ValidateAddress("Permanent address", p.PermanentAddress);
        if (!p.SameAsPermanentAddress)
        {
            ValidateAddress("Current address", p.CurrentAddress);
        }

        // PF / ESI format checks
        var pfEsi = workspace.PfEsi;
        if (pfEsi.PfApplicable && !string.IsNullOrWhiteSpace(pfEsi.UanNumber))
        {
            var uan = new string(pfEsi.UanNumber.Where(char.IsAsciiDigit).ToArray());
            if (uan.Length != 12)
            {
                throw new MobileValidationException("UAN_INVALID", "UAN must be exactly 12 digits.");
            }
            pfEsi.UanNumber = uan;
        }
        if (pfEsi.EsiApplicable && !string.IsNullOrWhiteSpace(pfEsi.EsiNumber))
        {
            var esi = new string(pfEsi.EsiNumber.Where(char.IsAsciiDigit).ToArray());
            if (esi.Length != 17)
            {
                throw new MobileValidationException("ESI_INVALID", "ESI Number must be exactly 17 digits.");
            }
            pfEsi.EsiNumber = esi;
        }

        // Education checks
        foreach (var edu in workspace.Education.Where(e => !string.IsNullOrWhiteSpace(e.Institute)))
        {
            if (edu.PassingYear is < 1950 or > 2100)
            {
                throw new MobileValidationException("EDU_YEAR_INVALID", $"Passing year {edu.PassingYear} must be between 1950 and 2100.");
            }
            if (edu.Cgpa is < 0m or > 10m)
            {
                throw new MobileValidationException("EDU_CGPA_INVALID", "CGPA must be between 0 and 10.");
            }
            if (edu.Percentage is < 0m or > 100m)
            {
                throw new MobileValidationException("EDU_PCT_INVALID", "Percentage must be between 0 and 100.");
            }
        }

        // Previous Company checks
        foreach (var prev in workspace.PreviousCompanies.Where(c => !string.IsNullOrWhiteSpace(c.CompanyName)))
        {
            if (prev.LeftOn < prev.JoinedOn)
            {
                throw new MobileValidationException("PREV_DATES_INVALID", $"Previous company '{prev.CompanyName}': leaving date cannot be before joining date.");
            }
            if (prev.LastAnnualCtc is < 0m)
            {
                throw new MobileValidationException("PREV_CTC_INVALID", "Last Annual CTC cannot be negative.");
            }
        }

        // Bank checks
        string? bankProtected = null;
        string? bankLast4 = null;
        var bank = workspace.Bank;
        var hasAnyBankInput = !string.IsNullOrWhiteSpace(bank.BankName) ||
                              !string.IsNullOrWhiteSpace(bank.AccountNumber) ||
                              !string.IsNullOrWhiteSpace(bank.Ifsc);

        if (hasAnyBankInput)
        {
            if (!string.IsNullOrWhiteSpace(bank.AccountNumber))
            {
                var accDigits = new string(bank.AccountNumber.Where(char.IsAsciiDigit).ToArray());
                if (accDigits.Length is < 9 or > 18)
                {
                    throw new MobileValidationException("BANK_ACCOUNT_INVALID", "Bank account number must be 9 to 18 digits.");
                }
                bankProtected = protector.Protect(BankAccountPurpose, accDigits);
                bankLast4 = accDigits[^4..];
            }
            else if (string.IsNullOrWhiteSpace(bank.MaskedAccountNumber))
            {
                throw new MobileValidationException("BANK_ACCOUNT_REQUIRED", "Enter the bank account number, or clear Bank Name and IFSC.");
            }

            if (string.IsNullOrWhiteSpace(bank.BankName))
            {
                throw new MobileValidationException("BANK_NAME_REQUIRED", "Bank Name is required when saving bank details.");
            }

            var ifsc = bank.Ifsc?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!IfscRegex.IsMatch(ifsc))
            {
                throw new MobileValidationException("IFSC_INVALID", "IFSC Code must match 4 letters + '0' + 6 characters (for example HDFC0001234).");
            }
            bank.Ifsc = ifsc;
        }

        var conflict = await repository.FindConflictAsync(
            caller.TenantKey, existingId, employeeNumber, email, normalizedPhone, pan, cancellationToken);
        if (conflict is not null)
        {
            throw new MobileValidationException("DUPLICATE_EMPLOYEE", conflict);
        }

        // Resolve Designation -> JobTitle + default Grade if needed
        var allDesignations = await designations.GetAllAsync(caller.TenantKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(p.DesignationCode))
        {
            var matchedDesignation = allDesignations.FirstOrDefault(d => Same(d.Code, p.DesignationCode));
            if (matchedDesignation is not null)
            {
                p.JobTitle = matchedDesignation.Name;
                if (string.IsNullOrWhiteSpace(workspace.Salary.GradeCode))
                {
                    workspace.Salary.GradeCode = matchedDesignation.GradeCode;
                }
            }
        }

        var options = await masters.GetAllAsync(caller.TenantKey, cancellationToken);
        var locationCode = string.IsNullOrWhiteSpace(p.LocationCode)
            ? options.Where(o => o.Category == MasterCategories.Location && o.IsActive).Select(o => o.Code).FirstOrDefault() ?? "HQ"
            : p.LocationCode.Trim();

        // Status & Leaving resolution
        bool isActive;
        string statusCode;
        DateOnly? leavingOn = null;

        if (asDraft)
        {
            isActive = false;
            statusCode = "Draft";
        }
        else if (workspace.Leaving.HasLeftOrganization || workspace.Leaving.IsDismissed)
        {
            isActive = false;
            statusCode = "Exited";
            var rawLeaving = workspace.Leaving.DismissDate
                             ?? workspace.Leaving.FinalSettlementDate
                             ?? workspace.Leaving.ResignationSubmitDate
                             ?? p.JoinedOn;
            leavingOn = rawLeaving < p.JoinedOn ? p.JoinedOn : rawLeaving;
        }
        else
        {
            isActive = true;
            statusCode = "Active";
        }

        // Recompute component CalculatedAmount cleanly
        workspace.Salary.Recalculate();

        var aggregate = new EmployeeWorkspaceAggregate
        {
            EmployeeId = existingId ?? 0,
            TenantKey = caller.TenantKey,
            IsDraft = asDraft,
            IsActive = isActive,
            EmploymentStatusCode = statusCode,
            LeavingOn = leavingOn,
            AadhaarProtected = aadhaarProtected,
            Personal = new EmployeePersonalWorkspaceDto
            {
                EmployeeNumber = employeeNumber,
                FirstName = firstName,
                MiddleName = p.MiddleName,
                LastName = lastName,
                Email = email ?? string.Empty,
                Mobile = normalizedPhone ?? string.Empty,
                DateOfBirth = p.DateOfBirth,
                JoinedOn = p.JoinedOn,
                GenderCode = p.GenderCode,
                AvatarUrl = p.AvatarUrl,
                DepartmentCode = string.IsNullOrWhiteSpace(p.DepartmentCode) ? "UNASSIGNED" : p.DepartmentCode,
                SubDepartmentCode = p.SubDepartmentCode,
                DesignationCode = p.DesignationCode,
                JobTitle = p.JobTitle,
                DivisionCode = p.DivisionCode,
                LocationCode = locationCode,
                ReportingManagerId = p.ReportingManagerId,
                MaritalStatusCode = p.MaritalStatusCode,
                BloodGroupCode = p.BloodGroupCode,
                ReligionCode = p.ReligionCode,
                OtherReligion = p.OtherReligion,
                NationalityCode = p.NationalityCode,
                FatherName = p.FatherName,
                FatherOccupationCode = p.FatherOccupationCode,
                MotherName = p.MotherName,
                MotherOccupationCode = p.MotherOccupationCode,
                SpouseName = p.SpouseName,
                SpouseOccupationCode = p.SpouseOccupationCode,
                PanNumber = pan,
                AadhaarLast4 = aadhaarLast4,
                VoterId = p.VoterId,
                PermanentAddress = p.PermanentAddress,
                CurrentAddress = p.CurrentAddress,
                SameAsPermanentAddress = p.SameAsPermanentAddress
            },
            Education = workspace.Education,
            PreviousCompanies = workspace.PreviousCompanies,
            Bank = bank,
            BankAccountNumberProtected = bankProtected,
            BankAccountLast4 = bankLast4,
            PfEsi = pfEsi,
            Salary = workspace.Salary,
            Documents = workspace.Documents,
            Leaving = workspace.Leaving,
            UpdatedBy = caller.UserId,
            UpdatedByName = caller.UserId
        };

        var savedId = await repository.SaveAsync(aggregate, cancellationToken);
        return savedId.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<UploadedEmployeeMediaDto> UploadMediaAsync(
        MobileCallerContext caller,
        string originalFileName,
        string contentType,
        long length,
        Stream content,
        bool imageOnly,
        CancellationToken cancellationToken = default)
    {
        if (!caller.Has(CreatePermission) && !caller.Has(ManageEmploymentPermission) && !caller.Has(ViewPermission))
        {
            throw new MobileAccessDeniedException(CreatePermission);
        }

        var maxBytes = imageOnly ? 5L * 1024 * 1024 : 10L * 1024 * 1024;
        if (length <= 0 || length > maxBytes)
        {
            throw new MobileValidationException("FILE_TOO_LARGE",
                imageOnly ? "Profile photo must be under 5 MB." : "Document file must be under 10 MB.");
        }

        var allowed = imageOnly ? ImageExtensions : DocumentExtensions;
        var extension = allowed.TryGetValue(contentType, out var mapped)
            ? mapped
            : ResolveByFileName(originalFileName, imageOnly);

        if (extension is null)
        {
            throw new MobileValidationException("FILE_TYPE_UNSUPPORTED",
                imageOnly
                    ? "Supported photo formats: PNG, JPG, WEBP or GIF."
                    : "Supported document formats: PDF, PNG, JPG, WEBP, DOC or DOCX.");
        }

        var safeContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        var tenant = caller.TenantKey.Trim().ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var storagePath = await mediaStorage.SaveAsync(tenant, storedFileName, content, cancellationToken);

        var safeOriginal = Path.GetFileName(originalFileName);
        var record = new MediaAssetRecord
        {
            TenantKey = tenant,
            OriginalFileName = string.IsNullOrWhiteSpace(safeOriginal) ? storedFileName : safeOriginal,
            StoredFileName = storedFileName,
            StoragePath = storagePath,
            ContentType = safeContentType,
            Length = length,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        var id = await mediaAssets.AddAsync(record, cancellationToken);

        return new UploadedEmployeeMediaDto(id, record.OriginalFileName, $"/media/{id}");
    }

    public static void RecalculateSalaryRows(EmployeeSalaryWorkspaceDto salary)
    {
        var basic = Math.Max(0m, salary.BasicSalary);

        // First compute allowances so Gross is known for PercentOfGross deductions.
        decimal gross = basic;
        foreach (var row in salary.Components.Where(c => c.Kind == SalaryComponentKinds.Allowance))
        {
            row.CalculatedAmount = ComputeRowAmount(row, basic, basic);
            gross += row.CalculatedAmount;
        }

        foreach (var row in salary.Components.Where(c => c.Kind != SalaryComponentKinds.Allowance))
        {
            row.CalculatedAmount = ComputeRowAmount(row, basic, gross);
        }
    }

    private static decimal ComputeRowAmount(EmployeeSalaryAllowanceRowDto row, decimal basic, decimal gross)
    {
        if (!row.IsPercentage)
        {
            return Math.Round(Math.Max(0m, row.Amount), 2);
        }

        var baseAmount = row.OnBasicOrGross switch
        {
            "Gross" => gross,
            "On Amount" => row.CalculatedAmount > 0 ? row.CalculatedAmount : basic,
            _ => basic
        };

        if (string.Equals(row.CalculationOn, "Amount", StringComparison.OrdinalIgnoreCase) && row.CalculatedAmount > 0)
        {
            return Math.Round(row.CalculatedAmount * row.Amount / 100m, 2);
        }

        return Math.Round(baseAmount * row.Amount / 100m, 2);
    }

    private static List<EmployeeSalaryAllowanceRowDto> MapGradeComponents(GradeSalaryStructureDto grade)
    {
        var basic = grade.BasicMonthly;
        var list = grade.Components.Select(c =>
        {
            var isPct = c.CalculationType != SalaryCalculationTypes.Fixed;
            var onBasis = c.CalculationType == SalaryCalculationTypes.PercentOfGross ? "Gross" : "Basic";
            return new EmployeeSalaryAllowanceRowDto
            {
                Code = c.ComponentCode,
                Name = c.ComponentName ?? c.ComponentCode,
                Kind = c.Kind,
                IsPercentage = isPct,
                Amount = c.Value,
                OnBasicOrGross = onBasis,
                VaryOnDays = true,
                CalculationOn = "Salary",
                CalculatedAmount = isPct ? Math.Round(basic * c.Value / 100m, 2) : c.Value
            };
        }).ToList();

        return list;
    }

    private static void ValidateAddress(string label, EmployeeAddressItemDto addr)
    {
        if (!string.IsNullOrWhiteSpace(addr.Pincode))
        {
            var pin = addr.Pincode.Trim();
            if (!DigitsOnlyRegex.IsMatch(pin) || pin.Length is < 4 or > 10)
            {
                throw new MobileValidationException("PINCODE_INVALID", $"{label}: Pincode must be 4 to 10 digits.");
            }
            addr.Pincode = pin;
        }
    }

    private static string? ResolveByFileName(string fileName, bool imageOnly)
    {
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext)) return null;
        if (imageOnly)
        {
            return ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" ? (ext == ".jpeg" ? ".jpg" : ext) : null;
        }
        return ext is ".pdf" or ".png" or ".jpg" or ".jpeg" or ".webp" or ".doc" or ".docx"
            ? (ext == ".jpeg" ? ".jpg" : ext)
            : null;
    }

    private static string SuggestEmployeeNumber(IReadOnlyList<string> numbers)
    {
        var best = numbers
            .Select(num => Regex.Match(num, @"^([A-Za-z]+-)(\d+)$"))
            .Where(match => match.Success)
            .Select(match => (Prefix: match.Groups[1].Value, Digits: match.Groups[2].Value, Value: long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)))
            .OrderByDescending(item => item.Value)
            .FirstOrDefault();

        return best.Prefix is null
            ? "EMP-0001"
            : best.Prefix + (best.Value + 1).ToString(CultureInfo.InvariantCulture).PadLeft(best.Digits.Length, '0');
    }

    private static (string Label, string Tone) ResolveStatus(EmployeeExplorerItemRecord item)
    {
        if (item.IsDraft || Same(item.EmploymentStatusCode, "Draft"))
        {
            return ("Draft", "warning");
        }
        if (!item.IsActive || Same(item.EmploymentStatusCode, "Exited"))
        {
            return ("Exited", "neutral");
        }
        if (Same(item.EmploymentStatusCode, "OnLeave") || Same(item.EmploymentStatusCode, "On Leave"))
        {
            return ("On Leave", "info");
        }
        return ("Active", "success");
    }

    private static bool MatchesStatus(string? filter, EmployeeExplorerRowDto row)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return filter switch
        {
            "Active" => row.IsActive && !row.IsDraft,
            "Draft" => row.IsDraft,
            "OnLeave" => Same(row.StatusLabel, "On Leave"),
            "Exited" => !row.IsActive && !row.IsDraft,
            _ => true
        };
    }

    private static Dictionary<string, string> NamesFor(IReadOnlyList<MasterOptionRecord> options, string category) =>
        options
            .Where(option => option.Category == category)
            .GroupBy(option => option.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

    private static string NameOr(IReadOnlyDictionary<string, string> names, string? code, string fallback) =>
        string.IsNullOrWhiteSpace(code) ? fallback : names.GetValueOrDefault(code) ?? code;

    private static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}
