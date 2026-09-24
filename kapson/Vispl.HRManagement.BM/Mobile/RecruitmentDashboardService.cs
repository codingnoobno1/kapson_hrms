using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Recruitment;
using Vispl.HRManagement.DL.ITF.Recruitment;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// The recruitment picture in one call: headline counts, how the pipeline is distributed, and
/// what is scheduled next.
///
/// It reads the same repositories the individual recruitment screens use, so a figure here can
/// always be reconciled by opening the screen behind it. Counts that would disagree with the HR
/// home screen come from <see cref="RecruitmentRules"/>, which both share.
/// </summary>
public sealed class RecruitmentDashboardService(
    IJobOpeningRepository openings,
    IApplicationRepository applications,
    IInterviewRepository interviews) : IRecruitmentDashboardService
{
    private const string ViewPermission = "RECRUITMENT.VIEW";

    public async Task<RecruitmentDashboardDto> GetAsync(MobileCallerContext caller, CancellationToken ct)
    {
        if (!caller.Has(ViewPermission))
        {
            throw new MobileAccessDeniedException(ViewPermission);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var jobs = await openings.GetAllAsync(caller.TenantKey, ct);
        var active = jobs.Count(job =>
            RecruitmentRules.IsOpeningActive(job.Status, job.IsPublished, job.ExpiresOn, today));

        var kpis = new List<KpiDto>
        {
            new("total-jobs", "Total Jobs", jobs.Count.ToString(), null, null, "blue", "/jobs"),
            new("active-jobs", "Active Jobs", active.ToString(), null, null, "cyan", "/jobs")
        };

        // Applications are a separate grant: a recruiter may plan openings without being
        // allowed to see who applied, so the pipeline stays empty rather than leaking counts.
        var pipeline = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (caller.Has("RECRUITMENT.APPLICATIONS"))
        {
            var all = await applications.GetSummariesAsync(caller.TenantKey, null, null, ct);

            kpis.Add(new KpiDto(
                "total-applicants", "Total Applicants", all.Count.ToString(), null, null, "amber", "/applicants"));

            foreach (var group in all.GroupBy(row => row.Application.Stage))
            {
                var stage = string.IsNullOrWhiteSpace(group.Key) ? "Unknown" : group.Key;
                pipeline[stage] = group.Count();
            }
        }

        var upcoming = await interviews.QueryAsync(
            caller.TenantKey,
            new InterviewFilter(today, null, null, null, null),
            ct);

        // Nearest first, and only a handful: this is a dashboard strip, not the schedule screen.
        var next = upcoming
            .OrderBy(row => row.Interview.StartUtc)
            .Take(5)
            .Select(InterviewService.MapListItem)
            .ToList();

        return new RecruitmentDashboardDto(kpis, pipeline, next);
    }
}
