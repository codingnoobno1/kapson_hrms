using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Employee.Controllers;

/// <summary>
/// The employee app's "My profile" sheets and the feature switches HR controls. The caller's own
/// record only — there is no employee id on any of these routes.
/// </summary>
[Route("api/mobile/v1/me")]
public sealed class EmployeeSelfController(IEmployeeSelfService self) : MobileApiControllerBase
{
    /// <summary>What HR has switched on for the app (HR portal → Settings → Employee App).</summary>
    [HttpGet("app-settings")]
    [ProducesResponseType<ApiEnvelope<EmployeeAppFeaturesDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<EmployeeAppFeaturesDto>>> AppSettings(CancellationToken ct) =>
        OkEnvelope(await self.GetFeaturesAsync(Caller, ct));

    /// <summary>Salary structure and payment terms (EMP-042). Never cached.</summary>
    [HttpGet("salary")]
    [ProducesResponseType<ApiEnvelope<MySalaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MySalaryDto>>> Salary(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await self.GetMySalaryAsync(Caller, ct));
    }

    /// <summary>The salary bank account, masked (EMP-043). Never cached.</summary>
    [HttpGet("bank-account")]
    [ProducesResponseType<ApiEnvelope<MyBankAccountDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MyBankAccountDto>>> BankAccount(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await self.GetMyBankAccountAsync(Caller, ct));
    }

    /// <summary>Onboarding stage, progress and tasks (EMP-041).</summary>
    [HttpGet("onboarding")]
    [ProducesResponseType<ApiEnvelope<MyOnboardingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MyOnboardingDto>>> Onboarding(CancellationToken ct) =>
        OkEnvelope(await self.GetMyOnboardingAsync(Caller, ct));
}
