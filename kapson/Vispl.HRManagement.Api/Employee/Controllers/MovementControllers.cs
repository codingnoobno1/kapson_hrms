using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Mobile.Workforce;

namespace Vispl.HRManagement.Api.Employee.Controllers;

/// <summary>
/// Employees off site during working hours. The board is HR-facing; declaring and returning are
/// self-service, shared with the employee app.
/// </summary>
[Route("api/mobile/v1/movement")]
public sealed class MovementController(IMovementService movement) : MobileApiControllerBase
{
    [HttpGet("out")]
    [RequirePermission("MOVEMENT.TEAM")]
    [ProducesResponseType<ApiEnvelope<MovementBoardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MovementBoardDto>>> Board(
        [FromQuery] MovementQuery query,
        CancellationToken ct) =>
        OkEnvelope(await movement.GetBoardAsync(Caller, query, ct));

    [HttpGet("overdue")]
    [RequirePermission("MOVEMENT.TEAM")]
    [ProducesResponseType<ApiEnvelope<MovementBoardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MovementBoardDto>>> Overdue(
        [FromQuery] MovementQuery query,
        CancellationToken ct)
    {
        query.OverdueOnly = true;
        return OkEnvelope(await movement.GetBoardAsync(Caller, query, ct));
    }

    [HttpGet("me")]
    [ProducesResponseType<ApiEnvelope<IReadOnlyList<MovementRowDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<MovementRowDto>>>> Mine(
        CancellationToken ct) =>
        OkEnvelope(await movement.GetMineAsync(Caller, ct));

    /// <summary>
    /// Self-service, so it carries no permission attribute: the service resolves the caller's
    /// own employee record and will not let anyone declare a movement for someone else. An HR
    /// account is also an employee, and gating this on an Employee-portal permission would stop
    /// HR staff recording their own errands.
    /// </summary>
    [HttpPost("out")]
    [ProducesResponseType<ApiEnvelope<MutationResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Declare(
        [FromBody] DeclareMovementRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await movement.DeclareAsync(Caller, request, ct));

    [HttpPost("{movementId}/return")]
    [ProducesResponseType<ApiEnvelope<MutationResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Return(
        string movementId,
        [FromBody] ReturnMovementRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await movement.RecordReturnAsync(Caller, movementId, request, ct));
}

/// <summary>
/// Gate passes: requested by an employee, approved by a manager, verified at a gate.
/// </summary>
[Route("api/mobile/v1/gate-passes")]
public sealed class GatePassController(IGatePassService passes) : MobileApiControllerBase
{
    [HttpGet]
    [RequirePermission("GATEPASS.TEAM")]
    [ProducesResponseType<ApiEnvelope<GatePassBoardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<GatePassBoardDto>>> Board(
        [FromQuery] GatePassQuery request,
        CancellationToken ct) =>
        OkEnvelope(await passes.GetBoardAsync(Caller, request, ct));

    [HttpGet("pending")]
    [RequirePermission("GATEPASS.APPROVE")]
    [ProducesResponseType<ApiEnvelope<GatePassBoardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<GatePassBoardDto>>> Pending(
        [FromQuery] GatePassQuery request,
        CancellationToken ct)
    {
        request.State = "Pending";
        return OkEnvelope(await passes.GetBoardAsync(Caller, request, ct));
    }

    [HttpGet("me")]
    [ProducesResponseType<ApiEnvelope<IReadOnlyList<GatePassRowDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<GatePassRowDto>>>> Mine(
        CancellationToken ct) =>
        OkEnvelope(await passes.GetMineAsync(Caller, ct));

    [HttpGet("{passId}")]
    [ProducesResponseType<ApiEnvelope<GatePassRowDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<GatePassRowDto>>> Get(
        string passId,
        CancellationToken ct) =>
        OkEnvelope(await passes.GetAsync(Caller, passId, ct));

    /// <summary>The scannable credential. Returned only to the person the pass belongs to.</summary>
    [HttpGet("{passId}/credential")]
    [ProducesResponseType<ApiEnvelope<GatePassCredentialDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<GatePassCredentialDto>>> Credential(
        string passId,
        CancellationToken ct) =>
        OkEnvelope(await passes.GetCredentialAsync(Caller, passId, ct));

    /// <summary>Self-service; ownership is enforced in the service, not by an attribute.</summary>
    [HttpPost]
    [ProducesResponseType<ApiEnvelope<MutationResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Request(
        [FromBody] RequestGatePassRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await passes.RequestAsync(Caller, request, ct));

    [HttpPost("{passId}/decision")]
    [RequirePermission("GATEPASS.APPROVE")]
    [ProducesResponseType<ApiEnvelope<MutationResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Decide(
        string passId,
        [FromBody] GatePassDecisionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await passes.DecideAsync(Caller, passId, request, ct));

    [HttpPost("{passId}/withdraw")]
    [ProducesResponseType<ApiEnvelope<MutationResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Withdraw(
        string passId,
        CancellationToken ct) =>
        MutationEnvelope(await passes.WithdrawAsync(Caller, passId, ct));

    /// <summary>Used at a gate. Answers only whether to open the barrier, and for whom.</summary>
    [HttpPost("scan")]
    [RequirePermission("GATEPASS.VERIFY")]
    [ProducesResponseType<ApiEnvelope<GatePassScanResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<GatePassScanResultDto>>> Scan(
        [FromBody] ScanGatePassRequest request,
        CancellationToken ct) =>
        OkEnvelope(await passes.ScanAsync(Caller, request, ct));
}

/// <summary>
/// Business travel that still counts as working time: requested by an employee, decided by a
/// manager or HR. No permission attribute on Pending or Decide — the HR portal's ONDUTY.APPROVE
/// and a reporting manager's TEAM.ONDUTY.APPROVE inside the employee app reach the same endpoint,
/// and only the service knows both names mean the same decision.
/// </summary>
[Route("api/mobile/v1/on-duty")]
public sealed class OnDutyController(IOnDutyService onDuty) : MobileApiControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<ApiEnvelope<IReadOnlyList<OnDutyRowDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<OnDutyRowDto>>>> Mine(
        CancellationToken ct) =>
        OkEnvelope(await onDuty.GetMineAsync(Caller, ct));

    [HttpGet("pending")]
    [ProducesResponseType<ApiEnvelope<IReadOnlyList<OnDutyRowDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<OnDutyRowDto>>>> Pending(
        CancellationToken ct) =>
        OkEnvelope(await onDuty.GetPendingAsync(Caller, ct));

    /// <summary>Self-service; ownership is enforced in the service, not by an attribute.</summary>
    [HttpPost]
    [ProducesResponseType<ApiEnvelope<MutationResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Request(
        [FromBody] RequestOnDutyRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await onDuty.RequestAsync(Caller, request, ct));

    [HttpPost("{requestId}/decision")]
    [ProducesResponseType<ApiEnvelope<MutationResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Decide(
        string requestId,
        [FromBody] OnDutyDecisionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await onDuty.DecideAsync(Caller, requestId, request, ct));

    [HttpPost("{requestId}/withdraw")]
    [ProducesResponseType<ApiEnvelope<MutationResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Withdraw(
        string requestId,
        CancellationToken ct) =>
        MutationEnvelope(await onDuty.WithdrawAsync(Caller, requestId, ct));
}
