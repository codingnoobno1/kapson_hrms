using Vispl.HRManagement.DL.ITF.Employee;

namespace Vispl.HRManagement.BM.Shared;

/// <summary>
/// Who a workflow notification is about, and where to send one addressed to them.
///
/// <see cref="AccountId"/> is null for an employee HR has not granted mobile access to. That is
/// a normal state rather than an error: the employee record exists, there is simply no inbox to
/// write to. A caller skips the personal notification and completes its decision regardless —
/// nobody's leave should fail to be approved because they have no phone.
/// </summary>
public readonly record struct NotificationTarget(string? AccountId, string DisplayName)
{
    /// <summary>True when there is somewhere to send a personal notification.</summary>
    public bool CanBeNotified => !string.IsNullOrWhiteSpace(AccountId);
}

/// <summary>
/// Resolves the notification target for an employee.
///
/// Shared because leave, attendance, gate passes and movement all need the same two facts about
/// the same person, and four private copies of it would drift.
/// </summary>
public static class NotificationTargets
{
    public static async Task<NotificationTarget> DescribeAsync(
        this IEmployeeRepository employees,
        string tenantKey,
        long employeeId,
        CancellationToken cancellationToken = default)
    {
        var person = await employees.GetAsync(tenantKey, employeeId, cancellationToken);

        // EmployeeRecord.DisplayName honours PreferredName. Rebuilding the name from the legal
        // parts here would announce someone by a name no other screen calls them.
        return person is null
            ? new NotificationTarget(null, "An employee")
            : new NotificationTarget(person.AccountId, person.DisplayName);
    }
}
