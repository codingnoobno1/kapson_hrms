using Vispl.HRManagement.BM.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.VO.Employee;

namespace Vispl.HRManagement.BM.Employee;

public sealed class EmployeeProvisioning(IEmployeeRepository employees) : IEmployeeProvisioning
{
    public async Task<long> HireAsync(HireRequest request, CancellationToken cancellationToken = default)
    {
        // Idempotent by candidate: a replayed completion returns the employee already created.
        var existing = await employees.GetByCandidateAsync(request.TenantKey, request.CandidateId, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var now = DateTimeOffset.UtcNow;
        var names = SplitName(request.FullName);

        // Derived from the candidate id, so it is unique within the tenant without a counter
        // that two concurrent hires could read at the same time.
        var employeeNumber = $"EMP-{request.CandidateId:D5}";

        var id = await employees.UpsertAsync(
            new EmployeeRecord(
                0,
                request.TenantKey,
                employeeNumber,
                names.First, names.Middle, names.Last, null,
                null, null, null, null,
                request.Email, request.Phone, null,
                request.JoinedOn,
                request.EmploymentTypeCode,
                "Active",
                null,
                request.JobTitle,
                null,
                null, null, request.DepartmentCode, request.LocationCode,
                null, null, null,
                request.CandidateId,
                true, now, now),
            cancellationToken);

        await employees.AddTimelineAsync(
            new EmployeeTimelineRecord(
                0, request.TenantKey, id, "Hired", "Joined the company",
                $"Hired through recruitment as {request.JobTitle ?? "a new joiner"}.",
                request.JoinedOn, null, "System", now),
            cancellationToken);

        return id;
    }

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
