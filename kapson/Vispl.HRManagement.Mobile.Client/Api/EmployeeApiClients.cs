using System.Globalization;
using Vispl.HRManagement.Contracts.Mobile.Employee;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Mobile.Workforce;
using Vispl.HRManagement.Mobile.Client.Http;

namespace Vispl.HRManagement.Mobile.Client.Api;

/// <summary>
/// The Home identity card and its tiles, and the tiles the Attendance and Leave tabs open on.
///
/// Every number here is computed server-side. The app formats what comes back; it does not
/// derive a day count, a balance or a present-days figure of its own.
/// </summary>
public sealed class MeApiClient(MobileApiClient api)
{
    public Task<EmployeeHomeSummaryDto> GetHomeSummaryAsync(CancellationToken ct = default) =>
        api.GetAsync<EmployeeHomeSummaryDto>("api/mobile/v1/me/summary", ct);

    /// <summary>The four attendance tiles for one month, plus the working-day denominator.</summary>
    public Task<AttendanceKpiSummaryDto> GetAttendanceKpisAsync(
        int year, int month, CancellationToken ct = default) =>
        api.GetAsync<AttendanceKpiSummaryDto>(
            $"api/mobile/v1/me/kpis/attendance?month={year:D4}-{month:D2}", ct);

    /// <summary>The Home Interview Schedule card. Empty and not-an-interviewer for most employees.</summary>
    public Task<EmployeeInterviewScheduleDto> GetMyInterviewsAsync(CancellationToken ct = default) =>
        api.GetAsync<EmployeeInterviewScheduleDto>("api/mobile/v1/me/interviews", ct);

    /// <summary>What HR has switched on. The server enforces the same switches.</summary>
    public Task<EmployeeAppFeaturesDto> GetAppFeaturesAsync(CancellationToken ct = default) =>
        api.GetAsync<EmployeeAppFeaturesDto>("api/mobile/v1/me/app-settings", ct);

    /// <summary>Salary structure. Held in memory for the sheet only, never persisted (CR-9).</summary>
    public Task<MySalaryDto> GetMySalaryAsync(CancellationToken ct = default) =>
        api.GetAsync<MySalaryDto>("api/mobile/v1/me/salary", ct);

    /// <summary>Salary bank account, already masked by the server (CR-9).</summary>
    public Task<MyBankAccountDto> GetMyBankAccountAsync(CancellationToken ct = default) =>
        api.GetAsync<MyBankAccountDto>("api/mobile/v1/me/bank-account", ct);

    public Task<MyOnboardingDto> GetMyOnboardingAsync(CancellationToken ct = default) =>
        api.GetAsync<MyOnboardingDto>("api/mobile/v1/me/onboarding", ct);

    /// <summary>Attendance Analytics for a period key (AttendancePeriods). Every figure is server-computed.</summary>
    public Task<AttendanceAnalyticsDto> GetAttendanceAnalyticsAsync(string period, CancellationToken ct = default) =>
        api.GetAsync<AttendanceAnalyticsDto>($"api/mobile/v1/me/attendance/analytics?period={Uri.EscapeDataString(period)}", ct);

    /// <summary>Export Report: an .xlsx as base64, saved to the app cache and shared — never kept.</summary>
    public Task<AttendanceReportFileDto> GetAttendanceReportAsync(string period, CancellationToken ct = default) =>
        api.GetAsync<AttendanceReportFileDto>($"api/mobile/v1/me/attendance/report?period={Uri.EscapeDataString(period)}", ct);

    /// <summary>Email Analytics: sent to the work email on the employee record.</summary>
    public Task<AttendanceReportEmailDto> EmailAttendanceReportAsync(string period, CancellationToken ct = default) =>
        api.PostAsync<AttendanceReportEmailDto>($"api/mobile/v1/me/attendance/report/email?period={Uri.EscapeDataString(period)}", null, null, ct);

