using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Mobile.Employee;

/// <summary>
/// An employee's standing with the mobile app.
///
/// Hiring creates an employee record; it does not create a login. Somebody has to decide that
/// this person may carry the company's attendance and salary data on a phone, and that decision
/// is recorded here rather than implied.
/// </summary>
public sealed record EmployeeMobileAccessDto(
    string EmployeeId,
    string EmployeeNumber,
    string DisplayName,
    string? DepartmentCode,
    string? JobTitle,
    string? Email,
    DateOnly? JoinedOn,
    string State,
    string StateTone,
    string? UserName,
    bool MustChangePassword,
    DateTimeOffset? LastLoginUtc,
    string? Explanation,

    /// <summary>What the login can reach. Empty until access is granted.</summary>
    IReadOnlyList<string> RoleNames);

public static class MobileAccessStates
{
    /// <summary>No account exists. HR has not granted mobile access.</summary>
    public const string NotGranted = "NotGranted";

    /// <summary>An account exists and the person has never signed in.</summary>
    public const string AwaitingFirstSignIn = "AwaitingFirstSignIn";

    public const string Active = "Active";

    /// <summary>The account exists but has been switched off.</summary>
    public const string Suspended = "Suspended";

    /// <summary>The employee has no email address, so no account can be created.</summary>
    public const string Blocked = "Blocked";
}

public sealed record GrantMobileAccessResultDto(
    string EmployeeId,
    string UserName,
    /// <summary>
    /// Shown to the granting HR user once and never stored in readable form. They pass it to the
    /// employee, who must replace it at first sign-in.
    /// </summary>
    string TemporaryPassword,
    string State,

    /// <summary>The Employee-portal role the new login carries.</summary>
    string RoleName);

public sealed class GrantMobileAccessRequest
{
    /// <summary>Blank derives one from the employee number.</summary>
    [StringLength(64)] public string? UserName { get; set; }

    /// <summary>Blank uses the address on the employee record.</summary>
    [EmailAddress, StringLength(256)] public string? Email { get; set; }

    /// <summary>
    /// The Employee-portal role the login carries. Blank means <see cref="MobileAccessRoles.Default"/>
    /// — self-service only. A manager who approves their team's leave needs a wider role, and that
    /// is a deliberate choice rather than something the grant should infer.
    /// </summary>
    [StringLength(60)] public string? RoleCode { get; set; }
}

public static class MobileAccessRoles
{
    /// <summary>Standard self-service: the employee's own attendance, leave and payslips.</summary>
    public const string Default = "EMPLOYEE";
}

public sealed class MobileAccessQuery
{
    /// <summary>Filter by state, or blank for everyone.</summary>
    [StringLength(30)] public string? State { get; set; }

    [StringLength(120)] public string? Search { get; set; }
}
