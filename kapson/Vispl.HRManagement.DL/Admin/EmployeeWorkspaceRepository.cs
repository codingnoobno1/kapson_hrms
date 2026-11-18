using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.Employee;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;

namespace Vispl.HRManagement.DL.Admin;

public sealed class EmployeeWorkspaceRepository(HrManagementDbContext dbContext) : IEmployeeWorkspaceRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<EmployeeExplorerItemRecord>> ListExplorerAsync(
        string tenantKey, CancellationToken cancellationToken = default)
    {
        return await dbContext.Employees
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey)
            .OrderByDescending(item => item.IsDraft)
            .ThenBy(item => item.LegalFirstName)
            .ThenBy(item => item.LegalLastName)
            .ThenBy(item => item.EmployeeNumber)
            .Select(item => new EmployeeExplorerItemRecord(
                item.Id,
                item.EmployeeNumber,
                item.LegalFirstName,
                item.LegalMiddleName,
                item.LegalLastName,
                item.PreferredName,
                item.AvatarUrl,
                item.JobTitle,
                item.DepartmentCode,
                item.SubDepartmentCode,
                item.DivisionCode,
                item.LocationCode,
                item.GradeCode,
                item.JoinedOn,
                item.Email,
                item.Phone,
                item.ManagerEmployeeId,
                item.EmploymentStatusCode,
                item.IsActive,
                item.IsDraft))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetAllEmployeeNumbersAsync(
        string tenantKey, CancellationToken cancellationToken = default)
    {
        return await dbContext.Employees
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey)
            .Select(item => item.EmployeeNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeWorkspaceAggregate?> GetAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default)
    {
        var emp = await dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == employeeId, cancellationToken);

        if (emp is null)
        {
            return null;
        }

        var statutory = await dbContext.EmployeeStatutory
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId, cancellationToken);

        var addresses = await dbContext.EmployeeAddresses
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        var education = await dbContext.EmployeeEducation
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId)
            .OrderByDescending(item => item.PassingYear)
            .ToListAsync(cancellationToken);

        var prevCompanies = await dbContext.EmployeePreviousEmployment
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId)
            .OrderByDescending(item => item.LeftOn)
            .ToListAsync(cancellationToken);

        var bank = await dbContext.EmployeeBankAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId, cancellationToken);

        var salary = await dbContext.EmployeeSalaryConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId, cancellationToken);

        var documents = await dbContext.EmployeeDocuments
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var leaving = await dbContext.EmployeeLeavingDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId, cancellationToken);

        var permAddr = addresses.FirstOrDefault(a => a.AddressType == "Permanent");
        var currAddr = addresses.FirstOrDefault(a => a.AddressType == "Current");

        var permDto = permAddr is null
            ? new EmployeeAddressItemDto { AddressType = "Permanent" }
            : new EmployeeAddressItemDto
            {
                AddressType = "Permanent",
                Line1 = permAddr.Line1,
                Line2 = permAddr.Line2,
                CountryCode = permAddr.CountryCode,
                StateCode = permAddr.StateCode,
                CityCode = permAddr.CityCode,
                Pincode = permAddr.Pincode
            };

        var currDto = currAddr is null
            ? new EmployeeAddressItemDto { AddressType = "Current" }
            : new EmployeeAddressItemDto
            {
                AddressType = "Current",
                Line1 = currAddr.Line1,
                Line2 = currAddr.Line2,
                CountryCode = currAddr.CountryCode,
                StateCode = currAddr.StateCode,
                CityCode = currAddr.CityCode,
                Pincode = currAddr.Pincode
            };

        var sameAddr = permAddr is not null && currAddr is not null &&
                       string.Equals(permDto.Line1, currDto.Line1, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(permDto.Pincode ?? "", currDto.Pincode ?? "", StringComparison.OrdinalIgnoreCase);

        List<EmployeeSalaryAllowanceRowDto> salaryRows = [];
        if (salary is not null && !string.IsNullOrWhiteSpace(salary.ComponentsJson))
        {
            try
            {
                salaryRows = JsonSerializer.Deserialize<List<EmployeeSalaryAllowanceRowDto>>(salary.ComponentsJson, Json) ?? [];
            }
            catch
            {
                salaryRows = [];
            }
        }

        return new EmployeeWorkspaceAggregate
        {
            EmployeeId = emp.Id,
            TenantKey = emp.TenantKey,
            IsDraft = emp.IsDraft,
            IsActive = emp.IsActive,
            EmploymentStatusCode = emp.EmploymentStatusCode,
            LeavingOn = emp.LeavingOn,
            AadhaarProtected = statutory?.AadhaarProtected,
            Personal = new EmployeePersonalWorkspaceDto
            {
                EmployeeNumber = emp.EmployeeNumber,
                FirstName = emp.LegalFirstName,
                MiddleName = emp.LegalMiddleName,
                LastName = emp.LegalLastName,
                Email = emp.Email ?? string.Empty,
                Mobile = emp.Phone ?? string.Empty,
                DateOfBirth = emp.DateOfBirth,
                JoinedOn = emp.JoinedOn,
                GenderCode = emp.GenderCode,
                AvatarUrl = emp.AvatarUrl,
                DepartmentCode = emp.DepartmentCode,
                SubDepartmentCode = emp.SubDepartmentCode,
                JobTitle = emp.JobTitle,
                DivisionCode = emp.DivisionCode,
                LocationCode = emp.LocationCode,
                ReportingManagerId = emp.ManagerEmployeeId?.ToString(CultureInfo.InvariantCulture),
                MaritalStatusCode = emp.MaritalStatusCode,
                BloodGroupCode = emp.BloodGroupCode,
                ReligionCode = emp.ReligionCode,
                OtherReligion = emp.OtherReligion,
                NationalityCode = emp.NationalityCode,
                FatherName = emp.FatherName,
                FatherOccupationCode = emp.FatherOccupationCode,
                MotherName = emp.MotherName,
                MotherOccupationCode = emp.MotherOccupationCode,
                SpouseName = emp.SpouseName,
                SpouseOccupationCode = emp.SpouseOccupationCode,
                PanNumber = statutory?.PanNumber,
                AadhaarLast4 = statutory?.AadhaarLast4,
                VoterId = statutory?.VoterId,
                PermanentAddress = permDto,
                CurrentAddress = currDto,
                SameAsPermanentAddress = sameAddr
            },
            Education = education.Select(item => new EmployeeEducationItemDto
            {
                Id = item.Id,
                QualificationCode = item.QualificationCode,
                EducationLevelCode = item.EducationLevelCode,
                Specialisation = item.Specialisation,
                Institute = item.Institute,
                BoardOrUniversity = item.BoardOrUniversity,
                PassingYear = item.PassingYear,
                Cgpa = item.Cgpa,
                GradeLetter = item.GradeLetter,
                Percentage = item.Percentage,
                CertificationName = item.CertificationName,
                CertificationValidTill = item.CertificationValidTill,
                IsHighest = item.IsHighest
            }).ToList(),
            PreviousCompanies = prevCompanies.Select(item => new EmployeePreviousCompanyItemDto
            {
                Id = item.Id,
                CompanyName = item.CompanyName,
                Designation = item.Designation,
                Department = item.Department,
                PreviousEmployeeId = item.PreviousEmployeeId,
                JoinedOn = item.JoinedOn,
                LeftOn = item.LeftOn,
                EmploymentType = item.EmploymentType,
                LastAnnualCtc = item.LastAnnualCtc,
                ReportingManagerName = item.ReportingManagerName,
                LeavingReasonCode = item.LeavingReasonCode,
                NoticeServed = item.NoticeServed,
                Remarks = item.Remarks
            }).ToList(),
            BankAccountNumberProtected = bank?.AccountNumberProtected,
            BankAccountLast4 = bank?.AccountLast4,
            Bank = bank is null
                ? new EmployeeBankWorkspaceDto()
                : new EmployeeBankWorkspaceDto
                {
                    AccountHolderName = bank.AccountHolderName,
                    BankName = bank.BankName,
                    MaskedAccountNumber = $"••••{bank.AccountLast4}",
                    Ifsc = bank.Ifsc,
                    BranchName = bank.BranchName,
                    AccountType = bank.AccountType,
                    TransferSalarySlipBy = bank.TransferSalarySlipBy,
                    SalaryFormat = bank.SalaryFormat,
                    SalaryAccountCode = bank.SalaryAccountCode,
                    AdvancedAccountCode = bank.AdvancedAccountCode
                },
            PfEsi = statutory is null
                ? new EmployeePfEsiWorkspaceDto()
                : new EmployeePfEsiWorkspaceDto
                {
                    PfApplicable = statutory.PfApplicable,
                    PfJoinedOn = statutory.PfJoinedOn,
                    PfScheme = string.IsNullOrWhiteSpace(statutory.PfScheme) ? "Govt" : statutory.PfScheme,
                    PfNumber = statutory.PfNumber,
                    UanNumber = statutory.UanNumber,
                    EpfAccountCode = statutory.EpfAccountCode,
                    EsiApplicable = statutory.EsiApplicable,
                    EsiNumber = statutory.EsiNumber,
                    EsiAccountCode = statutory.EsiAccountCode
                },
            Salary = new EmployeeSalaryWorkspaceDto
            {
                ApplyMaxLimit = salary?.ApplyMaxLimit ?? true,
                EpfEmployerShareRule = salary?.EpfEmployerShareRule ?? "Apply EPF Employer Share on Maxlimit",
                EpfOnBasicOnly = salary?.EpfOnBasicOnly ?? true,
                ExcludeFromPayroll = emp.ExcludeFromPayroll,
                GradeCode = emp.GradeCode,
                BasicSalary = salary?.BasicSalary ?? 0m,
                Components = salaryRows
            },
            Documents = documents.Select(item => new EmployeeDocumentItemDto
            {
                Id = item.Id,
                DocumentType = item.DocumentType,
                DocumentNumber = item.DocumentNumber,
                IssuedOn = item.IssuedOn,
                ExpiresOn = item.ExpiresOn,
                MediaAssetId = item.MediaAssetId,
                FileName = item.OriginalFileName,
                PublicUrl = item.PublicUrl
            }).ToList(),
            Leaving = leaving is null
                ? new EmployeeLeavingWorkspaceDto()
                : new EmployeeLeavingWorkspaceDto
                {
                    HasLeftOrganization = leaving.HasLeftOrganization,
                    ResignationSubmitDate = leaving.ResignationSubmitDate,
                    FinalSettlementDate = leaving.FinalSettlementDate,
                    LeavingReasonCode = leaving.LeavingReasonCode,
                    LeavingRemarks = leaving.LeavingRemarks,
                    IsDismissed = leaving.IsDismissed,
                    DismissDate = leaving.DismissDate,
                    DismissRemarks = leaving.DismissRemarks,
                    SettlementDate = leaving.SettlementDate
                }
        };
    }

    public async Task<string?> FindConflictAsync(
        string tenantKey,
        long? excludeEmployeeId,
        string employeeNumber,
        string? email,
        string? normalizedPhone,
        string? panNumber,
        CancellationToken cancellationToken = default)
    {
        var matches = await dbContext.Employees
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey &&
                           (excludeEmployeeId == null || item.Id != excludeEmployeeId.Value) &&
                           (item.EmployeeNumber == employeeNumber ||
                            (email != null && item.Email == email) ||
                            (normalizedPhone != null && item.Phone == normalizedPhone)))
            .ToListAsync(cancellationToken);

        foreach (var match in matches)
        {
            var who = $"{match.LegalFirstName} {match.LegalLastName}".Trim();
            if (string.Equals(match.EmployeeNumber, employeeNumber, StringComparison.OrdinalIgnoreCase))
            {
                return $"Employee code {employeeNumber} is already used by {who}.";
            }
            if (email is not null && string.Equals(match.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                return $"Email {email} already belongs to {who} ({match.EmployeeNumber}).";
            }
            if (normalizedPhone is not null && match.Phone == normalizedPhone)
            {
                return $"Mobile number {normalizedPhone} already belongs to {who} ({match.EmployeeNumber}).";
            }
        }

        if (!string.IsNullOrWhiteSpace(panNumber))
        {
            var panOwner = await dbContext.EmployeeStatutory
                .AsNoTracking()
                .Where(item => item.TenantKey == tenantKey &&
                               (excludeEmployeeId == null || item.EmployeeId != excludeEmployeeId.Value) &&
                               item.PanNumber == panNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (panOwner is not null)
            {
                return $"PAN {panNumber} is already recorded on another employee.";
            }
        }

        return null;
    }

    public async Task<long> SaveAsync(
        EmployeeWorkspaceAggregate aggregate, CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.IsRelational())
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                var id = await PersistCoreAsync(aggregate, cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return id;
            });
        }

        return await PersistCoreAsync(aggregate, cancellationToken);
    }

    private async Task<long> PersistCoreAsync(EmployeeWorkspaceAggregate aggregate, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var tenantKey = aggregate.TenantKey;
        var p = aggregate.Personal;
        var isNew = aggregate.EmployeeId <= 0;

        EmployeeEntity emp;
        if (isNew)
        {
            emp = new EmployeeEntity
            {
                TenantKey = tenantKey,
                CreatedUtc = now,
                EmploymentTypeCode = "FULL_TIME"
            };
            dbContext.Employees.Add(emp);
        }
        else
        {
            emp = await dbContext.Employees
                      .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == aggregate.EmployeeId, ct)
                  ?? throw new InvalidOperationException("Employee record was not found.");
        }

        long? managerId = long.TryParse(p.ReportingManagerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMgr) &&
                          (!isNew ? parsedMgr != emp.Id : true)
            ? parsedMgr
            : null;

        emp.EmployeeNumber = p.EmployeeNumber;
        emp.LegalFirstName = p.FirstName;
        emp.LegalMiddleName = NullIfBlank(p.MiddleName);
        emp.LegalLastName = p.LastName;
        emp.Email = NullIfBlank(p.Email);
        emp.Phone = NullIfBlank(p.Mobile);
        emp.DateOfBirth = p.DateOfBirth;
        emp.JoinedOn = p.JoinedOn;
        emp.GenderCode = NullIfBlank(p.GenderCode);
        emp.AvatarUrl = NullIfBlank(p.AvatarUrl);
        emp.DepartmentCode = string.IsNullOrWhiteSpace(p.DepartmentCode) ? "UNASSIGNED" : p.DepartmentCode.Trim();
        emp.SubDepartmentCode = NullIfBlank(p.SubDepartmentCode);
        emp.JobTitle = NullIfBlank(p.JobTitle);
        emp.DivisionCode = NullIfBlank(p.DivisionCode);
        emp.LocationCode = string.IsNullOrWhiteSpace(p.LocationCode) ? "HQ" : p.LocationCode.Trim();
        emp.ManagerEmployeeId = managerId;
        emp.MaritalStatusCode = NullIfBlank(p.MaritalStatusCode);
        emp.BloodGroupCode = NullIfBlank(p.BloodGroupCode);
        emp.ReligionCode = NullIfBlank(p.ReligionCode);
        emp.OtherReligion = NullIfBlank(p.OtherReligion);
        emp.NationalityCode = NullIfBlank(p.NationalityCode);
        emp.FatherName = NullIfBlank(p.FatherName);
        emp.FatherOccupationCode = NullIfBlank(p.FatherOccupationCode);
        emp.MotherName = NullIfBlank(p.MotherName);
        emp.MotherOccupationCode = NullIfBlank(p.MotherOccupationCode);
        emp.SpouseName = NullIfBlank(p.SpouseName);
        emp.SpouseOccupationCode = NullIfBlank(p.SpouseOccupationCode);
        emp.GradeCode = NullIfBlank(aggregate.Salary.GradeCode);
        emp.ExcludeFromPayroll = aggregate.Salary.ExcludeFromPayroll;
        emp.IsDraft = aggregate.IsDraft;
        emp.IsActive = aggregate.IsActive;
        emp.EmploymentStatusCode = aggregate.EmploymentStatusCode;
        emp.LeavingOn = aggregate.LeavingOn;
        emp.UpdatedUtc = now;

        await dbContext.SaveChangesAsync(ct);
        var employeeId = emp.Id;

        if (emp.ManagerEmployeeId == employeeId)
        {
            emp.ManagerEmployeeId = null;
            await dbContext.SaveChangesAsync(ct);
        }

        // 1. Statutory & PF/ESI
        var statutory = await dbContext.EmployeeStatutory
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId, ct);

        var hasStatutory = !string.IsNullOrWhiteSpace(p.PanNumber) ||
                           !string.IsNullOrWhiteSpace(aggregate.AadhaarProtected) ||
                           !string.IsNullOrWhiteSpace(p.VoterId) ||
                           aggregate.PfEsi.PfApplicable ||
                           aggregate.PfEsi.EsiApplicable ||
                           !string.IsNullOrWhiteSpace(aggregate.PfEsi.PfNumber) ||
                           !string.IsNullOrWhiteSpace(aggregate.PfEsi.UanNumber) ||
                           !string.IsNullOrWhiteSpace(aggregate.PfEsi.EsiNumber);

        if (hasStatutory || statutory is not null)
        {
            if (statutory is null)
            {
                statutory = new EmployeeStatutoryEntity { TenantKey = tenantKey, EmployeeId = employeeId };
                dbContext.EmployeeStatutory.Add(statutory);
            }

            statutory.PanNumber = NullIfBlank(p.PanNumber)?.ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(aggregate.AadhaarProtected) && !string.IsNullOrWhiteSpace(p.AadhaarLast4))
            {
                statutory.AadhaarProtected = aggregate.AadhaarProtected;
                statutory.AadhaarLast4 = p.AadhaarLast4;
            }
            statutory.VoterId = NullIfBlank(p.VoterId);
            statutory.PfApplicable = aggregate.PfEsi.PfApplicable;
            statutory.PfJoinedOn = aggregate.PfEsi.PfApplicable ? aggregate.PfEsi.PfJoinedOn : null;
            statutory.PfScheme = aggregate.PfEsi.PfApplicable
                ? (aggregate.PfEsi.PfScheme is "Govt" or "Company" ? aggregate.PfEsi.PfScheme : "Govt")
                : null;
            statutory.PfNumber = aggregate.PfEsi.PfApplicable ? NullIfBlank(aggregate.PfEsi.PfNumber) : null;
            statutory.UanNumber = aggregate.PfEsi.PfApplicable ? NullIfBlank(aggregate.PfEsi.UanNumber) : null;
            statutory.EpfAccountCode = aggregate.PfEsi.PfApplicable ? NullIfBlank(aggregate.PfEsi.EpfAccountCode) : null;
            statutory.EsiApplicable = aggregate.PfEsi.EsiApplicable;
            statutory.EsiNumber = aggregate.PfEsi.EsiApplicable ? NullIfBlank(aggregate.PfEsi.EsiNumber) : null;
            statutory.EsiAccountCode = aggregate.PfEsi.EsiApplicable ? NullIfBlank(aggregate.PfEsi.EsiAccountCode) : null;
            statutory.UpdatedBy = aggregate.UpdatedBy;
            statutory.UpdatedUtc = now;
        }

        // 2. Addresses (Permanent & Current)
        var existingAddresses = await dbContext.EmployeeAddresses
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId)
            .ToListAsync(ct);

        UpsertAddress(existingAddresses, tenantKey, employeeId, "Permanent", p.PermanentAddress, now);
        var currentSource = p.SameAsPermanentAddress ? p.PermanentAddress : p.CurrentAddress;
        UpsertAddress(existingAddresses, tenantKey, employeeId, "Current", currentSource, now);

        // 3. Education
        var existingEdu = await dbContext.EmployeeEducation
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId)
            .ToListAsync(ct);
        dbContext.EmployeeEducation.RemoveRange(existingEdu);

        foreach (var edu in aggregate.Education.Where(e => !string.IsNullOrWhiteSpace(e.Institute)))
        {
            dbContext.EmployeeEducation.Add(new EmployeeEducationEntity
            {
                TenantKey = tenantKey,
                EmployeeId = employeeId,
                QualificationCode = NullIfBlank(edu.QualificationCode),
                EducationLevelCode = NullIfBlank(edu.EducationLevelCode),
                Specialisation = NullIfBlank(edu.Specialisation),
                Institute = edu.Institute.Trim(),
                BoardOrUniversity = NullIfBlank(edu.BoardOrUniversity),
                PassingYear = Math.Clamp(edu.PassingYear, 1950, 2100),
                Cgpa = edu.Cgpa,
                GradeLetter = NullIfBlank(edu.GradeLetter),
                Percentage = edu.Percentage,
                CertificationName = NullIfBlank(edu.CertificationName),
                CertificationValidTill = edu.CertificationValidTill,
                IsHighest = edu.IsHighest,
                UpdatedUtc = now
            });
        }

        // 4. Previous Employment
        var existingPrev = await dbContext.EmployeePreviousEmployment
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId)
            .ToListAsync(ct);
        dbContext.EmployeePreviousEmployment.RemoveRange(existingPrev);

        foreach (var prev in aggregate.PreviousCompanies.Where(c => !string.IsNullOrWhiteSpace(c.CompanyName)))
        {
            var leftOn = prev.LeftOn < prev.JoinedOn ? prev.JoinedOn : prev.LeftOn;
            var empType = prev.EmploymentType is "Permanent" or "Contract" or "Intern" or "Consultant"
                ? prev.EmploymentType
                : null;

            dbContext.EmployeePreviousEmployment.Add(new EmployeePreviousEmploymentEntity
            {
                TenantKey = tenantKey,
                EmployeeId = employeeId,
                CompanyName = prev.CompanyName.Trim(),
                Designation = NullIfBlank(prev.Designation),
                Department = NullIfBlank(prev.Department),
                PreviousEmployeeId = NullIfBlank(prev.PreviousEmployeeId),
                JoinedOn = prev.JoinedOn,
                LeftOn = leftOn,
                EmploymentType = empType,
                LastAnnualCtc = prev.LastAnnualCtc,
                ReportingManagerName = NullIfBlank(prev.ReportingManagerName),
                LeavingReasonCode = NullIfBlank(prev.LeavingReasonCode),
                NoticeServed = prev.NoticeServed,
                Remarks = NullIfBlank(prev.Remarks),
                UpdatedUtc = now
            });
        }

        // 5. Bank Details
        var bankEntity = await dbContext.EmployeeBankAccounts
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId, ct);

        if (!string.IsNullOrWhiteSpace(aggregate.Bank.BankName) &&
            !string.IsNullOrWhiteSpace(aggregate.Bank.Ifsc) &&
            (!string.IsNullOrWhiteSpace(aggregate.BankAccountNumberProtected) || bankEntity is not null))
        {
            if (bankEntity is null)
            {
                bankEntity = new EmployeeBankAccountEntity { TenantKey = tenantKey, EmployeeId = employeeId };
                dbContext.EmployeeBankAccounts.Add(bankEntity);
            }

            bankEntity.BankName = aggregate.Bank.BankName.Trim();
            bankEntity.AccountHolderName = string.IsNullOrWhiteSpace(aggregate.Bank.AccountHolderName)
                ? $"{p.FirstName} {p.LastName}".Trim()
                : aggregate.Bank.AccountHolderName.Trim();
            if (!string.IsNullOrWhiteSpace(aggregate.BankAccountNumberProtected) &&
                !string.IsNullOrWhiteSpace(aggregate.BankAccountLast4))
            {
                bankEntity.AccountNumberProtected = aggregate.BankAccountNumberProtected;
                bankEntity.AccountLast4 = aggregate.BankAccountLast4;
            }
            bankEntity.Ifsc = aggregate.Bank.Ifsc.Trim().ToUpperInvariant();
            bankEntity.BranchName = NullIfBlank(aggregate.Bank.BranchName);
            bankEntity.AccountType = string.IsNullOrWhiteSpace(aggregate.Bank.AccountType)
                ? BankAccountTypes.Savings
                : aggregate.Bank.AccountType.Trim();
            bankEntity.TransferSalarySlipBy = NullIfBlank(aggregate.Bank.TransferSalarySlipBy);
            bankEntity.SalaryFormat = NullIfBlank(aggregate.Bank.SalaryFormat);
            bankEntity.SalaryAccountCode = NullIfBlank(aggregate.Bank.SalaryAccountCode);
            bankEntity.AdvancedAccountCode = NullIfBlank(aggregate.Bank.AdvancedAccountCode);
            bankEntity.UpdatedBy = aggregate.UpdatedBy;
            bankEntity.UpdatedUtc = now;
        }

        // 6. Salary Config
        var salaryEntity = await dbContext.EmployeeSalaryConfigs
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId, ct);

        if (salaryEntity is null)
        {
            salaryEntity = new EmployeeSalaryConfigEntity { TenantKey = tenantKey, EmployeeId = employeeId };
            dbContext.EmployeeSalaryConfigs.Add(salaryEntity);
        }

        salaryEntity.BasicSalary = Math.Max(0m, aggregate.Salary.BasicSalary);
        salaryEntity.ApplyMaxLimit = aggregate.Salary.ApplyMaxLimit;
        salaryEntity.EpfEmployerShareRule = NullIfBlank(aggregate.Salary.EpfEmployerShareRule);
        salaryEntity.EpfOnBasicOnly = aggregate.Salary.EpfOnBasicOnly;
        salaryEntity.ComponentsJson = JsonSerializer.Serialize(aggregate.Salary.Components ?? [], Json);
        salaryEntity.UpdatedUtc = now;

        // 7. Documents
        var existingDocs = await dbContext.EmployeeDocuments
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId)
            .ToListAsync(ct);
        dbContext.EmployeeDocuments.RemoveRange(existingDocs);

        foreach (var doc in aggregate.Documents.Where(d =>
                     !string.IsNullOrWhiteSpace(d.DocumentType) || !string.IsNullOrWhiteSpace(d.FileName)))
        {
            dbContext.EmployeeDocuments.Add(new EmployeeDocumentEntity
            {
                TenantKey = tenantKey,
                EmployeeId = employeeId,
                DocumentType = string.IsNullOrWhiteSpace(doc.DocumentType) ? "General" : doc.DocumentType.Trim(),
                DocumentNumber = NullIfBlank(doc.DocumentNumber),
                IssuedOn = doc.IssuedOn,
                ExpiresOn = doc.ExpiresOn,
                MediaAssetId = doc.MediaAssetId,
                OriginalFileName = NullIfBlank(doc.FileName),
                PublicUrl = NullIfBlank(doc.PublicUrl),
                UpdatedUtc = now
            });
        }

        // 8. Leaving Details
        var leavingEntity = await dbContext.EmployeeLeavingDetails
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId, ct);

        var hasLeaving = aggregate.Leaving.HasLeftOrganization ||
                         aggregate.Leaving.IsDismissed ||
                         aggregate.Leaving.ResignationSubmitDate is not null ||
                         aggregate.Leaving.DismissDate is not null;

        if (hasLeaving || leavingEntity is not null)
        {
            if (leavingEntity is null)
            {
                leavingEntity = new EmployeeLeavingEntity { TenantKey = tenantKey, EmployeeId = employeeId };
                dbContext.EmployeeLeavingDetails.Add(leavingEntity);
            }

            leavingEntity.HasLeftOrganization = aggregate.Leaving.HasLeftOrganization;
            leavingEntity.ResignationSubmitDate = aggregate.Leaving.ResignationSubmitDate;
            leavingEntity.FinalSettlementDate = aggregate.Leaving.FinalSettlementDate;
            leavingEntity.LeavingReasonCode = NullIfBlank(aggregate.Leaving.LeavingReasonCode);
            leavingEntity.LeavingRemarks = NullIfBlank(aggregate.Leaving.LeavingRemarks);
            leavingEntity.IsDismissed = aggregate.Leaving.IsDismissed;
            leavingEntity.DismissDate = aggregate.Leaving.DismissDate;
            leavingEntity.DismissRemarks = NullIfBlank(aggregate.Leaving.DismissRemarks);
            leavingEntity.SettlementDate = aggregate.Leaving.SettlementDate;
            leavingEntity.UpdatedUtc = now;
        }

        // 9. Timeline entry
        dbContext.EmployeeTimeline.Add(new EmployeeTimelineEntity
        {
            TenantKey = tenantKey,
            EmployeeId = employeeId,
            EventType = isNew ? "Hired" : "DetailsChanged",
            Title = aggregate.IsDraft
                ? (isNew ? "Draft employee created" : "Draft employee updated")
                : (isNew ? "Employee record created" : "Employee master updated"),
            Summary = $"{emp.LegalFirstName} {emp.LegalLastName}".Trim() + $" ({emp.EmployeeNumber})",
            EffectiveOn = emp.JoinedOn,
            ActorId = aggregate.UpdatedBy,
            ActorName = aggregate.UpdatedByName ?? "HR Portal",
            OccurredUtc = now
        });

        await dbContext.SaveChangesAsync(ct);
        return employeeId;
    }

    private void UpsertAddress(
        List<EmployeeAddressEntity> existing,
        string tenantKey,
        long employeeId,
        string addressType,
        EmployeeAddressItemDto dto,
        DateTimeOffset now)
    {
        var row = existing.FirstOrDefault(item => item.AddressType == addressType);
        if (string.IsNullOrWhiteSpace(dto.Line1))
        {
            if (row is not null)
            {
                dbContext.EmployeeAddresses.Remove(row);
            }
            return;
        }

        if (row is null)
        {
            row = new EmployeeAddressEntity
            {
                TenantKey = tenantKey,
                EmployeeId = employeeId,
                AddressType = addressType
            };
            dbContext.EmployeeAddresses.Add(row);
        }

        row.Line1 = dto.Line1.Trim();
        row.Line2 = NullIfBlank(dto.Line2);
        row.CountryCode = NullIfBlank(dto.CountryCode);
        row.StateCode = NullIfBlank(dto.StateCode);
        row.CityCode = NullIfBlank(dto.CityCode);
        row.Pincode = NullIfBlank(dto.Pincode);
        row.UpdatedUtc = now;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