    /// <summary>Full Month Attendance's Download report: one exact calendar month, not a rolling period.</summary>
    public Task<AttendanceReportFileDto> GetAttendanceReportForMonthAsync(
        int year, int month, CancellationToken ct = default) =>
        api.GetAsync<AttendanceReportFileDto>($"api/mobile/v1/me/attendance/report?month={year:D4}-{month:D2}", ct);

    /// <summary>Full Month Attendance's Email report: one exact calendar month, not a rolling period.</summary>
    public Task<AttendanceReportEmailDto> EmailAttendanceReportForMonthAsync(
        int year, int month, CancellationToken ct = default) =>
        api.PostAsync<AttendanceReportEmailDto>($"api/mobile/v1/me/attendance/report/email?month={year:D4}-{month:D2}", null, null, ct);

    public Task<LeaveKpiSummaryDto> GetLeaveKpisAsync(int? year = null, CancellationToken ct = default) =>
        api.GetAsync<LeaveKpiSummaryDto>(
            year is null
                ? "api/mobile/v1/me/kpis/leave"
                : $"api/mobile/v1/me/kpis/leave?year={year}", ct);
}

/// <summary>My attendance, my punches, and the corrections I have asked for.</summary>
public sealed class AttendanceApiClient(MobileApiClient api)
{
    public Task<IReadOnlyList<AttendanceDayDto>> GetMineAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default) =>
        api.GetAsync<IReadOnlyList<AttendanceDayDto>>(
            $"api/mobile/v1/attendance/me?from={Iso(from)}&to={Iso(to)}", ct);

    public Task<PunchStateDto> GetPunchStateAsync(CancellationToken ct = default) =>
        api.GetAsync<PunchStateDto>("api/mobile/v1/attendance/punch", ct);

    /// <summary>
    /// Records a punch.
    ///
    /// <paramref name="clientEventId"/> is the replay guard and must be generated once per
    /// gesture and reused across retries — a new id is a new punch, not the same one again.
    /// </summary>
    public Task<PunchResultDto> PunchAsync(
        string action,
        string deviceId,
        string clientEventId,
        decimal? latitude = null,
        decimal? longitude = null,
        CancellationToken ct = default) =>
        api.PostAsync<PunchResultDto>(
            "api/mobile/v1/attendance/punch",
            new PunchRequest
            {
                Action = action,
                DeviceId = deviceId,
                ClientEventId = clientEventId,
                ClientOccurredUtc = DateTimeOffset.UtcNow,
                Latitude = latitude,
                Longitude = longitude
            },
            clientEventId,
            ct);

    public Task<CursorPage<AttendanceCorrectionDto>> GetMyCorrectionsAsync(
        string? cursor = null, CancellationToken ct = default) =>
        api.GetAsync<CursorPage<AttendanceCorrectionDto>>(
            $"api/mobile/v1/attendance/corrections{Cursor(cursor)}", ct);

    public Task<MutationResultDto> RequestCorrectionAsync(
        CreateAttendanceCorrectionRequest request, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>("api/mobile/v1/attendance/corrections", request, null, ct);

    public Task<MutationResultDto> WithdrawCorrectionAsync(
        string requestId, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>(
            $"api/mobile/v1/attendance/corrections/{requestId}/withdraw", null, null, ct);

    /// <summary>Corrections waiting on a decision (ATTENDANCE.APPROVE) — the same queue HR works on the web.</summary>
    public Task<CursorPage<AttendanceCorrectionDto>> GetCorrectionQueueAsync(CancellationToken ct = default) =>
        api.GetAsync<CursorPage<AttendanceCorrectionDto>>(
            "api/mobile/v1/attendance/corrections?awaitingMyDecisionOnly=true&pageSize=50", ct);

    public Task<MutationResultDto> DecideCorrectionAsync(
        string requestId, AttendanceCorrectionDecisionRequest request, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>(
            $"api/mobile/v1/attendance/corrections/{requestId}/decision", request, null, ct);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Cursor(string? cursor) =>
        string.IsNullOrWhiteSpace(cursor) ? string.Empty : $"?cursor={Uri.EscapeDataString(cursor)}";
}

/// <summary>
/// Leave, from balance to decision.
///
/// The calculate-then-submit handshake is mandatory and is not optional politeness: the server
/// returns a signed, short-lived token with the chargeable day count, and the create call
/// rejects a submission without it.
/// </summary>
public sealed class LeaveApiClient(MobileApiClient api)
{
    public Task<IReadOnlyList<LeaveBalanceDto>> GetBalancesAsync(
        int? year = null, CancellationToken ct = default) =>
        api.GetAsync<IReadOnlyList<LeaveBalanceDto>>(
            year is null
                ? "api/mobile/v1/leave/balances"
                : $"api/mobile/v1/leave/balances?year={year}", ct);

    public Task<IReadOnlyList<LeavePolicySummaryDto>> GetPoliciesAsync(CancellationToken ct = default) =>
        api.GetAsync<IReadOnlyList<LeavePolicySummaryDto>>("api/mobile/v1/leave/policies", ct);

    public Task<CursorPage<LeaveRequestListItemDto>> GetMyRequestsAsync(
        string? cursor = null, CancellationToken ct = default) =>
        api.GetAsync<CursorPage<LeaveRequestListItemDto>>(
            $"api/mobile/v1/leave/requests{Query(cursor)}", ct);

    public Task<LeaveRequestDetailDto> GetRequestAsync(string requestId, CancellationToken ct = default) =>
        api.GetAsync<LeaveRequestDetailDto>($"api/mobile/v1/leave/requests/{requestId}", ct);

    /// <summary>Server-computed chargeable days. Never derive this on the device.</summary>
    public Task<LeaveCalculationDto> CalculateAsync(
        LeaveCalculationRequest request, CancellationToken ct = default) =>
        api.PostAsync<LeaveCalculationDto>(
            "api/mobile/v1/leave/requests/calculate", request, null, ct);

    /// <summary>
    /// <paramref name="idempotencyKey"/> is created once per filled-in form and reused on retry, so a
    /// dropped connection cannot book the same leave twice.
    /// </summary>
    public Task<MutationResultDto> SubmitAsync(
        CreateLeaveRequest request, string? idempotencyKey = null, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>("api/mobile/v1/leave/requests", request, idempotencyKey, ct);

    public Task<MutationResultDto> WithdrawAsync(
        string requestId, string? comment, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>(
            $"api/mobile/v1/leave/requests/{requestId}/withdraw",
            new WithdrawLeaveRequest { Comment = comment ?? string.Empty },
            null,
            ct);

    // ---- Approver side, shown only with LEAVE.APPROVE ----------------------

    public Task<CursorPage<LeaveRequestListItemDto>> GetTeamRequestsAsync(
        string? cursor = null, CancellationToken ct = default) =>
        api.GetAsync<CursorPage<LeaveRequestListItemDto>>(
            $"api/mobile/v1/leave/team/requests{Query(cursor)}", ct);

    public Task<MutationResultDto> DecideAsync(
        string requestId, LeaveDecisionRequest request, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>(
            $"api/mobile/v1/leave/requests/{requestId}/decision", request, null, ct);

    /// <summary>
    /// The Leave Pending for Approval sheet. The same queue the HR portal's Leave page works
    /// from, so a request decided on either disappears from both.
    /// </summary>
    public Task<Contracts.Mobile.Dashboard.LeaveApprovalQueueDto> GetApprovalQueueAsync(CancellationToken ct = default) =>
        api.GetAsync<Contracts.Mobile.Dashboard.LeaveApprovalQueueDto>("api/mobile/v1/leave/approvals", ct);

    /// <summary>Every page of the caller's own requests, newest first (Leave History sheet).</summary>
    public async Task<IReadOnlyList<LeaveRequestListItemDto>> GetAllMyRequestsAsync(CancellationToken ct = default)
    {
        var items = new List<LeaveRequestListItemDto>();
        string? cursor = null;

        do
        {
            var page = await GetMyRequestsAsync(cursor, ct);
            items.AddRange(page.Items);
            cursor = page.HasMore ? page.NextCursor : null;
        }
        while (cursor is not null && items.Count < 500);

        return items;
    }

    private static string Query(string? cursor) =>
        string.IsNullOrWhiteSpace(cursor) ? string.Empty : $"?cursor={Uri.EscapeDataString(cursor)}";
}

/// <summary>The caller's own employee record, and the change requests they raise against it.</summary>
public sealed class EmployeeApiClient(MobileApiClient api)
{
    public Task<EmployeeOverviewDto> GetOverviewAsync(string employeeId, CancellationToken ct = default) =>
        api.GetAsync<EmployeeOverviewDto>($"api/mobile/v1/employees/{employeeId}", ct);

    public Task<EmployeePersonalDto> GetPersonalAsync(string employeeId, CancellationToken ct = default) =>
        api.GetAsync<EmployeePersonalDto>($"api/mobile/v1/employees/{employeeId}/personal", ct);

    public Task<EmployeeEmploymentDto> GetEmploymentAsync(string employeeId, CancellationToken ct = default) =>
        api.GetAsync<EmployeeEmploymentDto>($"api/mobile/v1/employees/{employeeId}/employment", ct);

    public Task<EmployeeOrganisationDto> GetOrganisationAsync(string employeeId, CancellationToken ct = default) =>
        api.GetAsync<EmployeeOrganisationDto>($"api/mobile/v1/employees/{employeeId}/organisation", ct);

    /// <summary>
    /// Self-service edits are requests, not writes. The employee never touches the master
    /// record; HR applies or rejects what is raised here.
    /// </summary>
    public Task<MutationResultDto> RaiseChangeRequestAsync(
        string employeeId, CreateEmployeeChangeRequest request, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>(
            $"api/mobile/v1/employees/{employeeId}/change-requests", request, null, ct);
}

/// <summary>Gate passes and movement — the presence surfaces.</summary>
public sealed class PassesApiClient(MobileApiClient api)
{
    public Task<IReadOnlyList<GatePassRowDto>> GetMyPassesAsync(CancellationToken ct = default) =>
        api.GetAsync<IReadOnlyList<GatePassRowDto>>("api/mobile/v1/gate-passes/me", ct);

    public Task<GatePassRowDto> GetPassAsync(string passId, CancellationToken ct = default) =>
        api.GetAsync<GatePassRowDto>($"api/mobile/v1/gate-passes/{passId}", ct);

    /// <summary>
    /// The signed token a gate scans. Memory only — it is never written to local storage,
    /// because a credential cached on a device outlives the pass it belongs to.
    /// </summary>
    public Task<GatePassCredentialDto> GetCredentialAsync(string passId, CancellationToken ct = default) =>
        api.GetAsync<GatePassCredentialDto>($"api/mobile/v1/gate-passes/{passId}/credential", ct);

    public Task<MutationResultDto> RequestPassAsync(
        RequestGatePassRequest request, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>("api/mobile/v1/gate-passes", request, null, ct);

    public Task<MutationResultDto> WithdrawPassAsync(string passId, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>($"api/mobile/v1/gate-passes/{passId}/withdraw", null, null, ct);

    /// <summary>Passes waiting on the caller's decision (GATEPASS.APPROVE) — the W08 Gate Pass Approval List.</summary>
    public Task<GatePassBoardDto> GetPendingPassesAsync(CancellationToken ct = default) =>
        api.GetAsync<GatePassBoardDto>("api/mobile/v1/gate-passes/pending?pageSize=50", ct);

    public Task<MutationResultDto> DecidePassAsync(
        string passId, GatePassDecisionRequest request, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>($"api/mobile/v1/gate-passes/{passId}/decision", request, null, ct);

    public Task<IReadOnlyList<MovementRowDto>> GetMyMovementsAsync(CancellationToken ct = default) =>
        api.GetAsync<IReadOnlyList<MovementRowDto>>("api/mobile/v1/movement/me", ct);

    public Task<MutationResultDto> DeclareOutAsync(
        DeclareMovementRequest request, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>("api/mobile/v1/movement/out", request, null, ct);

    public Task<MutationResultDto> RecordReturnAsync(
        string movementId, ReturnMovementRequest request, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>(
            $"api/mobile/v1/movement/{movementId}/return", request, null, ct);
}

/// <summary>On duty: business travel that still counts as working time.</summary>
public sealed class OnDutyApiClient(MobileApiClient api)
{
    public Task<IReadOnlyList<OnDutyRowDto>> GetMineAsync(CancellationToken ct = default) =>
        api.GetAsync<IReadOnlyList<OnDutyRowDto>>("api/mobile/v1/on-duty/me", ct);

    /// <summary>Requests waiting on the caller's own decision (ONDUTY.APPROVE / TEAM.ONDUTY.APPROVE).</summary>
    public Task<IReadOnlyList<OnDutyRowDto>> GetPendingAsync(CancellationToken ct = default) =>
        api.GetAsync<IReadOnlyList<OnDutyRowDto>>("api/mobile/v1/on-duty/pending", ct);

    public Task<MutationResultDto> RequestAsync(
        RequestOnDutyRequest request, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>("api/mobile/v1/on-duty", request, null, ct);

    public Task<MutationResultDto> DecideAsync(
        string requestId, OnDutyDecisionRequest request, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>($"api/mobile/v1/on-duty/{requestId}/decision", request, null, ct);

    public Task<MutationResultDto> WithdrawAsync(string requestId, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>($"api/mobile/v1/on-duty/{requestId}/withdraw", null, null, ct);
}

/// <summary>
/// Recruitment: jobs, applications and candidate pipeline for HR staff.
/// Requires RECRUITMENT.VIEW; specific sub-actions require additional permissions.
/// </summary>
public sealed class RecruitmentApiClient(MobileApiClient api)
{
    public Task<CursorPage<JobListItemDto>> GetJobsAsync(
        string? status = null, CancellationToken ct = default) =>
        api.GetAsync<CursorPage<JobListItemDto>>(
            string.IsNullOrWhiteSpace(status)
                ? "api/mobile/v1/recruitment/jobs?pageSize=50"
                : $"api/mobile/v1/recruitment/jobs?pageSize=50&status={Uri.EscapeDataString(status)}", ct);

    public Task<JobDetailDto> GetJobAsync(long id, CancellationToken ct = default) =>
        api.GetAsync<JobDetailDto>($"api/mobile/v1/recruitment/jobs/{id}", ct);

    public Task<MutationResultDto> ChangeJobStatusAsync(
        long id,
        Contracts.Mobile.Recruitment.ChangeJobStatusRequest request,
        CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>($"api/mobile/v1/recruitment/jobs/{id}/status", request, null, ct);

    public Task<CursorPage<ApplicationListItemDto>> GetApplicationsAsync(
        long? jobId = null,
        string? stage = null,
        CancellationToken ct = default) =>
        api.GetAsync<CursorPage<ApplicationListItemDto>>(BuildApplicationsUrl(jobId, stage), ct);

    public Task<ApplicationDetailDto> GetApplicationAsync(long id, CancellationToken ct = default) =>
        api.GetAsync<ApplicationDetailDto>($"api/mobile/v1/recruitment/applications/{id}", ct);

    public Task<MutationResultDto> DecideApplicationAsync(
        long id,
        Contracts.Mobile.Recruitment.ApplicationDecisionRequest request,
        CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>($"api/mobile/v1/recruitment/applications/{id}/decision", request, null, ct);

    public Task<CandidateProfileDto> GetCandidateAsync(string candidateId, CancellationToken ct = default) =>
        api.GetAsync<CandidateProfileDto>($"api/mobile/v1/recruitment/candidates/{candidateId}", ct);

    public Task<IReadOnlyList<Contracts.Mobile.Recruitment.CandidateDocumentDto>> GetDocumentsAsync(
        string applicationId, CancellationToken ct = default) =>
        api.GetAsync<IReadOnlyList<Contracts.Mobile.Recruitment.CandidateDocumentDto>>(
            $"api/mobile/v1/recruitment/candidate-documents?applicationId={Uri.EscapeDataString(applicationId)}", ct);

    public Task<RecruitmentDashboardDto> GetDashboardAsync(CancellationToken ct = default) =>
        api.GetAsync<RecruitmentDashboardDto>("api/mobile/v1/recruitment/dashboard", ct);

    private static string BuildApplicationsUrl(long? jobId, string? stage)
    {
        var sb = new System.Text.StringBuilder("api/mobile/v1/recruitment/applications?pageSize=50");
        if (jobId.HasValue) sb.Append($"&jobId={jobId.Value}");
        if (!string.IsNullOrWhiteSpace(stage)) sb.Append($"&stage={Uri.EscapeDataString(stage)}");
        return sb.ToString();
    }
}

/// <summary>Interview scheduling and evaluation list for the Interview Status tab.</summary>
public sealed class InterviewsApiClient(MobileApiClient api)
{
    public Task<CursorPage<InterviewListItemDto>> GetInterviewsAsync(
        bool assignedToMeOnly = false,
        string? status = null,
        CancellationToken ct = default) =>
        api.GetAsync<CursorPage<InterviewListItemDto>>(BuildUrl(assignedToMeOnly, status), ct);

    public Task<InterviewDetailDto> GetInterviewAsync(string interviewId, CancellationToken ct = default) =>
        api.GetAsync<InterviewDetailDto>($"api/mobile/v1/recruitment/interviews/{interviewId}", ct);

    private static string BuildUrl(bool myOnly, string? status)
    {
        var sb = new System.Text.StringBuilder("api/mobile/v1/recruitment/interviews?pageSize=50");
        if (myOnly) sb.Append("&assignedToMeOnly=true");
        if (!string.IsNullOrWhiteSpace(status)) sb.Append($"&status={Uri.EscapeDataString(status)}");
        return sb.ToString();
    }
}

/// <summary>HR home: the recruitment dashboard KPIs.</summary>
public sealed class HrHomeApiClient(MobileApiClient api)
{
    public Task<RecruitmentDashboardDto> GetDashboardAsync(CancellationToken ct = default) =>
        api.GetAsync<RecruitmentDashboardDto>("api/mobile/v1/recruitment/dashboard", ct);
}

/// <summary>The bell, and what is behind it.</summary>
public sealed class NotificationsApiClient(MobileApiClient api)
{
    public Task<CursorPage<NotificationListItemDto>> ListAsync(
        string? cursor = null, CancellationToken ct = default) =>
        api.GetAsync<CursorPage<NotificationListItemDto>>(
            string.IsNullOrWhiteSpace(cursor)
                ? "api/mobile/v1/notifications"
                : $"api/mobile/v1/notifications?cursor={Uri.EscapeDataString(cursor)}", ct);

    public Task<NotificationDetailDto> GetAsync(string notificationId, CancellationToken ct = default) =>
        api.GetAsync<NotificationDetailDto>($"api/mobile/v1/notifications/{notificationId}", ct);

    public Task<MutationResultDto> MarkReadAsync(
        IReadOnlyList<string> notificationIds, CancellationToken ct = default) =>
        api.PostAsync<MutationResultDto>(
            "api/mobile/v1/notifications/read", new { notificationIds }, null, ct);

    public Task DeleteAsync(string notificationId, CancellationToken ct = default) =>
        api.SendNoContentAsync(
            HttpMethod.Delete, $"api/mobile/v1/notifications/{notificationId}", null, ct);
}
