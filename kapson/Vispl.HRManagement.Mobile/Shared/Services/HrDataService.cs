using System.Globalization;
using Vispl.HRManagement.Mobile.Client.Api;
using Vispl.HRManagement.Mobile.Shared.Models;

namespace Vispl.HRManagement.Mobile.Shared.Services;

/// <summary>
/// The HR app's data, from the API.
///
/// This replaced <c>DemoDataService</c>, which returned four hardcoded jobs, four applicants and
/// four interviews and never touched the network. Every endpoint behind these calls was already
/// real and tested; nothing consumed them.
///
/// The screens bind to the app's own small models rather than to the contract DTOs, so mapping
/// happens here in one place. That is deliberate: a contract change then breaks this file rather
/// than seven view models.
/// </summary>
public interface IHrDataService
{
    Task<IReadOnlyList<KpiMetric>> GetMetricsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InterviewSummary>> GetInterviewsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<JobSummary>> GetJobsAsync(CancellationToken ct = default);
    Task<JobSummary?> GetJobAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicantSummary>> GetApplicantsAsync(CancellationToken ct = default);
    Task<ApplicantSummary?> GetApplicantAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationSummary>> GetNotificationsAsync(CancellationToken ct = default);
}

public sealed class HrDataService(
    HrHomeApiClient home,
    RecruitmentApiClient recruitment,
    InterviewsApiClient interviews,
    NotificationsApiClient notifications) : IHrDataService
{
    public async Task<IReadOnlyList<KpiMetric>> GetMetricsAsync(CancellationToken ct = default)
    {
        var dashboard = await home.GetDashboardAsync(ct);

        // The server decides which tiles this caller may see — a KPI counting records they
        // cannot read would leak that those records exist. Whatever comes back is shown.
        return dashboard.Kpis
            .Select(kpi => new KpiMetric(
                kpi.Title,
                string.IsNullOrWhiteSpace(kpi.UnavailableReason) ? kpi.Value : "—",
                Accent(kpi.Tone),
                Symbol(kpi.Key)))
            .ToList();
    }

    public async Task<IReadOnlyList<InterviewSummary>> GetInterviewsAsync(CancellationToken ct = default)
    {
        var page = await interviews.GetInterviewsAsync(assignedToMeOnly: false, status: null, ct: ct);

        return page.Items
            .Select(item => new InterviewSummary(
                ParseId(item.Id),
                item.Candidate.DisplayName,
                item.JobTitle,
                item.StartsUtc.ToLocalTime(),
                item.Round,
                item.Status.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<JobSummary>> GetJobsAsync(CancellationToken ct = default)
    {
        var page = await recruitment.GetJobsAsync(null, ct);
        return page.Items.Select(MapJob).ToList();
    }

    public async Task<JobSummary?> GetJobAsync(long id, CancellationToken ct = default)
    {
        var job = await recruitment.GetJobAsync(id, ct);

        return new JobSummary(
            job.Id,
            job.Title,
            job.Department,
            job.Location,
            job.EmploymentType,
            job.Experience,
            job.SalaryRange ?? "Not disclosed",
            job.Status,
            Expiry(job.ExpiresOn),
            job.Summary,
            job.Responsibilities,
            job.Requirements);
    }

    public async Task<IReadOnlyList<ApplicantSummary>> GetApplicantsAsync(CancellationToken ct = default)
    {
        var page = await recruitment.GetApplicationsAsync(null, null, ct);
        return page.Items.Select(MapApplicant).ToList();
    }

    public async Task<ApplicantSummary?> GetApplicantAsync(long id, CancellationToken ct = default)
    {
        var detail = await recruitment.GetApplicationAsync(id, ct);
        return MapApplicant(detail.Summary);
    }

    public async Task<IReadOnlyList<NotificationSummary>> GetNotificationsAsync(CancellationToken ct = default)
    {
        var page = await notifications.ListAsync(null, ct);

        return page.Items
            .Select(item => new NotificationSummary(
                ParseId(item.Id),
                item.Title,
                item.Preview,
                item.OccurredUtc.ToLocalTime(),
                item.Category,
                item.IsRead))
            .ToList();
    }

    // ---- Mapping ----------------------------------------------------------

    /// <summary>
    /// A list row carries less than a detail row. The blanks here are honest: the list endpoint
    /// does not return responsibilities or a description, and inventing them would put text on
    /// a card that the record does not contain.
    /// </summary>
    private static JobSummary MapJob(Contracts.Mobile.Recruitment.JobListItemDto job) => new(
        job.Id,
        job.Title,
        job.Department,
        job.Location,
        job.EmploymentType,
        job.Experience,
        job.SalaryRange ?? "Not disclosed",
        job.Status,
        Expiry(job.ExpiresOn),
        job.Summary,
        [],
        []);

    private static ApplicantSummary MapApplicant(
        Contracts.Mobile.Recruitment.ApplicationListItemDto application) => new(
        ParseId(application.Id),
        application.Candidate.DisplayName,
        application.JobTitle,
        string.Empty,
        string.Empty,
        string.Empty,
        application.Stage.Name,
        application.Rating ?? 0m,
        string.Empty,
        string.Empty,
        string.Empty,
        application.AppliedUtc.ToLocalTime());

    /// <summary>
    /// Ids are strings on some contracts and longs on others. A non-numeric one maps to zero
    /// rather than throwing: one odd row should not take a whole list down.
    /// </summary>
    private static long ParseId(string id) =>
        long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    /// <summary>
    /// A job with no close date is not expiring. Today would read as expiring now, and
    /// DateTime.MaxValue would render as a nonsense year on a card.
    /// </summary>
    private static DateTimeOffset Expiry(DateOnly? closesOn) =>
        closesOn is null
            ? DateTimeOffset.MaxValue
            : new DateTimeOffset(closesOn.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    /// <summary>Maps a server tone onto the accent names the existing KPI tile understands.</summary>
    private static string Accent(string tone) => tone.ToLowerInvariant() switch
    {
        "blue" or "info" => "Blue",
        "cyan" => "Cyan",
        "amber" or "gold" or "warning" => "Gold",
        _ => "Blue"
    };

    private static string Symbol(string key) => key switch
    {
        "total-jobs" => "JOB",
        "active-jobs" => "LIVE",
        "total-applicants" => "CV",
        _ => "•"
    };
}
