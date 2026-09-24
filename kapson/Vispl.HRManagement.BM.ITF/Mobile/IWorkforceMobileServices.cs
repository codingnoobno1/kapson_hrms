using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Mobile.Workforce;

namespace Vispl.HRManagement.BM.ITF.Mobile;

public interface IAttendanceService
{
    Task<AttendanceDashboardDto> GetDashboardAsync(
        MobileCallerContext caller,
        DateOnly? date,
        CancellationToken ct);

    Task<IReadOnlyList<AttendanceDayDto>> GetMineAsync(
        MobileCallerContext caller,
        AttendanceRangeQuery query,
        CancellationToken ct);

    Task<CursorPage<AttendanceDayDto>> GetTeamAsync(
        MobileCallerContext caller,
        TeamAttendanceQuery query,
        CancellationToken ct);

    Task<CursorPage<AttendanceDayDto>> GetRegisterAsync(
        MobileCallerContext caller,
        DateOnly date,
        AttendanceRegisterQuery query,
        CancellationToken ct);

    Task<PunchStateDto> GetPunchStateAsync(MobileCallerContext caller, CancellationToken ct);

    Task<PunchResultDto> PunchAsync(
        MobileCallerContext caller,
        PunchRequest request,
        CancellationToken ct);

    Task<CursorPage<AttendanceCorrectionDto>> ListCorrectionsAsync(
        MobileCallerContext caller,
        AttendanceCorrectionListQuery query,
        CancellationToken ct);

    Task<AttendanceCorrectionDto> GetCorrectionAsync(
        MobileCallerContext caller,
        string requestId,
        CancellationToken ct);

    Task<MutationResultDto> CreateCorrectionAsync(
        MobileCallerContext caller,
        CreateAttendanceCorrectionRequest request,
        CancellationToken ct);

    Task<MutationResultDto> DecideCorrectionAsync(
        MobileCallerContext caller,
        string requestId,
        AttendanceCorrectionDecisionRequest request,
        CancellationToken ct);

    Task<MutationResultDto> WithdrawCorrectionAsync(
        MobileCallerContext caller,
        string requestId,
        CancellationToken ct);

    /// <summary>HR edits a day directly (ATTENDANCE.MANAGE): stored as an approved correction and applied.</summary>
    Task<MutationResultDto> ApplyHrCorrectionAsync(
        MobileCallerContext caller,
        HrAttendanceDayEditRequest request,
        CancellationToken ct);

    Task<CursorPage<AttendanceExceptionDto>> ListExceptionsAsync(
        MobileCallerContext caller,
        AttendanceExceptionListQuery query,
        CancellationToken ct);

    Task<AttendanceExceptionDto> GetExceptionAsync(
        MobileCallerContext caller,
        string exceptionId,
        CancellationToken ct);

    Task<MutationResultDto> ConfirmExceptionAsync(
        MobileCallerContext caller,
        string exceptionId,
        ConfirmAttendanceExceptionRequest request,
        CancellationToken ct);

    Task<MutationResultDto> ResolveExceptionAsync(
        MobileCallerContext caller,
        string exceptionId,
        ResolveAttendanceExceptionRequest request,
        CancellationToken ct);
}

public interface IShiftService
{
    Task<ShiftRosterDto> GetRosterAsync(
        MobileCallerContext caller,
        ShiftRosterQuery query,
        CancellationToken ct);

    Task<ShiftAssignmentDto> GetAssignmentAsync(
        MobileCallerContext caller,
        string assignmentId,
        CancellationToken ct);

    Task<MutationResultDto> UpdateAssignmentAsync(
        MobileCallerContext caller,
        string assignmentId,
        UpdateShiftAssignmentRequest request,
        CancellationToken ct);

    Task<ShiftSwapResultDto> RequestSwapAsync(
        MobileCallerContext caller,
        string assignmentId,
        RequestShiftSwapRequest request,
        CancellationToken ct);

    Task<MutationResultDto> RespondToSwapAsync(
        MobileCallerContext caller,
        string swapId,
        RespondToShiftSwapRequest request,
        CancellationToken ct);

    Task<MutationResultDto> DecideSwapAsync(
        MobileCallerContext caller,
        string swapId,
        RespondToShiftSwapRequest request,
        CancellationToken ct);

    Task<MutationResultDto> AcknowledgeAsync(
        MobileCallerContext caller,
        string assignmentId,
        AcknowledgeShiftRequest request,
        CancellationToken ct);
}

public interface IMobileLeaveService
{
    Task<IReadOnlyList<LeaveBalanceDto>> GetDashboardAsync(
        MobileCallerContext caller,
        CancellationToken ct);

    Task<IReadOnlyList<LeaveBalanceDto>> GetBalancesAsync(
        MobileCallerContext caller,
        LeaveBalanceQuery query,
        CancellationToken ct);

    Task<CursorPage<LeaveRequestListItemDto>> ListRequestsAsync(
        MobileCallerContext caller,
        LeaveRequestListQuery query,
        CancellationToken ct);

    Task<LeaveRequestDetailDto> GetRequestAsync(
        MobileCallerContext caller,
        string requestId,
        CancellationToken ct);

    Task<LeaveCalculationDto> CalculateAsync(
        MobileCallerContext caller,
        LeaveCalculationRequest request,
        CancellationToken ct);

    Task<MutationResultDto> CreateRequestAsync(
        MobileCallerContext caller,
        CreateLeaveRequest request,
        CancellationToken ct);

    Task<MutationResultDto> WithdrawAsync(
        MobileCallerContext caller,
        string requestId,
        WithdrawLeaveRequest request,
        CancellationToken ct);

    Task<MutationResultDto> CommentAsync(
        MobileCallerContext caller,
        string requestId,
        string comment,
        CancellationToken ct);

    Task<CursorPage<LeaveRequestListItemDto>> GetTeamRequestsAsync(
        MobileCallerContext caller,
        TeamLeaveQuery query,
        CancellationToken ct);

    Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller,
        string requestId,
        LeaveDecisionRequest request,
        CancellationToken ct);

    Task<LeaveCalendarDto> GetTeamCalendarAsync(
        MobileCallerContext caller,
        TeamLeaveQuery query,
        CancellationToken ct);

    Task<IReadOnlyList<LeaveAdjustmentCreditDto>> GetAdjustmentCreditsAsync(
        MobileCallerContext caller,
        CancellationToken ct);

    Task<MutationResultDto> CreateAdjustmentAsync(
        MobileCallerContext caller,
        LeaveAdjustmentRequest request,
        CancellationToken ct);
}

public interface ILeavePolicyService
{
    Task<IReadOnlyList<LeavePolicySummaryDto>> ListAsync(
        MobileCallerContext caller,
        CancellationToken ct);

    Task<LeavePolicyDetailDto> GetAsync(
        MobileCallerContext caller,
        string policyId,
        int? year,
        CancellationToken ct);
}
