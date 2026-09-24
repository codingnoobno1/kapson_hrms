using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Recruitment;
using Vispl.HRManagement.DL.ITF.Recruitment;
using Vispl.HRManagement.DL.ITF.Shared;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// REF-01. A projection over the modules that are built — nothing is stored for the dashboard
/// itself, so it cannot drift from the data it summarises.
///
/// Every tile is permission-gated: a KPI counting records the caller may not read would leak
/// their existence, so a tile the caller cannot open is not shown at all.
/// </summary>
public sealed class MobileHomeService(
    IJobOpeningRepository openings,
    IApplicationRepository applications,
    IInterviewRepository interviews,
    INotificationRepository notifications) : IMobileHomeService
{
    public async Task<HrHomeDto> GetHrHomeAsync(MobileCallerContext caller, CancellationToken ct)
    {
        var canSeeRecruitment = caller.Has("RECRUITMENT.VIEW");
        var canSeeApplications = caller.Has("RECRUITMENT.APPLICATIONS");
        var canSeeInterviews = caller.Has("RECRUITMENT.INTERVIEWS");

        var kpis = new List<KpiDto>();
        var alerts = new List<string>();

        if (canSeeRecruitment)
        {
            var jobs = await openings.GetAllAsync(caller.TenantKey, ct);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var active = jobs.Count(job => IsActive(job.Status, job.IsPublished, job.ExpiresOn, today));
            var expiring = jobs.Count(job =>
                job.ExpiresOn is { } expires &&
                expires >= today &&
                expires <= today.AddDays(14) &&
                IsActive(job.Status, job.IsPublished, job.ExpiresOn, today));

            kpis.Add(new KpiDto("total-jobs", "Total Jobs", jobs.Count.ToString(), null, null, "blue", "/jobs"));
            kpis.Add(new KpiDto("active-jobs", "Active Jobs", active.ToString(), null, null, "cyan", "/jobs"));

            if (expiring > 0)
            {
                alerts.Add($"{expiring} opening{(expiring == 1 ? "" : "s")} close within two weeks.");
            }
        }

        if (canSeeApplications)
        {
            var all = await applications.GetSummariesAsync(caller.TenantKey, null, null, ct);

            var awaiting = all.Count(row =>
                row.Application.Stage is ApplicationStages.Applied or ApplicationStages.Screening);

            kpis.Add(new KpiDto(
                "total-applicants", "Total Applicants", all.Count.ToString(), null, null, "amber", "/applicants"));

            if (awaiting > 0)
            {
                alerts.Add($"{awaiting} application{(awaiting == 1 ? "" : "s")} awaiting review.");
            }
        }

        var quickActions = new List<QuickActionDto>();

        if (canSeeRecruitment)
        {
            quickActions.Add(new QuickActionDto("jobs", "Job Openings", "📋", "/jobs", "RECRUITMENT.VIEW", true));
        }

        if (canSeeApplications)
        {
            quickActions.Add(new QuickActionDto(
                "applicants", "Applicants", "👥", "/applicants", "RECRUITMENT.APPLICATIONS", true));
        }

        if (canSeeInterviews)
        {
            quickActions.Add(new QuickActionDto(
                "interviews", "Interviews", "🗓️", "/interviews", "RECRUITMENT.INTERVIEWS", true));
        }

        var unread = await notifications.CountUnreadAsync(caller.TenantKey, caller.UserId, ct);
        if (unread > 0)
        {
            alerts.Add($"{unread} unread notification{(unread == 1 ? "" : "s")}.");
        }

        return new HrHomeDto(kpis, alerts, quickActions);
    }

    public Task<EmployeeHomeDto> GetEmployeeHomeAsync(MobileCallerContext caller, CancellationToken ct) =>
        // Attendance and leave balances are the substance of this screen and neither module
        // exists. An empty shell would read as "you have no leave", which is worse than saying
        // the feature is not ready.
        throw new MobileFeatureNotReadyException(
            "Employee Home", "attendance module", "leave module");

    public Task<ManagerHomeDto> GetManagerHomeAsync(MobileCallerContext caller, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Manager Home", "reporting hierarchy", "team attendance", "team leave");

    private static bool IsActive(string status, bool isPublished, DateOnly? expiresOn, DateOnly today) =>
        RecruitmentRules.IsOpeningActive(status, isPublished, expiresOn, today);
}
