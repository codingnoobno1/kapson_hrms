using System.Globalization;
using System.Text.RegularExpressions;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Employee;
using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.VO.Admin;
using Vispl.HRManagement.VO.Employee;

namespace Vispl.HRManagement.BM.Employee;

/// <summary>
/// Turns a completed recruitment onboarding into a full 8-section Employee Master record
/// (Personal/Family/Health/Placement, Statutory &amp; PF/ESI, Permanent &amp; Current Addresses,
/// Emergency Contact, Education, Previous Employment, Bank Account, Grade-hydrated Salary
/// Details, and Onboarding Documents).
/// </summary>
public sealed class EmployeeProvisioning(
    IEmployeeRepository employees,
    IEmployeeWorkspaceRepository workspaceRepo,
    IMasterOptionRepository masters,
    IDesignationService designations,
    IGradeSalaryService gradeSalaries,
    IMediaAssetRepository mediaAssets,
    ISensitiveValueProtector protector) : IEmployeeProvisioning
{
    private const string BankAccountPurpose = "EmployeeBankAccount.AccountNumber";
    private const string AadhaarPurpose = "EmployeeStatutory.Aadhaar";

    private static readonly Regex PanRegex = new("^[A-Z]{5}[0-9]{4}[A-Z]$", RegexOptions.Compiled);
    private static readonly Regex IfscRegex = new("^[A-Z]{4}0[A-Z0-9]{6}$", RegexOptions.Compiled);
    private static readonly Regex DigitsOnlyRegex = new("^[0-9]+$", RegexOptions.Compiled);

    public async Task<long> HireAsync(HireRequest request, CancellationToken cancellationToken = default)
    {
        // Idempotent by candidate: a replayed completion returns the employee already created.
        var existing = await employees.GetByCandidateAsync(request.TenantKey, request.CandidateId, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        string? phone = NormalizePhoneOrNull(request.Phone);

        var clashes = await employees.FindPossibleDuplicatesAsync(request.TenantKey, email, phone, null, cancellationToken);
        if (clashes.FirstOrDefault() is { } clash)
        {
            var field = email is not null && string.Equals(clash.Email, email, StringComparison.OrdinalIgnoreCase)
                ? "email"
                : "phone number";
            throw new InvalidOperationException(
                $"This candidate's {field} already belongs to employee {clash.DisplayName} ({clash.EmployeeNumber}). Each employee needs their own email and phone number.");
        }

        var now = DateTimeOffset.UtcNow;
        var tenant = request.TenantKey.Trim().ToLowerInvariant();
        var options = await masters.GetAllAsync(tenant, cancellationToken);
        var allDesignations = await designations.GetAllAsync(tenant, cancellationToken);
        var existingNumbers = await workspaceRepo.GetAllEmployeeNumbersAsync(tenant, cancellationToken);
        var employeeNumber = SuggestEmployeeNumber(existingNumbers, request.CandidateId);

        var personalForm = FormFor(request.TaskForms, "PERSONAL_DETAILS");
        var addressForm = FormFor(request.TaskForms, "ADDRESS_EMERGENCY");
        var bankForm = FormFor(request.TaskForms, "BANK_ACCOUNT");
        var eduForm = FormFor(request.TaskForms, "EDUCATION_HISTORY");
        var prevForm = FormFor(request.TaskForms, "PREVIOUS_EMPLOYMENT");
        var bgvForm = FormFor(request.TaskForms, "BACKGROUND_VERIFICATION");

        var fullName = FirstNonEmpty(GetVal(personalForm, "legalName", "fullName"), request.FullName) ?? request.FullName;
        var names = SplitName(fullName);

        var dob = ParseDate(GetVal(personalForm, "dateOfBirth", "dob"));
        if (dob is { } d && d >= DateOnly.FromDateTime(DateTime.Today))
        {
            dob = null;
        }

        var genderCode = ResolveOptionCode(options, MasterCategories.Gender, GetVal(personalForm, "gender", "genderCode"), GenderSynonyms);
        var maritalCode = ResolveOptionCode(options, MasterCategories.MaritalStatus, GetVal(personalForm, "maritalStatus", "maritalStatusCode"));
        var bloodCode = ResolveOptionCode(options, MasterCategories.BloodGroup, GetVal(personalForm, "bloodGroup", "bloodGroupCode"), BloodGroupSynonyms);
        var religionCode = ResolveOptionCode(options, MasterCategories.Religion, GetVal(personalForm, "religion", "religionCode"));
        var otherReligion = NullIfBlank(GetVal(personalForm, "otherReligion"));
        var nationalityCode = ResolveOptionCode(options, MasterCategories.Nationality, GetVal(personalForm, "nationality", "nationalityCode"), NationalitySynonyms)
                              ?? "INDIAN";

        var fatherName = NullIfBlank(GetVal(personalForm, "fatherName"));
        var fatherOccupation = ResolveOptionCode(options, MasterCategories.Occupation, GetVal(personalForm, "fatherOccupation", "fatherOccupationCode"));
        var motherName = NullIfBlank(GetVal(personalForm, "motherName"));
        var motherOccupation = ResolveOptionCode(options, MasterCategories.Occupation, GetVal(personalForm, "motherOccupation", "motherOccupationCode"));
        var spouseName = NullIfBlank(GetVal(personalForm, "spouseName"));
        var spouseOccupation = ResolveOptionCode(options, MasterCategories.Occupation, GetVal(personalForm, "spouseOccupation", "spouseOccupationCode"));

        // Organisation & Placement resolution (HR BGV form overrides -> JobOpening -> sensible master defaults)
        var deptCode = ResolveDepartmentCode(options, FirstNonEmpty(GetVal(bgvForm, "departmentCode"), request.DepartmentCode))
                       ?? "HR";
        var matchedDesignation = MatchDesignation(allDesignations, GetVal(bgvForm, "designationCode"), request.JobTitle, deptCode);
        var jobTitle = matchedDesignation?.Name ?? NullIfBlank(request.JobTitle) ?? "Associate";
        var gradeCode = ResolveOptionCode(options, MasterCategories.Grade,
                            FirstNonEmpty(GetVal(bgvForm, "gradeCode"), matchedDesignation?.GradeCode))
                        ?? matchedDesignation?.GradeCode
                        ?? "EXECUTIVE";

        var subDeptCode = ResolveSubDepartmentCode(options, deptCode, GetVal(bgvForm, "subDepartmentCode"), jobTitle);
        var divisionCode = ResolveOptionCode(options, MasterCategories.Division, GetVal(bgvForm, "divisionCode"))
                           ?? DefaultDivisionFor(deptCode);
        var locationCode = ResolveOptionCode(options, MasterCategories.Location,
                               FirstNonEmpty(GetVal(bgvForm, "locationCode"), request.LocationCode), LocationSynonyms)
                           ?? options.Where(o => o.Category == MasterCategories.Location && o.IsActive)
                               .OrderBy(o => o.SortOrder)
                               .Select(o => o.Code)
                               .FirstOrDefault()
                           ?? "CHANDIGARH";
        var employmentTypeCode = ResolveOptionCode(options, MasterCategories.JobType, request.EmploymentTypeCode, JobTypeSynonyms)
                                 ?? "FULL_TIME";
        var companyCode = deptCode switch
        {
            "DIGITAL" => "KAPSON-DIGITAL",
            "BRAND" => "KAPSON-LUXE",
            _ => "KAPSON-RETAIL"
        };

        var managerId = NullIfBlank(GetVal(bgvForm, "reportingManagerId", "managerEmployeeId"));

        // Statutory Identity (PAN, Aadhaar, Voter ID) & PF/ESI
        var rawPan = GetVal(personalForm, "panNumber", "pan")?.Trim().ToUpperInvariant();
        string? validPan = null;
        if (!string.IsNullOrWhiteSpace(rawPan) && PanRegex.IsMatch(rawPan))
        {
            var panConflict = await workspaceRepo.FindConflictAsync(tenant, null, employeeNumber, null, null, rawPan, cancellationToken);
            if (panConflict is null)
            {
                validPan = rawPan;
            }
        }

        string? aadhaarProtected = null;
        string? aadhaarLast4 = null;
        var rawAadhaar = GetVal(personalForm, "aadhaarNumber", "aadhaar");
        if (!string.IsNullOrWhiteSpace(rawAadhaar))
        {
            var digits = new string(rawAadhaar.Where(char.IsAsciiDigit).ToArray());
            if (digits.Length == 12)
            {
                aadhaarProtected = protector.Protect(AadhaarPurpose, digits);
                aadhaarLast4 = digits[^4..];
            }
        }

        var voterId = NullIfBlank(GetVal(personalForm, "voterId"));

        var pfFlagRaw = GetVal(bankForm, "pfApplicable");
        var pfApplicable = string.IsNullOrWhiteSpace(pfFlagRaw)
            ? employmentTypeCode == "FULL_TIME"
            : ParseBool(pfFlagRaw, true);
        var pfScheme = GetVal(bankForm, "pfScheme") is "Company" ? "Company" : "Govt";
        var pfNumber = NullIfBlank(GetVal(bankForm, "pfNumber"));

        string? uanNumber = null;
        var rawUan = GetVal(bankForm, "uanNumber", "uan");
        if (!string.IsNullOrWhiteSpace(rawUan))
        {
            var digits = new string(rawUan.Where(char.IsAsciiDigit).ToArray());
            if (digits.Length == 12)
            {
                uanNumber = digits;
            }
        }

        string? esiNumber = null;
        var rawEsi = GetVal(bankForm, "esiNumber", "esi");
        if (!string.IsNullOrWhiteSpace(rawEsi))
        {
            var digits = new string(rawEsi.Where(char.IsAsciiDigit).ToArray());
            if (digits.Length == 17)
            {
                esiNumber = digits;
            }
        }
        var esiApplicable = ParseBool(GetVal(bankForm, "esiApplicable"), esiNumber is not null);

        // Addresses (Permanent & Current)
        var permLine1 = FirstNonEmpty(GetVal(addressForm, "permanentLine1", "addressLine1", "address"), request.CurrentLocation);
        var permCity = ResolveOptionCode(options, MasterCategories.City,
            FirstNonEmpty(GetVal(addressForm, "permanentCity", "city"), InferCityFromText(permLine1 ?? request.CurrentLocation)), CitySynonyms);
        var permState = ResolveOptionCode(options, MasterCategories.State,
            FirstNonEmpty(GetVal(addressForm, "permanentState", "state"), InferStateFromCity(permCity)));
        var permCountry = ResolveOptionCode(options, MasterCategories.Country,
                              GetVal(addressForm, "permanentCountry", "country"), CountrySynonyms)
                          ?? (string.IsNullOrWhiteSpace(permLine1) ? null : "IN");
        var permPincode = CleanPincode(GetVal(addressForm, "permanentPincode", "pincode"));

        var permDto = new EmployeeAddressItemDto
        {
            AddressType = "Permanent",
            Line1 = permLine1 ?? string.Empty,
            Line2 = NullIfBlank(GetVal(addressForm, "permanentLine2", "addressLine2")),
            CityCode = permCity,
            StateCode = permState,
            CountryCode = permCountry,
            Pincode = permPincode
        };

        var currLine1 = NullIfBlank(GetVal(addressForm, "currentLine1"));
        var sameAsPermanent = string.IsNullOrWhiteSpace(currLine1) ||
                              ParseBool(GetVal(addressForm, "sameAsPermanent"), true);

        var currCity = ResolveOptionCode(options, MasterCategories.City, GetVal(addressForm, "currentCity"), CitySynonyms) ?? permCity;
        var currState = ResolveOptionCode(options, MasterCategories.State,
            FirstNonEmpty(GetVal(addressForm, "currentState"), InferStateFromCity(currCity))) ?? permState;
        var currCountry = ResolveOptionCode(options, MasterCategories.Country, GetVal(addressForm, "currentCountry"), CountrySynonyms) ?? permCountry;
        var currPincode = CleanPincode(GetVal(addressForm, "currentPincode")) ?? permPincode;

        var currDto = sameAsPermanent
            ? new EmployeeAddressItemDto
            {
                AddressType = "Current",
                Line1 = permDto.Line1,
                Line2 = permDto.Line2,
                CityCode = permDto.CityCode,
                StateCode = permDto.StateCode,
                CountryCode = permDto.CountryCode,
                Pincode = permDto.Pincode
            }
            : new EmployeeAddressItemDto
            {
                AddressType = "Current",
                Line1 = currLine1!,
                Line2 = NullIfBlank(GetVal(addressForm, "currentLine2")),
                CityCode = currCity,
                StateCode = currState,
                CountryCode = currCountry,
                Pincode = currPincode
            };

        // Education
        var educationList = BuildEducation(options, eduForm, dob, request.JoinedOn);

        // Previous Employment
        var prevCompanyList = BuildPreviousCompanies(options, prevForm, request.JoinedOn, request.TotalExperienceYears, request.ExpectedCtc);

        // Bank Account
        var bankName = NullIfBlank(GetVal(bankForm, "bankName", "bank"));
        var rawAccountNo = GetVal(bankForm, "accountNumber", "accountNo");
        var rawIfsc = GetVal(bankForm, "ifsc", "ifscCode")?.Trim().ToUpperInvariant();
        string? bankProtected = null;
        string? bankLast4 = null;

        if (!string.IsNullOrWhiteSpace(rawAccountNo))
        {
            var accDigits = new string(rawAccountNo.Where(char.IsAsciiDigit).ToArray());
            if (accDigits.Length is >= 9 and <= 18)
            {
                bankProtected = protector.Protect(BankAccountPurpose, accDigits);
                bankLast4 = accDigits[^4..];
            }
        }

        var validIfsc = !string.IsNullOrWhiteSpace(rawIfsc) && IfscRegex.IsMatch(rawIfsc) ? rawIfsc : null;
        var bankDto = bankName is not null && validIfsc is not null && bankProtected is not null
            ? new EmployeeBankWorkspaceDto
            {
                AccountHolderName = FirstNonEmpty(GetVal(bankForm, "accountHolderName"), fullName) ?? fullName,
                BankName = bankName,
                Ifsc = validIfsc,
                BranchName = NullIfBlank(GetVal(bankForm, "branchName", "branch")),
                AccountType = GetVal(bankForm, "accountType") is "Current" ? BankAccountTypes.Current : BankAccountTypes.Savings,
                TransferSalarySlipBy = FirstNonEmpty(GetVal(bankForm, "transferSalarySlipBy"), "Email"),
                SalaryFormat = FirstNonEmpty(GetVal(bankForm, "salaryFormat"), "NEFT")
            }
            : new EmployeeBankWorkspaceDto();

        // Salary Details hydrated from Grade Salary Structure
        var salaryDto = await BuildSalaryAsync(tenant, gradeCode, cancellationToken);

        // Documents linked from ApplicationDocuments -> MediaAssets -> EmployeeDocuments
        var documentItems = await BuildDocumentsAsync(tenant, request.Documents, validPan, aadhaarLast4, now, cancellationToken);

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var statusCode = request.JoinedOn > today.AddMonths(-3) ? "PROBATION" : "ACTIVE";

        var aggregate = new EmployeeWorkspaceAggregate
        {
            EmployeeId = 0,
            TenantKey = tenant,
            IsDraft = false,
            IsActive = true,
            EmploymentStatusCode = statusCode,
            EmploymentTypeCode = employmentTypeCode,
            CompanyCode = companyCode,
            ProbationEndsOn = request.JoinedOn.AddMonths(6),
            CostCentre = $"CC-{deptCode}",
            CandidateId = request.CandidateId,
            AadhaarProtected = aadhaarProtected,
            Personal = new EmployeePersonalWorkspaceDto
            {
                EmployeeNumber = employeeNumber,
                FirstName = names.First,
                MiddleName = names.Middle,
                LastName = names.Last,
                Email = email ?? string.Empty,
                Mobile = phone ?? string.Empty,
                DateOfBirth = dob,
                JoinedOn = request.JoinedOn,
                GenderCode = genderCode,
                DepartmentCode = deptCode,
                SubDepartmentCode = subDeptCode,
                DesignationCode = matchedDesignation?.Code,
                JobTitle = jobTitle,
                DivisionCode = divisionCode,
                LocationCode = locationCode,
                ReportingManagerId = managerId,
                MaritalStatusCode = maritalCode,
                BloodGroupCode = bloodCode,
                ReligionCode = religionCode,
                OtherReligion = otherReligion,
                NationalityCode = nationalityCode,
                FatherName = fatherName,
                FatherOccupationCode = fatherOccupation,
                MotherName = motherName,
                MotherOccupationCode = motherOccupation,
                SpouseName = spouseName,
                SpouseOccupationCode = spouseOccupation,
                PanNumber = validPan,
                AadhaarLast4 = aadhaarLast4,
                VoterId = voterId,
                PermanentAddress = permDto,
                CurrentAddress = currDto,
                SameAsPermanentAddress = sameAsPermanent
            },
            Education = educationList,
            PreviousCompanies = prevCompanyList,
            Bank = bankDto,
            BankAccountNumberProtected = bankDto.BankName.Length > 0 ? bankProtected : null,
            BankAccountLast4 = bankDto.BankName.Length > 0 ? bankLast4 : null,
            PfEsi = new EmployeePfEsiWorkspaceDto
            {
                PfApplicable = pfApplicable,
                PfJoinedOn = pfApplicable ? request.JoinedOn : null,
                PfScheme = pfScheme,
                PfNumber = pfNumber,
                UanNumber = uanNumber,
                EpfAccountCode = pfApplicable ? "EPF-CORP-01" : null,
                EsiApplicable = esiApplicable,
                EsiNumber = esiNumber,
                EsiAccountCode = esiApplicable ? "ESI-CORP-01" : null
            },
            Salary = salaryDto,
            Documents = documentItems,
            Leaving = new EmployeeLeavingWorkspaceDto(),
            UpdatedBy = "System",
            UpdatedByName = FirstNonEmpty(GetVal(bgvForm, "verifiedBy"), "Recruitment Onboarding")
        };

        var employeeId = await workspaceRepo.SaveAsync(aggregate, cancellationToken);

        // Emergency contact from ADDRESS_EMERGENCY form
        var emergencyName = NullIfBlank(GetVal(addressForm, "emergencyContact", "emergencyName"));
        if (emergencyName is not null)
        {
            var emergencyPhone = NormalizePhoneOrNull(GetVal(addressForm, "emergencyPhone"))
                                 ?? NullIfBlank(GetVal(addressForm, "emergencyPhone"));
            var emergencyRelation = FirstNonEmpty(GetVal(addressForm, "emergencyRelationship", "relationship"), "Family");
            var emergencyEmail = NullIfBlank(GetVal(addressForm, "emergencyEmail"));

            await employees.UpsertContactAsync(
                new EmployeeContactRecord(
                    0,
                    tenant,
                    employeeId,
                    "Emergency",
                    emergencyName,
                    emergencyRelation,
                    emergencyPhone,
                    emergencyEmail,
                    null,
                    true,
                    true,
                    now),
                cancellationToken);
        }

        return employeeId;
    }

    private async Task<EmployeeSalaryWorkspaceDto> BuildSalaryAsync(
        string tenant, string gradeCode, CancellationToken ct)
    {
        var dto = new EmployeeSalaryWorkspaceDto
        {
            GradeCode = gradeCode,
            ApplyMaxLimit = true,
            EpfEmployerShareRule = "Apply EPF Employer Share on Maxlimit",
            EpfOnBasicOnly = true
        };

        var structures = await gradeSalaries.GetAllAsync(tenant, ct);
        var structure = structures.FirstOrDefault(g =>
            string.Equals(g.GradeCode, gradeCode, StringComparison.OrdinalIgnoreCase));

        if (structure is not null && structure.IsConfigured)
        {
            dto.BasicSalary = structure.BasicMonthly;
            dto.Components = structure.Components.Select(c => new EmployeeSalaryAllowanceRowDto
            {
                Code = c.ComponentCode,
                Name = c.ComponentName ?? c.ComponentCode,
                Kind = c.Kind,
                IsPercentage = c.CalculationType != SalaryCalculationTypes.Fixed,
                Amount = c.Value,
                OnBasicOrGross = c.CalculationType == SalaryCalculationTypes.PercentOfGross ? "Gross" : "Basic",
                VaryOnDays = true,
                CalculationOn = "Salary"
            }).ToList();
            dto.Recalculate();
        }

        return dto;
    }

    private async Task<List<EmployeeDocumentItemDto>> BuildDocumentsAsync(
        string tenant,
        IReadOnlyList<HiredCandidateDocument>? documents,
        string? panNumber,
        string? aadhaarLast4,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var items = new List<EmployeeDocumentItemDto>();
        if (documents is null || documents.Count == 0)
        {
            return items;
        }

        foreach (var doc in documents)
        {
            if (string.IsNullOrWhiteSpace(doc.StoragePath))
            {
                continue;
            }

            var storedFile = Path.GetFileName(doc.StoragePath);
            var mediaId = await mediaAssets.AddAsync(new MediaAssetRecord
            {
                TenantKey = tenant,
                OriginalFileName = string.IsNullOrWhiteSpace(doc.FileName) ? storedFile : doc.FileName,
                StoredFileName = string.IsNullOrWhiteSpace(storedFile) ? doc.FileName : storedFile,
                StoragePath = doc.StoragePath,
                ContentType = string.IsNullOrWhiteSpace(doc.ContentType) ? "application/octet-stream" : doc.ContentType,
                Length = Math.Max(1L, doc.SizeBytes),
                CreatedUtc = doc.UploadedUtc == default ? now : doc.UploadedUtc
            }, ct);

            var (docType, docNo) = doc.CategoryCode.ToUpperInvariant() switch
            {
                "PAN" or "PAN_CARD" => ("PAN Card", panNumber),
                "AADHAAR" or "AADHAAR_ID" => ("Aadhaar Card", aadhaarLast4 is not null ? $"••••{aadhaarLast4}" : null),
                "EDUCATION_CERTIFICATES" => ("Educational Certificate", null),
                "EXPERIENCE_LETTERS" or "PREV_RELIEVING" => ("Experience / Relieving Letter", null),
                "SIGNED_OFFER" => ("Signed Offer Letter", null),
                "RESUME" => ("Resume / CV", null),
                _ => (doc.CategoryCode, null)
            };

            items.Add(new EmployeeDocumentItemDto
            {
                DocumentType = docType,
                DocumentNumber = docNo,
                IssuedOn = DateOnly.FromDateTime((doc.UploadedUtc == default ? now : doc.UploadedUtc).UtcDateTime),
                MediaAssetId = mediaId,
                FileName = doc.FileName,
                PublicUrl = $"/media/{mediaId}"
            });
        }

        return items;
    }

    private static List<EmployeeEducationItemDto> BuildEducation(
        IReadOnlyList<MasterOptionRecord> options,
        IReadOnlyDictionary<string, string>? form,
        DateOnly? dob,
        DateOnly joinedOn)
    {
        var list = new List<EmployeeEducationItemDto>();
        if (form is null || form.Count == 0)
        {
            return list;
        }

        var institute = NullIfBlank(GetVal(form, "institute", "college", "school"));
        if (institute is null)
        {
            return list;
        }

        var defaultYear = dob is { } b
            ? Math.Clamp(b.Year + 22, 1950, joinedOn.Year)
            : Math.Clamp(joinedOn.Year - 2, 1950, 2100);

        var passingYear = int.TryParse(GetVal(form, "passingYear", "year"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var yr) && yr is >= 1950 and <= 2100
            ? yr
            : defaultYear;

        decimal? cgpa = decimal.TryParse(GetVal(form, "cgpa"), NumberStyles.Number, CultureInfo.InvariantCulture, out var c) && c is >= 0m and <= 10m
            ? c
            : null;

        decimal? pct = decimal.TryParse(GetVal(form, "percentage"), NumberStyles.Number, CultureInfo.InvariantCulture, out var p) && p is >= 0m and <= 100m
            ? p
            : null;

        list.Add(new EmployeeEducationItemDto
        {
            EducationLevelCode = ResolveOptionCode(options, MasterCategories.EducationLevel, GetVal(form, "educationLevel", "educationLevelCode")),
            QualificationCode = ResolveOptionCode(options, MasterCategories.Qualification, GetVal(form, "qualification", "qualificationCode")),
            Specialisation = NullIfBlank(GetVal(form, "specialisation", "specialization")),
            Institute = institute,
            BoardOrUniversity = NullIfBlank(GetVal(form, "boardOrUniversity", "university")),
            PassingYear = passingYear,
            Cgpa = cgpa,
            Percentage = pct,
            GradeLetter = NullIfBlank(GetVal(form, "gradeLetter", "grade")),
            CertificationName = NullIfBlank(GetVal(form, "certificationName")),
            CertificationValidTill = ParseDate(GetVal(form, "certificationValidTill")),
            IsHighest = true
        });

        return list;
    }

    private static List<EmployeePreviousCompanyItemDto> BuildPreviousCompanies(
        IReadOnlyList<MasterOptionRecord> options,
        IReadOnlyDictionary<string, string>? form,
        DateOnly joinedOn,
        decimal totalExpYears,
        decimal? expectedCtc)
    {
        var list = new List<EmployeePreviousCompanyItemDto>();
        if (form is null || form.Count == 0)
        {
            return list;
        }

        var company = NullIfBlank(GetVal(form, "companyName", "company", "previousEmployer"));
        if (company is null)
        {
            return list;
        }

        var months = Math.Max(6, (int)Math.Round(Math.Max(1m, totalExpYears) * 12m));
        var prevLeft = ParseDate(GetVal(form, "leftOn", "leavingDate")) ?? joinedOn.AddDays(-7);
        var prevJoined = ParseDate(GetVal(form, "joinedOn", "joiningDate")) ?? prevLeft.AddMonths(-months);
        if (prevLeft < prevJoined)
        {
            prevLeft = prevJoined;
        }

        var empType = GetVal(form, "employmentType") switch
        {
            "Contract" => "Contract",
            "Intern" => "Intern",
            "Consultant" => "Consultant",
            _ => "Permanent"
        };

        decimal? lastCtc = decimal.TryParse(GetVal(form, "lastAnnualCtc", "lastCtc"), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedCtc) && parsedCtc >= 0m
            ? parsedCtc
            : (expectedCtc is > 0m ? Math.Round(expectedCtc.Value * 0.85m, 0) : null);

        list.Add(new EmployeePreviousCompanyItemDto
        {
            CompanyName = company,
            Designation = NullIfBlank(GetVal(form, "designation", "role")),
            Department = NullIfBlank(GetVal(form, "department")),
            PreviousEmployeeId = NullIfBlank(GetVal(form, "previousEmployeeId")),
            JoinedOn = prevJoined,
            LeftOn = prevLeft,
            EmploymentType = empType,
            LastAnnualCtc = lastCtc,
            ReportingManagerName = NullIfBlank(GetVal(form, "reportingManagerName", "reportingManager")),
            LeavingReasonCode = ResolveOptionCode(options, MasterCategories.LeavingReason, GetVal(form, "leavingReason", "leavingReasonCode")),
            NoticeServed = ParseBool(GetVal(form, "noticeServed"), true),
            Remarks = NullIfBlank(GetVal(form, "remarks"))
        });

        return list;
    }

    private static DesignationDto? MatchDesignation(
        IReadOnlyList<DesignationDto> designations,
        string? designationCode,
        string? jobTitle,
        string deptCode)
    {
        if (!string.IsNullOrWhiteSpace(designationCode))
        {
            var byCode = designations.FirstOrDefault(d =>
                string.Equals(d.Code, designationCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byCode is not null)
            {
                return byCode;
            }
        }

        if (!string.IsNullOrWhiteSpace(jobTitle))
        {
            var title = jobTitle.Trim();
            var exact = designations.FirstOrDefault(d =>
                string.Equals(d.Name, title, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.Code, title, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }

            if (title.Contains("Human Resources Executive", StringComparison.OrdinalIgnoreCase))
            {
                return designations.FirstOrDefault(d => string.Equals(d.Code, "HRE", StringComparison.OrdinalIgnoreCase));
            }
            if (title.Contains("Human Resources Manager", StringComparison.OrdinalIgnoreCase))
            {
                return designations.FirstOrDefault(d => string.Equals(d.Code, "HRM", StringComparison.OrdinalIgnoreCase));
            }
        }

        return designations.FirstOrDefault(d =>
            d.IsActive && string.Equals(d.DepartmentCode, deptCode, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveDepartmentCode(IReadOnlyList<MasterOptionRecord> options, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var topDepts = options.Where(o => o.Category == MasterCategories.Department && string.IsNullOrWhiteSpace(o.ParentCode)).ToList();
        var trimmed = raw.Trim();

        var match = topDepts.FirstOrDefault(o =>
            string.Equals(o.Code, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(o.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        return match?.Code;
    }

    private static string? ResolveSubDepartmentCode(
        IReadOnlyList<MasterOptionRecord> options,
        string deptCode,
        string? rawSubDept,
        string jobTitle)
    {
        var subs = options
            .Where(o => o.Category == MasterCategories.Department &&
                        string.Equals(o.ParentCode, deptCode, StringComparison.OrdinalIgnoreCase) &&
                        o.IsActive)
            .OrderBy(o => o.SortOrder)
            .ToList();

        if (subs.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(rawSubDept))
        {
            var byInput = subs.FirstOrDefault(o =>
                string.Equals(o.Code, rawSubDept.Trim(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(o.Name, rawSubDept.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byInput is not null)
            {
                return byInput.Code;
            }
        }

        var preferred = (deptCode.ToUpperInvariant(), jobTitle.Contains("Manager", StringComparison.OrdinalIgnoreCase) || jobTitle.Contains("Analyst", StringComparison.OrdinalIgnoreCase) || jobTitle.Contains("Engineer", StringComparison.OrdinalIgnoreCase)) switch
        {
            ("RETAIL", true) => "RETAIL_OPS",
            ("RETAIL", false) => "RETAIL_FLOOR",
            ("FINOPS", true) => "FINOPS_FPNA",
            ("FINOPS", false) => "FINOPS_ACCOUNTS",
            ("HR", true) => "HR_PEOPLE_OPS",
            ("HR", false) => "HR_TALENT",
            ("DIGITAL", true) => "DIGITAL_ENG",
            ("DIGITAL", false) => "DIGITAL_CAT",
            ("BRAND", _) => "BRAND_VM",
            _ => subs[0].Code
        };

        return subs.FirstOrDefault(s => string.Equals(s.Code, preferred, StringComparison.OrdinalIgnoreCase))?.Code
               ?? subs[0].Code;
    }

    private static string DefaultDivisionFor(string deptCode) => deptCode.ToUpperInvariant() switch
    {
        "RETAIL" => "STORE",
        "BRAND" => "REGIONAL",
        _ => "HO"
    };

    private static string? ResolveOptionCode(
        IReadOnlyList<MasterOptionRecord> options,
        string category,
        string? raw,
        IReadOnlyDictionary<string, string>? synonyms = null)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        var catOptions = options.Where(o => o.Category == category).ToList();

        var direct = catOptions.FirstOrDefault(o =>
            string.Equals(o.Code, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(o.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (direct is not null)
        {
            return direct.Code;
        }

        if (synonyms is not null && synonyms.TryGetValue(trimmed, out var mappedCode))
        {
            var mapped = catOptions.FirstOrDefault(o => string.Equals(o.Code, mappedCode, StringComparison.OrdinalIgnoreCase));
            return mapped?.Code ?? mappedCode;
        }

        return null;
    }

    private static readonly IReadOnlyDictionary<string, string> GenderSynonyms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["M"] = "MALE",
            ["F"] = "FEMALE",
            ["NonBinary"] = "NON_BINARY"
        };

    private static readonly IReadOnlyDictionary<string, string> BloodGroupSynonyms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A+"] = "A_POS", ["A-"] = "A_NEG",
            ["B+"] = "B_POS", ["B-"] = "B_NEG",
            ["AB+"] = "AB_POS", ["AB-"] = "AB_NEG",
            ["O+"] = "O_POS", ["O-"] = "O_NEG"
        };

    private static readonly IReadOnlyDictionary<string, string> NationalitySynonyms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["IN"] = "INDIAN", ["India"] = "INDIAN",
            ["US"] = "AMERICAN", ["UK"] = "BRITISH", ["GB"] = "BRITISH"
        };

    private static readonly IReadOnlyDictionary<string, string> JobTypeSynonyms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Full-time"] = "FULL_TIME", ["Full Time"] = "FULL_TIME", ["Permanent"] = "FULL_TIME",
            ["Part-time"] = "PART_TIME", ["Part Time"] = "PART_TIME",
            ["Intern"] = "INTERNSHIP"
        };

    private static readonly IReadOnlyDictionary<string, string> LocationSynonyms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Chandigarh, Mohali"] = "CHD_MOH",
            ["Panchkula"] = "CHANDIGARH",
            ["Ludhiana"] = "CHD_MOH",
            ["Gurugram"] = "DELHI_NCR",
            ["Noida"] = "DELHI_NCR",
            ["Delhi"] = "DELHI_NCR",
            ["New Delhi"] = "DELHI_NCR"
        };

    private static readonly IReadOnlyDictionary<string, string> CitySynonyms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mohali"] = "MOHALI",
            ["Delhi"] = "DELHI",
            ["Delhi NCR"] = "DELHI",
            ["Chandigarh, Mohali"] = "CHANDIGARH"
        };

    private static readonly IReadOnlyDictionary<string, string> CountrySynonyms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["India"] = "IN", ["INDIAN"] = "IN"
        };

    private static string? InferCityFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var city in new[] { "Chandigarh", "Mohali", "Panchkula", "Ludhiana", "Amritsar", "Jalandhar", "Gurugram", "Noida", "Delhi", "Mumbai", "Bengaluru", "Pune", "Jaipur" })
        {
            if (text.Contains(city, StringComparison.OrdinalIgnoreCase))
            {
                return city;
            }
        }
        return null;
    }

    private static string? InferStateFromCity(string? cityCode) => cityCode?.ToUpperInvariant() switch
    {
        "CHANDIGARH" => "CH_UT",
        "MOHALI" or "LUDHIANA" or "AMRITSAR" or "JALANDHAR" => "PB",
        "PANCHKULA" or "GURUGRAM" => "HR_ST",
        "DELHI" => "DL",
        "NOIDA" => "UP",
        "MUMBAI" or "PUNE" => "MH",
        "BENGALURU" => "KA",
        "JAIPUR" => "RJ",
        _ => null
    };

    private static string? CleanPincode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var pin = raw.Trim();
        return DigitsOnlyRegex.IsMatch(pin) && pin.Length is >= 4 and <= 10 ? pin : null;
    }

    private static string? NormalizePhoneOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            return EmployeePhone.Normalize(raw);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string SuggestEmployeeNumber(IReadOnlyList<string> numbers, long candidateId)
    {
        var existingSet = numbers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var best = numbers
            .Select(num => Regex.Match(num, @"^([A-Za-z]+-)(\d+)$"))
            .Where(match => match.Success)
            .Select(match => (
                Prefix: match.Groups[1].Value,
                Digits: match.Groups[2].Value,
                Value: long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)))
            .OrderByDescending(item => item.Value)
            .FirstOrDefault();

        var candidate = best.Prefix is null
            ? $"EMP-{1000 + candidateId:D4}"
            : best.Prefix + (best.Value + 1).ToString(CultureInfo.InvariantCulture).PadLeft(best.Digits.Length, '0');

        return existingSet.Contains(candidate) ? $"EMP-{candidateId:D5}" : candidate;
    }

    private static IReadOnlyDictionary<string, string>? FormFor(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? forms, string code) =>
        forms is not null && forms.TryGetValue(code, out var dict) ? dict : null;

    private static string? GetVal(IReadOnlyDictionary<string, string>? dict, params string[] keys)
    {
        if (dict is null) return null;
        foreach (var key in keys)
        {
            if (dict.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val.Trim();
            }

            var ci = dict.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(ci.Value))
            {
                return ci.Value.Trim();
            }
        }
        return null;
    }

    private static DateOnly? ParseDate(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && DateOnly.TryParse(raw.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    private static bool ParseBool(string? raw, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        return raw.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "y" => true,
            "false" or "0" or "no" or "n" => false,
            _ => fallback
        };
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static (string First, string? Middle, string Last) SplitName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            0 => ("Unknown", null, "Unknown"),
            1 => (parts[0], null, parts[0]),
            2 => (parts[0], null, parts[1]),
            _ => (parts[0], string.Join(' ', parts[1..^1]), parts[^1])
        };
    }
}
