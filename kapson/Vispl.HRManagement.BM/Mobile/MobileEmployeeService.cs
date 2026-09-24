using System.Security.Cryptography;
using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.Contracts.Mobile.Employee;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.VO.Employee;

namespace Vispl.HRManagement.BM.Mobile;

public sealed class MobileEmployeeService(
    IEmployeeRepository employees,
    DL.ITF.Admin.IMasterOptionRepository masters,
    ITF.Shared.INotificationPublisher notifications,
    ITF.Shared.ISensitiveValueProtector protector,
    ITF.Admin.IBankAccountService bankAccounts) : IMobileEmployeeService
{
    /// <summary>Scopes the encryption key to this one use, so it cannot decrypt anything else this protector guards.</summary>
    private const string BankChangeRequestProtectionPurpose = "EmployeeChangeRequest.BankAccountNumber";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Notification category for everything this service raises.</summary>
    private const string NotificationCategory = "Employee";

    /// <summary>Subject type, so a repeat about the same request is not stacked in an inbox.</summary>
    private const string NotificationSubject = "EmployeeChangeRequest";

    private const string ViewPermission = "EMPLOYEES.VIEW";
    private const string EditPermission = "EMPLOYEES.EDIT";
    private const string CreatePermission = "EMPLOYEES.CREATE";
    private const string CompensationPermission = "PAYROLL.VIEW";

    // These match the RequirePermission attributes on the routes. Enforcing the same
    // permission in both places means a caller cannot pass the route and fail the service.
    private const string EmploymentPermission = "EMPLOYEES.EMPLOYMENT_MANAGE";
    private const string OrganisationPermission = "EMPLOYEES.ORGANISATION_MANAGE";

    // ---- Reads ------------------------------------------------------------

    public async Task<CursorPage<EmployeeListItemDto>> ListAsync(
        MobileCallerContext caller, EmployeeListQuery query, CancellationToken ct)
    {
        Require(caller, ViewPermission);

        var filter = new EmployeeQueryFilter(
            query.Query, query.Department, query.Location, query.Status, ParseId(query.ManagerId));

        var all = await employees.QueryAsync(caller.TenantKey, filter, ct);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var offset = DecodeCursor(query.Cursor);

        var page = all.Skip(offset).Take(pageSize).Select(MapListItem).ToList();
        var next = offset + page.Count;
        var hasMore = next < all.Count;

        return new CursorPage<EmployeeListItemDto>(
            page, hasMore ? EncodeCursor(next) : null, hasMore, pageSize, all.Count);
    }

    public async Task<EmployeeOverviewDto> GetOverviewAsync(
        MobileCallerContext caller, string employeeId, CancellationToken ct)
    {
        var employee = await LoadReadableAsync(caller, employeeId, ct);
        var manager = employee.ManagerEmployeeId is { } managerId
            ? await employees.GetAsync(caller.TenantKey, managerId, ct)
            : null;

        var canEdit = caller.Has(EditPermission);

        // Compensation is a separate section and a separate permission; it is omitted from the
        // section list rather than offered and then refused.
        var sections = new List<string> { "personal", "employment", "organisation", "contacts", "timeline" };
        if (caller.Has(CompensationPermission))
        {
            sections.Add("compensation");
        }

        return new EmployeeOverviewDto(
            MapListItem(employee),
            manager is null ? null : new PersonReferenceDto(
                manager.Id.ToString(), manager.DisplayName, manager.AvatarUrl, manager.EmployeeNumber),
            employee.JoinedOn,
            sections,
            [
                new ActionAvailabilityDto("edit", canEdit, canEdit ? null : "Permission required."),
                new ActionAvailabilityDto("request-change", true)
            ],
            null);
    }

    public async Task<EmployeePersonalDto> GetPersonalAsync(
        MobileCallerContext caller, string employeeId, CancellationToken ct)
    {
        var employee = await LoadReadableAsync(caller, employeeId, ct);
        var canEdit = caller.Has(EditPermission);

        // Date of birth is personal data: the owner sees it, everyone else needs the view
        // permission, and it is reported as masked rather than silently blanked.
        var canSeeDateOfBirth = caller.IsSelf(employeeId) || caller.Has(ViewPermission);

        return new EmployeePersonalDto(
            employee.Id.ToString(),
            employee.LegalName,
            employee.PreferredName,
            canSeeDateOfBirth ? employee.DateOfBirth : null,
            employee.GenderCode,
            employee.MaritalStatusCode,
            employee.Email,
            employee.Phone,
            [
                new FieldAccessDto("legalName", true, canEdit, false),
                new FieldAccessDto("dateOfBirth", canSeeDateOfBirth, canEdit, !canSeeDateOfBirth,
                    canSeeDateOfBirth ? null : "Restricted personal data."),
                new FieldAccessDto("email", true, canEdit, false),
                new FieldAccessDto("phone", true, canEdit, false)
            ],
            null);
    }

    public async Task<EmployeeEmploymentDto> GetEmploymentAsync(
        MobileCallerContext caller, string employeeId, CancellationToken ct)
    {
        var employee = await LoadReadableAsync(caller, employeeId, ct);

        return new EmployeeEmploymentDto(
            employee.Id.ToString(), employee.EmployeeNumber, employee.JoinedOn,
            employee.EmploymentTypeCode, employee.EmploymentStatusCode, employee.GradeCode,
            employee.ProbationEndsOn, null);
    }

    public async Task<EmployeeOrganisationDto> GetOrganisationAsync(
        MobileCallerContext caller, string employeeId, CancellationToken ct)
    {
        var employee = await LoadReadableAsync(caller, employeeId, ct);
        var manager = employee.ManagerEmployeeId is { } managerId
            ? await employees.GetAsync(caller.TenantKey, managerId, ct)
            : null;

        return new EmployeeOrganisationDto(
            employee.Id.ToString(), employee.CompanyCode, employee.DivisionCode,
            employee.DepartmentCode, employee.LocationCode,
            manager is null ? null : new PersonReferenceDto(
                manager.Id.ToString(), manager.DisplayName, manager.AvatarUrl, manager.EmployeeNumber),
            employee.CostCentre, null);
    }

    public async Task<CompensationSnapshotDto> GetCompensationAsync(
        MobileCallerContext caller, string employeeId, CancellationToken ct)
    {
        var employee = await LoadReadableAsync(caller, employeeId, ct);

        // Salary is never returned on a view permission alone.
        if (!caller.Has(CompensationPermission) && !caller.IsSelf(employeeId))
        {
            throw new MobileAccessDeniedException(CompensationPermission);
        }

        // The pay structure itself belongs to payroll, which is not built. Returning an empty
        // snapshot would read as "no salary"; this says plainly that the module is missing.
        throw new MobileFeatureNotReadyException(
            "Employee compensation", "payroll schema", "salary structure service");
    }

    public async Task<IReadOnlyList<EmployeeContactDto>> ListContactsAsync(
        MobileCallerContext caller, string employeeId, CancellationToken ct)
    {
        await LoadReadableAsync(caller, employeeId, ct);
        var contacts = await employees.GetContactsAsync(caller.TenantKey, ParseRequiredId(employeeId), ct);

        return contacts
            .Select(item => new EmployeeContactDto(
                item.Id.ToString(),
                item.Kind,
                Describe(item),
                item.IsPrimary,
                item.IsVerified,
                null))
            .ToList();
    }

    public async Task<IReadOnlyList<EmployeeDependantDto>> ListDependantsAsync(
        MobileCallerContext caller, string employeeId, CancellationToken ct)
    {
        await LoadReadableAsync(caller, employeeId, ct);
        var dependants = await employees.GetDependantsAsync(caller.TenantKey, ParseRequiredId(employeeId), ct);

        return dependants
            .Select(item => new EmployeeDependantDto(
                item.Id.ToString(), item.DisplayName, item.RelationCode,
                item.DateOfBirth, item.IsBenefitEligible, null))
            .ToList();
    }

    public async Task<CursorPage<EmployeeTimelineEntryDto>> GetTimelineAsync(
        MobileCallerContext caller, string employeeId, EmployeeTimelineQuery query, CancellationToken ct)
    {
        await LoadReadableAsync(caller, employeeId, ct);

        var entries = await employees.GetTimelineAsync(
            caller.TenantKey, ParseRequiredId(employeeId), query.EventType, query.From, query.To, ct);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var offset = DecodeCursor(query.Cursor);
        var page = entries.Skip(offset).Take(pageSize)
            .Select(item => new EmployeeTimelineEntryDto(
                item.Id.ToString(), item.EventType, item.Title, item.EffectiveOn, item.Summary,
                item.ActorId is null ? null : new PersonReferenceDto(item.ActorId, item.ActorName ?? "Unknown")))
            .ToList();

        var next = offset + page.Count;
        var hasMore = next < entries.Count;

        return new CursorPage<EmployeeTimelineEntryDto>(
            page, hasMore ? EncodeCursor(next) : null, hasMore, pageSize, entries.Count);
    }

    // ---- Writes -----------------------------------------------------------

    public async Task<MutationResultDto> UpdatePersonalAsync(
        MobileCallerContext caller, string employeeId, UpdateEmployeePersonalRequest request, CancellationToken ct)
    {
        Require(caller, EditPermission);
        var employee = await LoadAsync(caller, employeeId, ct);
        var now = DateTimeOffset.UtcNow;

        var updated = employee with
        {
            LegalFirstName = request.LegalFirstName.Trim(),
            LegalMiddleName = request.LegalMiddleName?.Trim(),
            LegalLastName = request.LegalLastName.Trim(),
            PreferredName = request.PreferredName?.Trim(),
            DateOfBirth = request.DateOfBirth ?? employee.DateOfBirth,
            GenderCode = request.Gender ?? employee.GenderCode,
            MaritalStatusCode = request.MaritalStatus ?? employee.MaritalStatusCode,
            NationalityCode = request.Nationality ?? employee.NationalityCode,
            Phone = request.Phone ?? employee.Phone,
            UpdatedUtc = now
        };

        await employees.UpsertAsync(updated, ct);
        await RecordAsync(caller, employee.Id, "DetailsChanged", "Personal details updated",
            "Name, date of birth or demographic fields were changed.", now, ct);

        return new MutationResultDto(employee.Id.ToString(), "Updated", null, now);
    }

    public async Task<MutationResultDto> UpdateEmploymentAsync(
        MobileCallerContext caller, string employeeId, UpdateEmployeeEmploymentRequest request, CancellationToken ct)
    {
        Require(caller, EmploymentPermission);
        var employee = await LoadAsync(caller, employeeId, ct);
        var now = DateTimeOffset.UtcNow;

        var statusChanged = request.EmploymentStatus is { Length: > 0 } &&
                            !string.Equals(request.EmploymentStatus, employee.EmploymentStatusCode, StringComparison.OrdinalIgnoreCase);

        // Payroll prices an employee by grade, so a grade that is not in the Grade master would
        // leave them with no salary structure at all.
        var gradeCode = await ResolveMasterCodeAsync(
            caller.TenantKey, "Grade", request.Grade, employee.GradeCode, "GRADE_UNKNOWN", ct);

        var updated = employee with
        {
            JoinedOn = request.JoinedOn ?? employee.JoinedOn,
            EmploymentTypeCode = request.EmploymentType ?? employee.EmploymentTypeCode,
            EmploymentStatusCode = request.EmploymentStatus ?? employee.EmploymentStatusCode,
            GradeCode = gradeCode,
            ProbationEndsOn = request.ProbationEndsOn ?? employee.ProbationEndsOn,
            LeavingOn = request.LeavingOn ?? employee.LeavingOn,
            UpdatedUtc = now
        };

        await employees.UpsertAsync(updated, ct);

        await RecordAsync(caller, employee.Id,
            statusChanged ? "StatusChanged" : "DetailsChanged",
            statusChanged ? $"Status changed to {updated.EmploymentStatusCode}" : "Employment details updated",
            request.ReasonCode is { Length: > 0 } ? $"Reason: {request.ReasonCode}" : "Employment fields were changed.",
            now, ct);

        return new MutationResultDto(employee.Id.ToString(), "Updated", null, now);
    }

    public async Task<MutationResultDto> UpdateOrganisationAsync(
        MobileCallerContext caller, string employeeId, UpdateEmployeeOrganisationRequest request, CancellationToken ct)
    {
        Require(caller, OrganisationPermission);
        var employee = await LoadAsync(caller, employeeId, ct);
        var now = DateTimeOffset.UtcNow;

        var managerId = ParseId(request.ManagerEmployeeId) ?? employee.ManagerEmployeeId;

        // A reporting line that points at itself would make the org tree cyclic at depth one.
        if (managerId == employee.Id)
        {
            throw new MobileValidationException("MANAGER_SELF", "An employee cannot report to themselves.");
        }

        if (managerId is { } candidateManager)
        {
            var manager = await employees.GetAsync(caller.TenantKey, candidateManager, ct)
                ?? throw new MobileValidationException("MANAGER_NOT_FOUND", "That manager is not an employee of this company.");

            if (manager.ManagerEmployeeId == employee.Id)
            {
                throw new MobileValidationException(
                    "MANAGER_CYCLE", "That manager already reports to this employee.");
            }
        }

        var transferred =
            (request.DepartmentCode is { Length: > 0 } && !string.Equals(request.DepartmentCode, employee.DepartmentCode, StringComparison.OrdinalIgnoreCase)) ||
            (request.LocationCode is { Length: > 0 } && !string.Equals(request.LocationCode, employee.LocationCode, StringComparison.OrdinalIgnoreCase));

        var divisionCode = await ResolveMasterCodeAsync(
            caller.TenantKey, "Division", request.OrganisationUnitCode, employee.DivisionCode, "DIVISION_UNKNOWN", ct);

        var updated = employee with
        {
            CompanyCode = request.CompanyCode ?? employee.CompanyCode,
            DivisionCode = divisionCode,
            DepartmentCode = request.DepartmentCode ?? employee.DepartmentCode,
            LocationCode = request.LocationCode ?? employee.LocationCode,
            ManagerEmployeeId = managerId,
            UpdatedUtc = now
        };

        await employees.UpsertAsync(updated, ct);

        await RecordAsync(caller, employee.Id,
            transferred ? "Transferred" : "DetailsChanged",
            transferred ? $"Moved to {updated.DepartmentCode}, {updated.LocationCode}" : "Organisation details updated",
            request.ReasonCode is { Length: > 0 } ? $"Reason: {request.ReasonCode}" : "Organisation fields were changed.",
            now, ct, request.EffectiveFrom);

        return new MutationResultDto(employee.Id.ToString(), "Updated", null, now);
    }

    public async Task<MutationResultDto> SaveContactAsync(
        MobileCallerContext caller, string employeeId, SaveEmployeeContactRequest request, CancellationToken ct)
    {
        // An employee may maintain their own contacts; changing someone else's needs the permission.
        if (!caller.IsSelf(employeeId))
        {
            Require(caller, EditPermission);
        }

        var employee = await LoadAsync(caller, employeeId, ct);
        var now = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(request.Phone) && string.IsNullOrWhiteSpace(request.Email))
        {
            throw new MobileValidationException(
                "CONTACT_UNREACHABLE", "A contact needs at least a phone number or an email address.");
        }

        var id = await employees.UpsertContactAsync(
            new EmployeeContactRecord(
                ParseId(request.Id) ?? 0, caller.TenantKey, employee.Id, request.Kind,
                request.Name.Trim(), request.Relationship, request.Phone, request.Email,
                request.DateOfBirth, request.IsPrimary, false, now),
            ct);

        return new MutationResultDto(id.ToString(), "Saved", null, now);
    }

    public async Task RemoveContactAsync(
        MobileCallerContext caller, string employeeId, string contactId, CancellationToken ct)
    {
        if (!caller.IsSelf(employeeId))
        {
            Require(caller, EditPermission);
        }

        var employee = await LoadAsync(caller, employeeId, ct);
        await employees.RemoveContactAsync(caller.TenantKey, employee.Id, ParseRequiredId(contactId), ct);
    }

    public async Task<CreateEmployeeResultDto> CreateAsync(
        MobileCallerContext caller, CreateEmployeeRequest request, CancellationToken ct)
    {
        Require(caller, CreatePermission);
        var now = DateTimeOffset.UtcNow;
        var number = request.EmployeeNumber.Trim();

        var existing = await employees.GetByNumberAsync(caller.TenantKey, number, ct);
        if (existing is not null)
        {
            throw new MobileValidationException(
                "EMPLOYEE_NUMBER_TAKEN", $"Employee number {request.EmployeeNumber} is already in use.");
        }

        // Placement codes must be real master values, so payroll, reports and the organisation
        // chart can resolve them. The stored code is the master's own spelling.
        var department = await ResolveMasterCodeAsync(caller.TenantKey, "Department", request.DepartmentCode, null, "DEPARTMENT_UNKNOWN", ct)
            ?? throw new MobileValidationException("DEPARTMENT_REQUIRED", "Choose a department.");
        var location = await ResolveMasterCodeAsync(caller.TenantKey, "Location", request.LocationCode, null, "LOCATION_UNKNOWN", ct)
            ?? throw new MobileValidationException("LOCATION_REQUIRED", "Choose a location.");
        var division = await ResolveMasterCodeAsync(caller.TenantKey, "Division", request.DivisionCode, null, "DIVISION_UNKNOWN", ct);
        var grade = await ResolveMasterCodeAsync(caller.TenantKey, "Grade", request.GradeCode, null, "GRADE_UNKNOWN", ct);
        var gender = await ResolveMasterCodeAsync(caller.TenantKey, "Gender", request.GenderCode, null, "GENDER_UNKNOWN", ct);

        long? managerId = null;
        if (!string.IsNullOrWhiteSpace(request.ManagerEmployeeId))
        {
            var manager = await employees.GetAsync(caller.TenantKey, ParseRequiredId(request.ManagerEmployeeId), ct)
                ?? throw new MobileValidationException("MANAGER_NOT_FOUND", "That manager is not an employee of this company.");
            managerId = manager.Id;
        }

        var employmentType = string.IsNullOrWhiteSpace(request.EmploymentType)
            ? "FULL_TIME"
            : request.EmploymentType.Trim().ToUpperInvariant();
        if (employmentType is not ("FULL_TIME" or "PART_TIME" or "CONTRACT" or "INTERN"))
        {
            throw new MobileValidationException(
                "EMPLOYMENT_TYPE_UNKNOWN", "Employment type must be full time, part time, contract or intern.");
        }

        if (request.DateOfBirth is { } dateOfBirth && dateOfBirth.AddYears(14) > request.JoinedOn)
        {
            throw new MobileValidationException("DATE_OF_BIRTH_INVALID", "Date of birth must be at least 14 years before joining.");
        }

        if (request.ProbationEndsOn is { } probationEnds && probationEnds <= request.JoinedOn)
        {
            throw new MobileValidationException("PROBATION_INVALID", "Probation must end after the joining date.");
        }

        var names = SplitName(request.LegalName);

        var id = await employees.UpsertAsync(
            new EmployeeRecord(
                0, caller.TenantKey, number,
                names.First, names.Middle, names.Last, null,
                request.DateOfBirth, gender, null, null,
                request.Email.Trim().ToLowerInvariant(), OptionalText(request.Phone), null,
                request.JoinedOn, employmentType, request.ProbationEndsOn is null ? "Active" : "Probation",
                grade, OptionalText(request.JobTitle), request.ProbationEndsOn,
                null, division, department, location,
                managerId, null, null, null,
                true, now, now),
            ct);

        await RecordAsync(caller, id, "Hired", "Joined the company",
            $"Created with employee number {request.EmployeeNumber}.", now, ct, request.JoinedOn);

        // Account creation is the identity module's job and is not triggered from here.
        return new CreateEmployeeResultDto(id.ToString(), null, null, "NotInvited");
    }

    public async Task<IReadOnlyList<EmployeeListItemDto>> CheckDuplicateAsync(
        MobileCallerContext caller, CreateEmployeeRequest request, CancellationToken ct)
    {
        Require(caller, CreatePermission);

        var matches = await employees.FindPossibleDuplicatesAsync(
            caller.TenantKey, request.Email, null, request.EmployeeNumber, ct);

        return matches.Select(MapListItem).ToList();
    }

    public async Task<MutationResultDto> CreateChangeRequestAsync(
        MobileCallerContext caller, string employeeId, CreateEmployeeChangeRequest request, CancellationToken ct)
    {
        var employee = await LoadReadableAsync(caller, employeeId, ct);

        if (request.Changes.Count == 0)
        {
            throw new MobileValidationException("NO_CHANGES", "A change request must name at least one field.");
        }

        var now = DateTimeOffset.UtcNow;

        // A bank account number is the one field in this generic table that must never sit at
        // rest in plain text — it is encrypted here, with the same protector the bank account
        // master itself uses, and only ever decrypted again inside DecideChangeRequestAsync.
        var storedChanges = request.Section == EmployeeChangeRequestSections.BankAccount
            ? ProtectBankChanges(request.Changes)
            : request.Changes;

        var id = await employees.AddChangeRequestAsync(
            new EmployeeChangeRequestRecord(
                0, caller.TenantKey, employee.Id, request.Section,
                JsonSerializer.Serialize(storedChanges, Json), request.Reason,
                "Pending", null, caller.UserId, null, now, null),
            ct);

        // Raised after the request is stored. This is the only route by which an employee's own
        // correction reaches HR — CR-7 means they never write the master record — so without a
        // notification the request sits Pending until somebody happens to open their file.
        var fields = request.Changes.Count;

        await notifications.PublishToPermissionAsync(
            new ITF.Shared.NotificationRequest(
                caller.TenantKey,
                NotificationCategory,
                "Profile change requested",
                $"{employee.DisplayName} asked to change {fields} " +
                $"{(fields == 1 ? "field" : "fields")} in their {request.Section} details. " +
                $"{request.Reason?.Trim()}".TrimEnd(),
                $"/employees/{employee.Id}",
                "Normal",
                NotificationSubject,
                id.ToString()),
            EditPermission,
            ct);

        return new MutationResultDto(id.ToString(), "Pending", null, now);
    }

    public async Task<EmployeeChangeRequestDto> GetChangeRequestAsync(
        MobileCallerContext caller, string changeRequestId, CancellationToken ct)
    {
        var request = await employees.GetChangeRequestAsync(
            caller.TenantKey, ParseRequiredId(changeRequestId), ct)
            ?? throw new MobileNotFoundException("That change request no longer exists.");

        // A request about someone else's record is only readable with the view permission.
        if (!caller.Has(ViewPermission) && !caller.IsSelf(request.EmployeeId.ToString()))
        {
            throw new MobileAccessDeniedException(ViewPermission);
        }

        var changes = DisplayChanges(request.Section, request.ChangesJson);

        return new EmployeeChangeRequestDto(
            request.Id.ToString(),
            request.EmployeeId.ToString(),
            request.Section,
            changes,
            new StatusDto(request.Status, request.Status, ToneFor(request.Status), request.DecidedUtc),
            [],
            [new WorkflowStepDto("review", "HR review", request.Status, null, null, request.DecidedUtc)],
            request.SubmittedUtc,
            null);
    }

    public async Task<IReadOnlyList<EmployeeChangeRequestListItemDto>> ListChangeRequestsAsync(
        MobileCallerContext caller, EmployeeChangeRequestQuery query, CancellationToken ct)
    {
        Require(caller, ViewPermission);

        var requests = await employees.QueryChangeRequestsAsync(caller.TenantKey, query.Status, ct);
        var items = new List<EmployeeChangeRequestListItemDto>(requests.Count);

        foreach (var item in requests)
        {
            var person = await employees.GetAsync(caller.TenantKey, item.EmployeeId, ct);

            items.Add(new EmployeeChangeRequestListItemDto(
                item.Id.ToString(),
                item.EmployeeId.ToString(),
                person?.DisplayName ?? $"Employee {item.EmployeeId}",
                person?.EmployeeNumber,
                person?.DepartmentCode,
                item.Section,
                DisplayChanges(item.Section, item.ChangesJson),
                item.Reason,
                new StatusDto(item.Status, item.Status, ToneFor(item.Status), item.DecidedUtc),
                item.SubmittedUtc));
        }

        return items;
    }

    public async Task<MutationResultDto> DecideChangeRequestAsync(
        MobileCallerContext caller, string changeRequestId, DecideEmployeeChangeRequest request, CancellationToken ct)
    {
        Require(caller, EditPermission);

        var id = ParseRequiredId(changeRequestId);
        var stored = await employees.GetChangeRequestAsync(caller.TenantKey, id, ct)
            ?? throw new MobileNotFoundException("That change request no longer exists.");

        if (stored.Status != "Pending")
        {
            throw new MobileValidationException(
                "ALREADY_DECIDED", $"This request was already {stored.Status.ToLowerInvariant()}.");
        }

        var employee = await employees.GetAsync(caller.TenantKey, stored.EmployeeId, ct)
            ?? throw new MobileNotFoundException("That employee no longer exists.");

        var now = DateTimeOffset.UtcNow;
        var approve = string.Equals(request.Decision, "approve", StringComparison.OrdinalIgnoreCase);

        if (approve)
        {
            var changes = JsonSerializer.Deserialize<List<FieldChangeDto>>(stored.ChangesJson, Json) ?? [];

            // Applied through the same service HR itself uses to write the field, so a change
            // request can never take a path the direct edit screens do not also enforce.
            switch (stored.Section)
            {
                case EmployeeChangeRequestSections.Personal:
                    await ApplyPersonalChangesAsync(caller, employee, changes, ct);
                    break;
                case EmployeeChangeRequestSections.BankAccount:
                    await ApplyBankChangesAsync(caller, employee, changes, ct);
                    break;
                default:
                    throw new MobileValidationException(
                        "SECTION_NOT_APPLIABLE",
                        $"Changes to {stored.Section} cannot be applied automatically yet.");
            }
        }

        await employees.UpdateChangeRequestStatusAsync(
            caller.TenantKey, id, approve ? "Approved" : "Rejected", request.Comment, caller.UserId, now, ct);

        if (employee.AccountId is { } accountId)
        {
            await notifications.PublishAsync(
                new ITF.Shared.NotificationRequest(
                    caller.TenantKey,
                    NotificationCategory,
                    approve ? "Your profile change was approved" : "Your profile change was rejected",
                    approve
                        ? $"Your {stored.Section} change request has been applied."
                        : $"Your {stored.Section} change request was declined." +
                          (string.IsNullOrWhiteSpace(request.Comment) ? "" : $" {request.Comment.Trim()}"),
                    "/menu/profile",
                    "Normal",
                    NotificationSubject,
                    id.ToString()),
                [accountId],
                ct);
        }

        return new MutationResultDto(id.ToString(), approve ? "Approved" : "Rejected", null, now);
    }

    // ---- Helpers ----------------------------------------------------------

    private async Task<EmployeeRecord> LoadAsync(
        MobileCallerContext caller, string employeeId, CancellationToken ct) =>
        await employees.GetAsync(caller.TenantKey, ParseRequiredId(employeeId), ct)
        ?? throw new MobileNotFoundException("That employee no longer exists.");

    /// <summary>Loads an employee the caller is allowed to read: their own record, or any with the view permission.</summary>
    private async Task<EmployeeRecord> LoadReadableAsync(
        MobileCallerContext caller, string employeeId, CancellationToken ct)
    {
        if (!caller.IsSelf(employeeId))
        {
            Require(caller, ViewPermission);
        }

        return await LoadAsync(caller, employeeId, ct);
    }

    private static string? OptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ---- Change requests: bank account number handling ---------------------

    private List<FieldChangeDto> ProtectBankChanges(IReadOnlyList<FieldChangeDto> changes) =>
        changes
            .Select(change => change.Field == "AccountNumber" && !string.IsNullOrWhiteSpace(change.RequestedValue)
                ? change with { RequestedValue = protector.Protect(BankChangeRequestProtectionPurpose, change.RequestedValue) }
                : change)
            .ToList();

    /// <summary>What a reader is shown: every field as stored, except a bank account number,
    /// which is decrypted only long enough to mask it to its last four digits.</summary>
    private List<FieldChangeDto> DisplayChanges(string section, string changesJson)
    {
        var changes = JsonSerializer.Deserialize<List<FieldChangeDto>>(changesJson, Json) ?? [];

        if (section != EmployeeChangeRequestSections.BankAccount)
        {
            return changes;
        }

        return changes
            .Select(change => change.Field == "AccountNumber" && !string.IsNullOrWhiteSpace(change.RequestedValue)
                ? change with { RequestedValue = MaskAccountNumber(Unprotect(change.RequestedValue)) }
                : change)
            .ToList();
    }

    private string Unprotect(string protectedValue)
    {
        try
        {
            return protector.Unprotect(BankChangeRequestProtectionPurpose, protectedValue);
        }
        catch (CryptographicException)
        {
            // A value protected under an older data-protection key set is unreadable rather than
            // corrupt; showing "unavailable" is honest, where a thrown 500 would not be.
            return "unavailable";
        }
    }

    private static string MaskAccountNumber(string accountNumber)
    {
        var digits = accountNumber.Trim();
        return digits.Length <= 4 ? digits : $"•••• {digits[^4..]}";
    }

    private async Task ApplyPersonalChangesAsync(
        MobileCallerContext caller, EmployeeRecord employee, IReadOnlyList<FieldChangeDto> changes, CancellationToken ct)
    {
        var request = new UpdateEmployeePersonalRequest
        {
            LegalFirstName = employee.LegalFirstName,
            LegalMiddleName = employee.LegalMiddleName,
            LegalLastName = employee.LegalLastName,
            PreferredName = employee.PreferredName,
            DateOfBirth = employee.DateOfBirth,
            Gender = employee.GenderCode,
            MaritalStatus = employee.MaritalStatusCode,
            Nationality = employee.NationalityCode,
            Phone = employee.Phone
        };

        foreach (var change in changes)
        {
            var value = OptionalText(change.RequestedValue);

            switch (change.Field)
            {
                case "Phone": request.Phone = value; break;
                case "PreferredName": request.PreferredName = value; break;
                case "MaritalStatus": request.MaritalStatus = value; break;
            }
        }

        await UpdatePersonalAsync(caller, employee.Id.ToString(), request, ct);
    }

    private async Task ApplyBankChangesAsync(
        MobileCallerContext caller, EmployeeRecord employee, IReadOnlyList<FieldChangeDto> changes, CancellationToken ct)
    {
        var current = await bankAccounts.GetEmployeeAccountAsync(caller, employee.Id.ToString(), ct);

        var request = new SaveEmployeeBankAccountRequest
        {
            EmployeeId = employee.Id.ToString(),
            BankName = current?.BankName ?? string.Empty,
            AccountHolderName = current?.AccountHolderName ?? string.Empty,
            Ifsc = current?.Ifsc ?? string.Empty,
            AccountType = current?.AccountType ?? BankAccountTypes.Savings
        };

        foreach (var change in changes)
        {
            var value = OptionalText(change.RequestedValue);

            switch (change.Field)
            {
                case "BankName" when value is not null: request.BankName = value; break;
                case "AccountHolderName" when value is not null: request.AccountHolderName = value; break;
                case "Ifsc" when value is not null: request.Ifsc = value; break;
                case "AccountType" when value is not null: request.AccountType = value; break;
                // The only field stored encrypted — decrypted here, transiently, to hand to the
                // bank account service, which re-encrypts it for storage.
                case "AccountNumber" when value is not null: request.AccountNumber = Unprotect(value); break;
            }
        }

        await bankAccounts.SaveEmployeeAccountAsync(caller, request, ct);
    }

    private static void Require(MobileCallerContext caller, string permission)
    {
        if (!caller.Has(permission))
        {
            throw new MobileAccessDeniedException(permission);
        }
    }

    /// <summary>
    /// Resolves a requested master code to the master's own spelling. No request keeps the current
    /// code; a code that is not active in the master is refused, unless it is the one already held.
    /// </summary>
    private async Task<string?> ResolveMasterCodeAsync(
        string tenantKey, string category, string? requested, string? current, string errorCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return current;
        }

        var code = requested.Trim();
        if (string.Equals(code, current, StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }

        var options = await masters.GetActiveAsync(tenantKey, category, ct);
        var match = options.FirstOrDefault(option => string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? throw new MobileValidationException(errorCode, $"'{code}' is not an active {category} in the {category} master.");

        return match.Code;
    }

    private Task RecordAsync(
        MobileCallerContext caller, long employeeId, string eventType, string title, string summary,
        DateTimeOffset now, CancellationToken ct, DateOnly? effectiveOn = null) =>
        employees.AddTimelineAsync(
            new EmployeeTimelineRecord(
                0, caller.TenantKey, employeeId, eventType, title, summary,
                effectiveOn ?? DateOnly.FromDateTime(now.UtcDateTime),
                caller.UserId, null, now),
            ct);

    private static EmployeeListItemDto MapListItem(EmployeeRecord employee) => new(
        employee.Id.ToString(),
        employee.EmployeeNumber,
        employee.DisplayName,
        employee.AvatarUrl,
        employee.JobTitle ?? string.Empty,
        employee.DepartmentCode,
        employee.LocationCode,
        new StatusDto(
            employee.EmploymentStatusCode,
            employee.EmploymentStatusCode,
            employee.IsActive ? "success" : "neutral"),
        null);

    private static string Describe(EmployeeContactRecord contact)
    {
        var reach = contact.Phone ?? contact.Email ?? string.Empty;
        return string.IsNullOrWhiteSpace(contact.Relationship)
            ? $"{contact.Name} · {reach}"
            : $"{contact.Name} ({contact.Relationship}) · {reach}";
    }

    private static string ToneFor(string status) => status switch
    {
        "Approved" => "success",
        "Rejected" => "danger",
        _ => "pending"
    };

    private static (string First, string? Middle, string Last) SplitName(string legalName)
    {
        var parts = legalName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            0 => ("Unknown", null, "Unknown"),
            1 => (parts[0], null, parts[0]),
            2 => (parts[0], null, parts[1]),
            _ => (parts[0], string.Join(' ', parts[1..^1]), parts[^1])
        };
    }

    private static long? ParseId(string? value) =>
        long.TryParse(value, out var parsed) ? parsed : null;

    private static long ParseRequiredId(string value) =>
        ParseId(value) ?? throw new MobileNotFoundException("That identifier is not valid.");

    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        try
        {
            return int.TryParse(
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor)), out var offset)
                ? Math.Max(0, offset)
                : 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }

    private static string EncodeCursor(int offset) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(offset.ToString()));
}
