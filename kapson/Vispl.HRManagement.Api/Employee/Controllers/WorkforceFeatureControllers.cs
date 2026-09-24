using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Mobile.Workforce;

namespace Vispl.HRManagement.Api.Employee.Controllers;

/// <summary>
/// Closing a finished day on demand.
///
/// A background worker does this hourly, so this exists for the case the worker cannot help
/// with: a shift or a leave request corrected after the day was already closed, where HR wants
/// the register to agree with the correction now rather than at the top of the hour.
/// </summary>
[Route("api/mobile/v1/attendance/close")]
public sealed class AttendanceCloseController(
    Vispl.HRManagement.BM.ITF.Workforce.IAttendanceDayCloser closer) : MobileApiControllerBase
{
    [HttpPost("{date}")]
    [RequirePermission("ATTENDANCE.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<Vispl.HRManagement.BM.ITF.Workforce.AttendanceCloseResultDto>>>
        Close(DateOnly date, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<Vispl.HRManagement.BM.ITF.Workforce.AttendanceCloseResultDto>();
        }

        try
        {
            return OkEnvelope(await closer.CloseAsync(TenantKey, date, ct));
        }
        catch (InvalidOperationException tooEarly)
        {
            // The day is not over in this client's own time zone. A refusal with the reason,
            // not a 500: the caller asked for something that is simply not yet knowable.
            return BadRequest(new ProblemDetails
            {
                Title = tooEarly.Message,
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path
            });
        }
    }
}

// MBK-024 — WF-003 and WF-004 -------------------------------------------------

[Route("api/mobile/v1/attendance")]
public sealed class AttendanceController(IAttendanceService attendance) : MobileApiControllerBase
{
    [HttpGet("dashboard")]
    [RequirePermission("ATTENDANCE.VIEW")]
    public async Task<ActionResult<ApiEnvelope<AttendanceDashboardDto>>> Dashboard(
        [FromQuery] DateOnly? date,
        CancellationToken ct) =>
        OkEnvelope(await attendance.GetDashboardAsync(Caller, date, ct));

    [HttpGet("me")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<AttendanceDayDto>>>> Mine(
        [FromQuery] AttendanceRangeQuery query,
        CancellationToken ct) =>
        OkEnvelope(await attendance.GetMineAsync(Caller, query, ct));

    [HttpGet("team")]
    [RequirePermission("ATTENDANCE.TEAM")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<AttendanceDayDto>>>> Team(
        [FromQuery] TeamAttendanceQuery request,
        CancellationToken ct) =>
        OkEnvelope(await attendance.GetTeamAsync(Caller, request, ct));

    [HttpGet("register/{date}")]
    [RequirePermission("ATTENDANCE.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<AttendanceDayDto>>>> Register(
        DateOnly date,
        [FromQuery] AttendanceRegisterQuery request,
        CancellationToken ct) =>
        OkEnvelope(await attendance.GetRegisterAsync(Caller, date, request, ct));

    // Punch (MOB-073) --------------------------------------------------------

    [HttpGet("punch")]
    public async Task<ActionResult<ApiEnvelope<PunchStateDto>>> PunchState(CancellationToken ct) =>
        OkEnvelope(await attendance.GetPunchStateAsync(Caller, ct));

    /// <summary>
    /// Records a punch with device, time and location evidence. Live only — a punch is
    /// never queued offline, because its evidence cannot be trusted after the fact.
    /// </summary>
    [HttpPost("punch")]
    public async Task<ActionResult<ApiEnvelope<PunchResultDto>>> Punch(
        [FromBody] PunchRequest request,
        CancellationToken ct) =>
        OkEnvelope(await attendance.PunchAsync(Caller, request, ct));

    // Corrections (WF-003) ---------------------------------------------------

    [HttpGet("corrections")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<AttendanceCorrectionDto>>>> ListCorrections(
        [FromQuery] AttendanceCorrectionListQuery request,
        CancellationToken ct) =>
        OkEnvelope(await attendance.ListCorrectionsAsync(Caller, request, ct));

    /// <summary>
    /// The one mobile submission that may be queued offline: dated, deterministic and low
    /// risk. Carries an idempotency key so a replayed queue entry cannot duplicate it.
    /// </summary>
    [HttpPost("corrections")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> CreateCorrection(
        [FromBody] CreateAttendanceCorrectionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await attendance.CreateCorrectionAsync(Caller, request, ct));

    [HttpGet("corrections/{requestId}")]
    public async Task<ActionResult<ApiEnvelope<AttendanceCorrectionDto>>> GetCorrection(
        string requestId,
        CancellationToken ct) =>
        OkEnvelope(await attendance.GetCorrectionAsync(Caller, requestId, ct));

    [HttpPost("corrections/{requestId}/decision")]
    [RequirePermission("ATTENDANCE.APPROVE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> DecideCorrection(
        string requestId,
        [FromBody] AttendanceCorrectionDecisionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await attendance.DecideCorrectionAsync(Caller, requestId, request, ct));

    [HttpPost("corrections/{requestId}/withdraw")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> WithdrawCorrection(
        string requestId,
        CancellationToken ct) =>
        MutationEnvelope(await attendance.WithdrawCorrectionAsync(Caller, requestId, ct));

    // Exceptions and overtime (WF-004) ----------------------------------------

    [HttpGet("exceptions")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<AttendanceExceptionDto>>>> ListExceptions(
        [FromQuery] AttendanceExceptionListQuery request,
        CancellationToken ct) =>
        OkEnvelope(await attendance.ListExceptionsAsync(Caller, request, ct));

    [HttpGet("exceptions/{exceptionId}")]
    public async Task<ActionResult<ApiEnvelope<AttendanceExceptionDto>>> GetException(
        string exceptionId,
        CancellationToken ct) =>
        OkEnvelope(await attendance.GetExceptionAsync(Caller, exceptionId, ct));

    /// <summary>Employee confirms or disputes a system-detected exception (WF-004.E2).</summary>
    [HttpPost("exceptions/{exceptionId}/confirm")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> ConfirmException(
        string exceptionId,
        [FromBody] ConfirmAttendanceExceptionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await attendance.ConfirmExceptionAsync(Caller, exceptionId, request, ct));

    [HttpPost("exceptions/{exceptionId}/resolution")]
    [RequirePermission("ATTENDANCE.APPROVE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> ResolveException(
        string exceptionId,
        [FromBody] ResolveAttendanceExceptionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await attendance.ResolveExceptionAsync(Caller, exceptionId, request, ct));
}

// MBK-025 — WF-005 -----------------------------------------------------------

[Route("api/mobile/v1/shifts")]
public sealed class ShiftsController(IShiftService shifts) : MobileApiControllerBase
{
    [HttpGet("roster")]
    [RequirePermission("SHIFTS.VIEW")]
    public async Task<ActionResult<ApiEnvelope<ShiftRosterDto>>> Roster(
        [FromQuery] ShiftRosterQuery request,
        CancellationToken ct) =>
        OkEnvelope(await shifts.GetRosterAsync(Caller, request, ct));

    [HttpGet("assignments/{assignmentId}")]
    public async Task<ActionResult<ApiEnvelope<ShiftAssignmentDto>>> Assignment(
        string assignmentId,
        CancellationToken ct) =>
        OkEnvelope(await shifts.GetAssignmentAsync(Caller, assignmentId, ct));

    [HttpPut("assignments/{assignmentId}")]
    [RequirePermission("SHIFTS.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> UpdateAssignment(
        string assignmentId,
        [FromBody] UpdateShiftAssignmentRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await shifts.UpdateAssignmentAsync(Caller, assignmentId, request, ct));

    /// <summary>
    /// Proposes a swap to an eligible counterparty. Both assignments are version-checked;
    /// a swap is void if either changes before approval (WF-005).
    /// </summary>
    [HttpPost("assignments/{assignmentId}/swap")]
    public async Task<ActionResult<ApiEnvelope<ShiftSwapResultDto>>> RequestSwap(
        string assignmentId,
        [FromBody] RequestShiftSwapRequest request,
        CancellationToken ct) =>
        OkEnvelope(await shifts.RequestSwapAsync(Caller, assignmentId, request, ct));

    /// <summary>Counterparty accepts or declines. A decline closes without reaching HR.</summary>
    [HttpPost("swaps/{swapId}/response")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> RespondToSwap(
        string swapId,
        [FromBody] RespondToShiftSwapRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await shifts.RespondToSwapAsync(Caller, swapId, request, ct));

    [HttpPost("swaps/{swapId}/decision")]
    [RequirePermission("SHIFTS.APPROVE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> DecideSwap(
        string swapId,
        [FromBody] RespondToShiftSwapRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await shifts.DecideSwapAsync(Caller, swapId, request, ct));

    [HttpPost("assignments/{assignmentId}/acknowledge")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Acknowledge(
        string assignmentId,
        [FromBody] AcknowledgeShiftRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await shifts.AcknowledgeAsync(Caller, assignmentId, request, ct));
}

// MBK-026 — WF-001 and WF-002 -------------------------------------------------

[Route("api/mobile/v1/leave")]
public sealed class LeaveController(IMobileLeaveService leave) : MobileApiControllerBase
{
    [HttpGet("me/dashboard")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<LeaveBalanceDto>>>> Dashboard(
        CancellationToken ct) =>
        OkEnvelope(await leave.GetDashboardAsync(Caller, ct));

    [HttpGet("balances")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<LeaveBalanceDto>>>> Balances(
        [FromQuery] LeaveBalanceQuery query,
        CancellationToken ct) =>
        OkEnvelope(await leave.GetBalancesAsync(Caller, query, ct));

    // Requests (WF-001 submitter side) ----------------------------------------

    [HttpGet("requests")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<LeaveRequestListItemDto>>>> ListRequests(
        [FromQuery] LeaveRequestListQuery request,
        CancellationToken ct) =>
        OkEnvelope(await leave.ListRequestsAsync(Caller, request, ct));

    /// <summary>
    /// Server-computed working days, holiday exclusions and balance impact for the review
    /// step (WF-001.E3). The client never computes leave days itself.
    /// </summary>
    [HttpPost("requests/calculate")]
    public async Task<ActionResult<ApiEnvelope<LeaveCalculationDto>>> Calculate(
        [FromBody] LeaveCalculationRequest request,
        CancellationToken ct) =>
        OkEnvelope(await leave.CalculateAsync(Caller, request, ct));

    [HttpPost("requests")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> CreateRequest(
        [FromBody] CreateLeaveRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await leave.CreateRequestAsync(Caller, request, ct));

    [HttpGet("requests/{requestId}")]
    public async Task<ActionResult<ApiEnvelope<LeaveRequestDetailDto>>> GetRequest(
        string requestId,
        CancellationToken ct) =>
        OkEnvelope(await leave.GetRequestAsync(Caller, requestId, ct));

    [HttpPost("requests/{requestId}/withdraw")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Withdraw(
        string requestId,
        [FromBody] WithdrawLeaveRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await leave.WithdrawAsync(Caller, requestId, request, ct));

    [HttpPost("requests/{requestId}/comments")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Comment(
        string requestId,
        [FromBody] AddRequestCommentBody body,
        CancellationToken ct) =>
        MutationEnvelope(await leave.CommentAsync(Caller, requestId, body.Comment, ct));

    // Manager and HR side (WF-001 H1–H4) ---------------------------------------

    [HttpGet("team/requests")]
    [RequirePermission("LEAVE.TEAM")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<LeaveRequestListItemDto>>>> TeamRequests(
        [FromQuery] TeamLeaveQuery request,
        CancellationToken ct) =>
        OkEnvelope(await leave.GetTeamRequestsAsync(Caller, request, ct));

    /// <summary>Approve, reject or return. Live only, version-checked (WF-001.H3).</summary>
    [HttpPost("requests/{requestId}/decision")]
    [RequirePermission("LEAVE.APPROVE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Decide(
        string requestId,
        [FromBody] LeaveDecisionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await leave.DecideAsync(Caller, requestId, request, ct));

    [HttpGet("team/calendar")]
    [RequirePermission("LEAVE.TEAM")]
    public async Task<ActionResult<ApiEnvelope<LeaveCalendarDto>>> TeamCalendar(
        [FromQuery] TeamLeaveQuery request,
        CancellationToken ct) =>
        OkEnvelope(await leave.GetTeamCalendarAsync(Caller, request, ct));

    // Comp-off and encashment (WF-002) ------------------------------------------

    [HttpGet("adjustments")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<LeaveAdjustmentCreditDto>>>> Credits(
        CancellationToken ct) =>
        OkEnvelope(await leave.GetAdjustmentCreditsAsync(Caller, ct));

    [HttpPost("adjustments")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> CreateAdjustment(
        [FromBody] LeaveAdjustmentRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await leave.CreateAdjustmentAsync(Caller, request, ct));
}

// MBK-027 -------------------------------------------------------------------

[Route("api/mobile/v1/leave/policies")]
public sealed class LeavePoliciesController(ILeavePolicyService policies) : MobileApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<LeavePolicySummaryDto>>>> List(
        CancellationToken ct) =>
        OkEnvelope(await policies.ListAsync(Caller, ct));

    [HttpGet("{policyId}")]
    public async Task<ActionResult<ApiEnvelope<LeavePolicyDetailDto>>> Get(
        string policyId,
        [FromQuery] int? year,
        CancellationToken ct) =>
        OkEnvelope(await policies.GetAsync(Caller, policyId, year, ct));
}
