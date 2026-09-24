using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Mobile.Workforce;

namespace Vispl.HRManagement.BM.ITF.Mobile;

/// <summary>
/// Employees leaving the premises during working hours, and the passes that permit it.
///
/// Two related things kept separate: a gate pass is permission granted in advance, a movement
/// record is what actually happened. A pass may be approved and never used.
/// </summary>
public interface IMovementService
{
    Task<MovementBoardDto> GetBoardAsync(
        MobileCallerContext caller, MovementQuery query, CancellationToken ct);

    Task<IReadOnlyList<MovementRowDto>> GetMineAsync(
        MobileCallerContext caller, CancellationToken ct);

    Task<MutationResultDto> DeclareAsync(
        MobileCallerContext caller, DeclareMovementRequest request, CancellationToken ct);

    Task<MutationResultDto> RecordReturnAsync(
        MobileCallerContext caller, string movementId, ReturnMovementRequest request, CancellationToken ct);
}

public interface IGatePassService
{
    Task<GatePassBoardDto> GetBoardAsync(
        MobileCallerContext caller, GatePassQuery query, CancellationToken ct);

    Task<GatePassRowDto> GetAsync(
        MobileCallerContext caller, string passId, CancellationToken ct);

    Task<IReadOnlyList<GatePassRowDto>> GetMineAsync(
        MobileCallerContext caller, CancellationToken ct);

    Task<MutationResultDto> RequestAsync(
        MobileCallerContext caller, RequestGatePassRequest request, CancellationToken ct);

    Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller, string passId, GatePassDecisionRequest request, CancellationToken ct);

    Task<MutationResultDto> WithdrawAsync(
        MobileCallerContext caller, string passId, CancellationToken ct);

    /// <summary>The credential for the holder's own approved pass, for display as a QR.</summary>
    Task<GatePassCredentialDto> GetCredentialAsync(
        MobileCallerContext caller, string passId, CancellationToken ct);

    /// <summary>Resolves a scanned token at a gate and records the crossing.</summary>
    Task<GatePassScanResultDto> ScanAsync(
        MobileCallerContext caller, ScanGatePassRequest request, CancellationToken ct);
}

/// <summary>
/// Business travel that still counts as working time — apply, view and decide, no gate scan.
///
/// Decide has no permission attribute on its controller action, on purpose: both the HR portal
/// (ONDUTY.APPROVE) and a reporting manager inside the employee app (TEAM.ONDUTY.APPROVE) reach
/// the same endpoint, and only the service knows both names are the same decision.
/// </summary>
public interface IOnDutyService
{
    Task<IReadOnlyList<OnDutyRowDto>> GetMineAsync(
        MobileCallerContext caller, CancellationToken ct);

    /// <summary>Requests waiting on the caller's own decision.</summary>
    Task<IReadOnlyList<OnDutyRowDto>> GetPendingAsync(
        MobileCallerContext caller, CancellationToken ct);

    Task<MutationResultDto> RequestAsync(
        MobileCallerContext caller, RequestOnDutyRequest request, CancellationToken ct);

    Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller, string requestId, OnDutyDecisionRequest request, CancellationToken ct);

    Task<MutationResultDto> WithdrawAsync(
        MobileCallerContext caller, string requestId, CancellationToken ct);
}
