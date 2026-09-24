using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Employee;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Employee.Controllers;

/// <summary>
/// HR granting and revoking employee access to the mobile app.
/// </summary>
[Route("api/mobile/v1/employees/mobile-access")]
public sealed class EmployeeMobileAccessController(IEmployeeMobileAccessService access)
    : MobileApiControllerBase
{
    /// <summary>Every employee with their mobile-app standing, those awaiting a decision first.</summary>
    [HttpGet]
    [RequirePermission("EMPLOYEES.VIEW")]
    [ProducesResponseType<ApiEnvelope<IReadOnlyList<EmployeeMobileAccessDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<EmployeeMobileAccessDto>>>> List(
        [FromQuery] MobileAccessQuery query,
        CancellationToken ct) =>
        OkEnvelope(await access.GetAsync(Caller, query, ct));

    /// <summary>
    /// Approves mobile access. The temporary password in the response is shown once and is not
    /// recoverable afterwards.
    /// </summary>
    [HttpPost("{employeeId}/grant")]
    [RequirePermission("ACCOUNTS.MANAGE")]
    [ProducesResponseType<ApiEnvelope<GrantMobileAccessResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<GrantMobileAccessResultDto>>> Grant(
        string employeeId,
        [FromBody] GrantMobileAccessRequest request,
        CancellationToken ct) =>
        OkEnvelope(await access.GrantAsync(Caller, employeeId, request, ct));

    [HttpPost("{employeeId}/suspend")]
    [RequirePermission("ACCOUNTS.MANAGE")]
    [ProducesResponseType<ApiEnvelope<EmployeeMobileAccessDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<EmployeeMobileAccessDto>>> Suspend(
        string employeeId,
        CancellationToken ct) =>
        OkEnvelope(await access.SetActiveAsync(Caller, employeeId, false, ct));

    [HttpPost("{employeeId}/restore")]
    [RequirePermission("ACCOUNTS.MANAGE")]
    [ProducesResponseType<ApiEnvelope<EmployeeMobileAccessDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<EmployeeMobileAccessDto>>> Restore(
        string employeeId,
        CancellationToken ct) =>
        OkEnvelope(await access.SetActiveAsync(Caller, employeeId, true, ct));
}
