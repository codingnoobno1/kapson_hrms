using Vispl.HRManagement.Contracts.Mobile.Home;

namespace Vispl.HRManagement.BM.ITF.Mobile;

/// <summary>
/// The employee app's "My profile" sheets. Every read is the caller's own record; there is no
/// employee id parameter anywhere, by design.
/// </summary>
public interface IEmployeeSelfService
{
    Task<EmployeeAppFeaturesDto> GetFeaturesAsync(MobileCallerContext caller, CancellationToken ct);

    Task<MySalaryDto> GetMySalaryAsync(MobileCallerContext caller, CancellationToken ct);

    Task<MyBankAccountDto> GetMyBankAccountAsync(MobileCallerContext caller, CancellationToken ct);

    Task<MyOnboardingDto> GetMyOnboardingAsync(MobileCallerContext caller, CancellationToken ct);
}
