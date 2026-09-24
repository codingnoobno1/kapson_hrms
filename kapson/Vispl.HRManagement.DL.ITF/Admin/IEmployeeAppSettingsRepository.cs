using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

/// <summary>The tenant's employee app settings row, or nothing — which means the defaults.</summary>
public interface IEmployeeAppSettingsRepository
{
    Task<EmployeeAppSettingsRecord?> GetAsync(string tenantKey, CancellationToken cancellationToken = default);

    Task UpsertAsync(EmployeeAppSettingsRecord record, CancellationToken cancellationToken = default);
}
