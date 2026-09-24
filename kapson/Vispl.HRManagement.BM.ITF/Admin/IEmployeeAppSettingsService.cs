using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

/// <summary>What HR allows in the employee app, per tenant.</summary>
public interface IEmployeeAppSettingsService
{
    /// <summary>The stored settings, or the defaults when HR has never saved any. No permission needed:
    /// every signed-in employee's app has to know what it may show.</summary>
    Task<EmployeeAppSettingsDto> GetAsync(string tenantKey, CancellationToken cancellationToken = default);

    /// <summary>Needs MASTERS.MANAGE.</summary>
    Task SaveAsync(MobileCallerContext caller, EmployeeAppSettingsDto settings, CancellationToken cancellationToken = default);
}
