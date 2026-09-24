using System.Globalization;
using System.Security.Cryptography;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.Contracts.Mobile.Employee;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.VO.Employee;

namespace Vispl.HRManagement.BM.Employee;

/// <summary>
/// Whether an employee may sign in to the mobile app.
///
/// Being hired does not grant a login. Someone has to decide that this person may carry the
/// company's attendance, leave and salary data on a phone, and revoke it when they leave — so
/// the grant is an explicit HR action with an audit trail behind it, not a side effect of
/// provisioning an employee record.
/// </summary>
public sealed class EmployeeMobileAccessService(
    IEmployeeRepository employees,
    IAccessAccountService accounts,
    IAccessRoleService roles,
    Vispl.HRManagement.BM.ITF.Shared.INotificationPublisher notifications) : IEmployeeMobileAccessService
{
    private const string ViewPermission = "EMPLOYEES.VIEW";
    private const string ManagePermission = "ACCOUNTS.MANAGE";

    /// <summary>Notification category for everything this service raises.</summary>
    private const string NotificationCategory = "Account";

    /// <summary>Subject type, so a repeat about the same account is not stacked in an inbox.</summary>
    private const string NotificationSubject = "MobileAccess";

    public async Task<IReadOnlyList<EmployeeMobileAccessDto>> GetAsync(
        MobileCallerContext caller, MobileAccessQuery query, CancellationToken ct)
    {
        Require(caller, ViewPermission);

        var staff = await employees.QueryAsync(
            caller.TenantKey,
            new EmployeeQueryFilter(query.Search, null, null, null, null, ActiveOnly: true),
            ct);

        var existing = await accounts.GetAllAsync(caller.TenantKey, AccountPortal.Employee, ct);

        // Accounts are matched to employees by employee number, which is the one identifier both
        // sides carry. An account with no number cannot be attributed and is left out rather
        // than guessed at.
        var byNumber = existing
            .Where(account => !string.IsNullOrWhiteSpace(account.EmployeeNumber))
            .GroupBy(account => account.EmployeeNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var rows = staff
            .Select(person => Describe(person, byNumber.GetValueOrDefault(person.EmployeeNumber)))
            .ToList();

        if (!string.IsNullOrWhiteSpace(query.State))
        {
            rows = rows.Where(row =>
                string.Equals(row.State, query.State, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Those waiting on a decision first: the list exists to be worked through.
        return rows
            .OrderByDescending(row => row.State == MobileAccessStates.NotGranted)
            .ThenBy(row => row.DisplayName)
            .ToList();
    }

    public async Task<GrantMobileAccessResultDto> GrantAsync(
        MobileCallerContext caller, string employeeId, GrantMobileAccessRequest request, CancellationToken ct)
    {
        Require(caller, ManagePermission);

        var person = await RequireEmployeeAsync(caller, employeeId, ct);

        var email = (request.Email ?? person.Email)?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new MobileValidationException(
                "EMAIL_REQUIRED",
                "This employee has no email address, so no account can be created. Add one first.");
        }

        var existing = await FindAccountAsync(caller, person, ct);
        if (existing is not null)
        {
            throw new MobileValidationException(
                "ALREADY_GRANTED",
                $"{person.EmployeeNumber} already has mobile access as '{existing.UserName}'.");
        }

        var userName = string.IsNullOrWhiteSpace(request.UserName)
            ? DeriveUserName(person)
            : request.UserName.Trim();

        var role = await ResolveRoleAsync(caller, request.RoleCode, ct);
        var temporaryPassword = GenerateTemporaryPassword();

        // Most people this company hires applied for the job, and applying already gave them a
        // login on this address. An address belongs to one account, so the applicant's login
        // becomes the employee's login rather than a second account being made for one person.
        var applicant = await FindAccountByEmailAsync(caller, email, ct);

        if (applicant is not null && applicant.Portal != AccountPortal.Candidate)
        {
            throw new MobileValidationException(
                "EMAIL_IN_USE",
                $"'{email}' already signs in as '{applicant.UserName}' on the {applicant.Portal} portal. " +
                "Give the employee a different address, or change the existing account instead.");
        }

        // Either way the account ends up on the Employee portal, so this login reaches the
        // mobile app and self service — never the HR portal or /admin.
        var account = applicant is null
            ? await accounts.CreateAsync(new CreateAccessAccountRequest
            {
                TenantKey = caller.TenantKey,
                Portal = AccountPortal.Employee,
                DisplayName = DisplayName(person),
                UserName = userName,
                Email = email,
                EmployeeNumber = person.EmployeeNumber,
                TemporaryPassword = temporaryPassword,
                IsActive = true,
                MobileAccessEnabled = true,
                RoleIds = [role.Id]
            }, caller.UserId, ct)
            : await accounts.TransferPortalAsync(new TransferAccountPortalRequest
            {
                AccountId = applicant.Id,
                Portal = AccountPortal.Employee,
                UserName = userName,
                EmployeeNumber = person.EmployeeNumber,
                TemporaryPassword = temporaryPassword,
                MobileAccessEnabled = true,
                RoleIds = [role.Id]
            }, caller.UserId, ct);

        await EnsureLinkedAsync(person, account.Id, ct);

        // Written to an account that has never been used, so nobody reads it now — it is
        // waiting when they first sign in. The credentials themselves are handed over out of
        // band by whoever granted access; this says what the login is for, which a username
        // and a temporary password on their own do not.
        await notifications.PublishAsync(
            new ITF.Shared.NotificationRequest(
                caller.TenantKey,
                NotificationCategory,
                "Mobile access granted",
                $"Welcome. You can now use the Kapsons app to see your attendance and payslips, " +
                $"apply for leave and request a gate pass. Your access level is {role.DisplayName}.",
                "/",
                "Normal",
                NotificationSubject,
                $"{person.Id}:granted"),
            [account.Id],
            ct);

        return new GrantMobileAccessResultDto(
            employeeId, account.UserName, temporaryPassword,
            MobileAccessStates.AwaitingFirstSignIn, role.DisplayName);
    }

    public async Task<EmployeeMobileAccessDto> SetActiveAsync(
        MobileCallerContext caller, string employeeId, bool isActive, CancellationToken ct)
    {
        Require(caller, ManagePermission);

        var person = await RequireEmployeeAsync(caller, employeeId, ct);
        var account = await FindAccountAsync(caller, person, ct)
                      ?? throw new MobileValidationException(
                          "NOT_GRANTED", "This employee has no mobile access to change.");

        // Captured before the write, so the notification below fires on the transition rather
        // than on every call. Restoring an already-active account is a no-op and should read
        // as one.
        var wasActive = account.IsActive;

        await accounts.SetActiveAsync(account.Id, isActive, caller.UserId, ct);

        // An Employee-portal account exists only to reach a phone, so the two move together
        // here. The flags stay separate underneath because an HR account needs them apart.
        await accounts.SetMobileAccessAsync(account.Id, isActive, caller.UserId, ct);

        if (isActive)
        {
            // Also repairs an account granted before the link was written: switching access
            // back on is the moment to make sure it reaches the person's own data.
            await EnsureLinkedAsync(person, account.Id, ct);
        }

        if (isActive && !wasActive)
        {
            await notifications.PublishAsync(
                new ITF.Shared.NotificationRequest(
                    caller.TenantKey,
                    NotificationCategory,
                    "Mobile access restored",
                    "Your access to the Kapsons app has been switched back on.",
                    "/",
                    "Normal",
                    NotificationSubject,
                    $"{person.Id}:restored:{DateTimeOffset.UtcNow:yyyyMMddHHmm}"),
                [account.Id],
                ct);
        }

        // A suspension is deliberately silent. The account it would be addressed to is the one
        // just switched off, so the person cannot sign in to read it — and telling them through
        // a channel they have lost is worse than telling them nothing. Whoever suspended the
        // account owns saying so.

        var refreshed = await FindAccountAsync(caller, person, ct);
        return Describe(person, refreshed);
    }

    // ---- Helpers ----------------------------------------------------------

    /// <summary>
    /// Points the employee record back at the account.
    ///
    /// Without it the login works and reaches nothing: every self-service read resolves the
    /// caller's own employee by account id, so the person signs in and finds no attendance, no
    /// leave and no payslip of their own.
    /// </summary>
    private async Task EnsureLinkedAsync(EmployeeRecord person, string accountId, CancellationToken ct)
    {
        if (string.Equals(person.AccountId, accountId, StringComparison.Ordinal))
        {
            return;
        }

        await employees.UpsertAsync(
            person with { AccountId = accountId, UpdatedUtc = DateTimeOffset.UtcNow }, ct);
    }

    /// <summary>
    /// An account with no role can sign in and do nothing, which looks like a broken app rather
    /// than a permissions decision — so a grant always attaches one, defaulting to plain
    /// self-service. Only Employee-portal roles are offered: this route must not be a way to
    /// hand somebody HR-portal access.
    /// </summary>
    private async Task<AccessRoleDto> ResolveRoleAsync(
        MobileCallerContext caller, string? roleCode, CancellationToken ct)
    {
        var available = await roles.GetAllAsync(caller.TenantKey, AccountPortal.Employee, ct);
        var wanted = string.IsNullOrWhiteSpace(roleCode) ? MobileAccessRoles.Default : roleCode.Trim();

        return available.FirstOrDefault(role =>
                   string.Equals(role.RoleCode, wanted, StringComparison.OrdinalIgnoreCase) &&
                   role.IsActive)
               ?? throw new MobileValidationException(
                   "ROLE_NOT_FOUND",
                   $"'{wanted}' is not an available employee role. Choose one of: " +
                   string.Join(", ", available.Where(role => role.IsActive).Select(role => role.RoleCode)));
    }

    private static EmployeeMobileAccessDto Describe(EmployeeRecord person, AccessAccountDto? account)
    {
        var (state, explanation) = Resolve(person, account);

        return new EmployeeMobileAccessDto(
            person.Id.ToString(CultureInfo.InvariantCulture),
            person.EmployeeNumber,
            DisplayName(person),
            person.DepartmentCode,
            person.JobTitle,
            person.Email,
            person.JoinedOn,
            state,
            state switch
            {
                MobileAccessStates.Active => "success",
                MobileAccessStates.AwaitingFirstSignIn => "info",
                MobileAccessStates.Suspended => "warning",
                MobileAccessStates.Blocked => "danger",
                _ => "muted"
            },
            account?.UserName,
            account?.MustChangePassword ?? false,
            account?.LastLoginUtc,
            explanation,
            account?.RoleNames ?? []);
    }

    private static (string State, string? Explanation) Resolve(
        EmployeeRecord person, AccessAccountDto? account)
    {
        if (account is null)
        {
            return string.IsNullOrWhiteSpace(person.Email)
                ? (MobileAccessStates.Blocked, "No email address on the employee record.")
                : (MobileAccessStates.NotGranted, "Hired, but not yet given mobile access.");
        }

        if (!account.IsActive)
        {
            return (MobileAccessStates.Suspended, "Mobile access has been switched off.");
        }

        return account.LastLoginUtc is null
            ? (MobileAccessStates.AwaitingFirstSignIn, "Granted; the employee has not signed in yet.")
            : (MobileAccessStates.Active, null);
    }

    /// <summary>
    /// Any account in this client already holding the address, on any portal. Searched across
    /// portals on purpose: the collision that matters is with the candidate login the person
    /// got when they applied.
    /// </summary>
    private async Task<AccessAccountDto?> FindAccountByEmailAsync(
        MobileCallerContext caller, string email, CancellationToken ct)
    {
        var all = await accounts.GetAllAsync(caller.TenantKey, null, ct);

        return all.FirstOrDefault(account =>
            string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<AccessAccountDto?> FindAccountAsync(
        MobileCallerContext caller, EmployeeRecord person, CancellationToken ct)
    {
        var existing = await accounts.GetAllAsync(caller.TenantKey, AccountPortal.Employee, ct);

        return existing.FirstOrDefault(account =>
            string.Equals(account.EmployeeNumber, person.EmployeeNumber, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<EmployeeRecord> RequireEmployeeAsync(
        MobileCallerContext caller, string employeeId, CancellationToken ct)
    {
        if (!long.TryParse(employeeId, out var id))
        {
            throw new MobileNotFoundException($"Employee {employeeId} was not found.");
        }

        return await employees.GetAsync(caller.TenantKey, id, ct)
               ?? throw new MobileNotFoundException($"Employee {employeeId} was not found.");
    }

    private static string DisplayName(EmployeeRecord person) =>
        string.IsNullOrWhiteSpace(person.PreferredName)
            ? $"{person.LegalFirstName} {person.LegalLastName}".Trim()
            : person.PreferredName;

    /// <summary>
    /// A username from the employee number, which is unique within the client and stable. Names
    /// collide; employee numbers do not.
    /// </summary>
    private static string DeriveUserName(EmployeeRecord person) =>
        new(person.EmployeeNumber
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '.')
            .ToArray());

    /// <summary>
    /// A password the employee replaces at first sign-in. Random rather than derived from
    /// anything about the person, so it cannot be guessed from their name or number.
    /// </summary>
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!#$%&*+-";

        var pool = upper + lower + digits + symbols;

        // One of each class first so the policy is satisfied by construction, then padded out.
        var characters = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
        };

        while (characters.Count < 14)
        {
            characters.Add(pool[RandomNumberGenerator.GetInt32(pool.Length)]);
        }

        // Shuffled so the classes are not always in the same positions.
        for (var index = characters.Count - 1; index > 0; index--)
        {
            var swap = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swap]) = (characters[swap], characters[index]);
        }

        return new string([.. characters]);
    }

    private static void Require(MobileCallerContext caller, string permission)
    {
        if (!caller.Has(permission))
        {
            throw new MobileAccessDeniedException(permission);
        }
    }
}
