using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.MobileApp.Services.Clients;

// Both contract namespaces define an application list DTO. The recruitment one is the
// server contract this app consumes; alias it so the mobile one stays unambiguous.
using HrApplication = Vispl.HRManagement.Contracts.Recruitment.ApplicationListItemDto;
using OnboardingDto = Vispl.HRManagement.Contracts.Recruitment.OnboardingDto;

namespace Vispl.HRManagement.MobileApp.Services.Ui;

public sealed record ScreenResult<T>(T? Data, bool IsSample, ApiOutcome Outcome, ProblemInfo? Problem)
{
    public bool HasData => Data is not null;

    public static ScreenResult<T> Sample(T data) => new(data, true, ApiOutcome.Success, null);
    public static ScreenResult<T> Live(T data) => new(data, false, ApiOutcome.Success, null);
    public static ScreenResult<T> Failed(ApiOutcome outcome, ProblemInfo? problem) => new(default, false, outcome, problem);
}

/// <summary>
/// Feeds the reference screens. When sample mode is on the screens render the
/// design-reference content so the UI can be reviewed before the API exists;
/// the shell shows a persistent banner in that mode so it is never mistaken for
/// real data. When it is off, every screen calls the real endpoint and reports
/// honestly what the server returned.
/// </summary>
public sealed class ScreenData(
    JobsApi jobsApi,
    ApplicationsApi applicationsApi,
    InterviewsApi interviewsApi,
    HomeApi homeApi,
    FeatureApi featureApi)
{
    private const string SampleModePreference = "vispl.hr.sample_mode";

    public event Action? Changed;

    public bool UseSampleData
    {
        get => Preferences.Default.Get(SampleModePreference, false);
        set
        {
            Preferences.Default.Set(SampleModePreference, value);
            Changed?.Invoke();
        }
    }

    // ---- REF-01 Dashboard -------------------------------------------------

    public async Task<ScreenResult<DashboardModel>> GetDashboardAsync(CancellationToken ct = default)
    {
        if (UseSampleData)
        {
            return ScreenResult<DashboardModel>.Sample(SampleContent.Dashboard);
        }

        var home = await homeApi.GetHrHomeAsync(ct);
        if (!home.IsSuccess || home.Data is null)
        {
            return ScreenResult<DashboardModel>.Failed(home.Outcome, home.Problem);
        }

        // The dashboard's interview strip and analytics come from the same endpoints the
        // dedicated screens use, so the numbers cannot disagree between screens.
        var upcoming = await interviewsApi.GetAsync(pageSize: 5, cancellationToken: ct);
        var interviewCards = upcoming.IsSuccess && upcoming.Data is not null
            ? upcoming.Data.Items.Select(MapInterview).ToList()
            : [];

        var jobs = await jobsApi.GetAsync(pageSize: 50, cancellationToken: ct);
        var analytics = jobs.IsSuccess && jobs.Data is not null
            ? jobs.Data.Items
                .GroupBy(job => job.Title)
                .Select(group => new AnalyticsRowModel(group.Key, group.Count(), 1))
                .OrderByDescending(row => row.Value)
                .Take(6)
                .ToList()
            : [];

        // Bars are relative to the largest value, so the widest bar is always full.
        var maximum = analytics.Count == 0 ? 1 : analytics.Max(row => row.Value);
        analytics = analytics.Select(row => row with { Maximum = maximum }).ToList();

        var kpis = home.Data.Kpis
            .Select(kpi => new KpiModel(kpi.Title, kpi.Value, IconFor(kpi.Key), kpi.Tone))
            .ToList();

        return ScreenResult<DashboardModel>.Live(new DashboardModel(kpis, interviewCards, analytics));
    }

    private static string IconFor(string key) => key switch
    {
        "total-jobs" => "clipboard",
        "active-jobs" => "file-check",
        "total-applicants" => "users",
        _ => "bar-chart"
    };

    // ---- REF-02 Manage Jobs ----------------------------------------------

    public async Task<ScreenResult<IReadOnlyList<JobCardModel>>> GetJobsAsync(
        string? query,
        CancellationToken ct = default)
    {
        if (UseSampleData)
        {
            var filtered = SampleContent.Jobs
                .Where(job => string.IsNullOrWhiteSpace(query) ||
                              job.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return ScreenResult<IReadOnlyList<JobCardModel>>.Sample(filtered);
        }

        var result = await jobsApi.GetAsync(query: query, cancellationToken: ct);
        if (!result.IsSuccess || result.Data is null)
        {
            return ScreenResult<IReadOnlyList<JobCardModel>>.Failed(result.Outcome, result.Problem);
        }

        var mapped = result.Data.Items.Select(MapJob).ToList();
        return ScreenResult<IReadOnlyList<JobCardModel>>.Live(mapped);
    }

    public async Task<ScreenResult<JobDetailModel>> GetJobAsync(long id, CancellationToken ct = default)
    {
        if (UseSampleData)
        {
            var match = SampleContent.JobDetails.FirstOrDefault(job => job.Id == id)
                        ?? SampleContent.JobDetails[0];
            return ScreenResult<JobDetailModel>.Sample(match);
        }

        var result = await jobsApi.GetByIdAsync(id, ct);
        if (!result.IsSuccess || result.Data is null)
        {
            return ScreenResult<JobDetailModel>.Failed(result.Outcome, result.Problem);
        }

        var dto = result.Data;
        var model = new JobDetailModel(
            dto.Id,
            dto.Title,
            dto.Department,
            dto.PublishedUtc.HasValue ? DateOnly.FromDateTime(dto.PublishedUtc.Value.LocalDateTime) : null,
            dto.IsPublished ? JobState.Active : JobState.Draft,
            dto.Location,
            dto.EmploymentType,
            dto.Experience,
            null, // SalaryRange is not carried by JobDetailDto.
            dto.Summary,
            dto.Responsibilities.Count == 0 ? [] : [new BulletGroup("Responsibilities", dto.Responsibilities)],
            dto.Requirements.Count == 0 ? [] : [new BulletGroup("Requirements", dto.Requirements)]);

        return ScreenResult<JobDetailModel>.Live(model);
    }

    // ---- REF-04 / REF-09 Interviews --------------------------------------

    public async Task<ScreenResult<IReadOnlyList<InterviewModel>>> GetInterviewsAsync(CancellationToken ct = default)
    {
        if (UseSampleData)
        {
            return ScreenResult<IReadOnlyList<InterviewModel>>.Sample(SampleContent.Interviews);
        }

        var result = await interviewsApi.GetAsync(cancellationToken: ct);
        if (!result.IsSuccess || result.Data is null)
        {
            return ScreenResult<IReadOnlyList<InterviewModel>>.Failed(result.Outcome, result.Problem);
        }

        return ScreenResult<IReadOnlyList<InterviewModel>>.Live(
            result.Data.Items.Select(MapInterview).ToList());
    }

    /// <summary>
    /// The card shows local date and time. The server sends UTC, so the conversion happens
    /// once here rather than in each widget.
    /// </summary>
    private static InterviewModel MapInterview(InterviewListItemDto item)
    {
        var local = item.StartsUtc.ToLocalTime();

        return new InterviewModel(
            item.Id,
            item.Candidate.DisplayName,
            item.JobTitle,
            DateOnly.FromDateTime(local.DateTime),
            TimeOnly.FromDateTime(local.DateTime),
            item.Round,
            item.Status.Name,

            // Scheduled and rescheduled are the live states; anything else has already
            // happened or already been called off.
            item.Status.Code is "Scheduled" or "Rescheduled");
    }

    public Task<ApiResult<MutationResultDto>> CancelInterviewAsync(
        string interviewId, string reasonCode, string? comment, CancellationToken ct = default) =>
        interviewsApi.CancelAsync(interviewId, reasonCode, comment, ct);

    // ---- REF-06 / REF-07 Applicants --------------------------------------

    public async Task<ScreenResult<IReadOnlyList<ApplicantModel>>> GetApplicantsAsync(
        string? query,
        ApplicantState? state,
        CancellationToken ct = default)
    {
        if (UseSampleData)
        {
            var filtered = SampleContent.Applicants
                .Where(a => state is null || a.State == state)
                .Where(a => string.IsNullOrWhiteSpace(query) ||
                            a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            a.RoleAndDepartment.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return ScreenResult<IReadOnlyList<ApplicantModel>>.Sample(filtered);
        }

        var stageFilter = state switch
        {
            ApplicantState.Shortlisted => "Shortlisted",
            ApplicantState.Rejected => "Rejected",
            _ => null
        };

        var result = await applicationsApi.GetAsync(stage: stageFilter, query: query, cancellationToken: ct);
        if (!result.IsSuccess || result.Data is null)
        {
            return ScreenResult<IReadOnlyList<ApplicantModel>>.Failed(result.Outcome, result.Problem);
        }

        var mapped = result.Data.Items.Select(MapApplicant).ToList();

        // PendingReview covers every stage that is neither shortlisted nor rejected,
        // so it is filtered here rather than pushed onto the server.
        if (state == ApplicantState.PendingReview)
        {
            mapped = mapped.Where(item => item.State == ApplicantState.PendingReview).ToList();
        }

        return ScreenResult<IReadOnlyList<ApplicantModel>>.Live(mapped);
    }

    public async Task<ScreenResult<ApplicationDetailModel>> GetApplicationAsync(
        string id, CancellationToken ct = default)
    {
        if (UseSampleData)
        {
            var match = SampleContent.Applications.FirstOrDefault(a => a.Id == id)
                        ?? SampleContent.Applications[0];
            return ScreenResult<ApplicationDetailModel>.Sample(match);
        }

        if (!long.TryParse(id, out var applicationId))
        {
            return ScreenResult<ApplicationDetailModel>.Failed(ApiOutcome.NotFound, null);
        }

        var result = await applicationsApi.GetByIdAsync(applicationId, ct);
        if (!result.IsSuccess || result.Data is null)
        {
            return ScreenResult<ApplicationDetailModel>.Failed(result.Outcome, result.Problem);
        }

        var detail = result.Data;
        var summary = detail.Summary;

        // The application contract carries what the candidate submitted. Demographic and
        // address fields belong to onboarding, so those sections stay empty until then.
        var personal = new List<(string, string)>
        {
            ("Email", summary.Email ?? "—"),
            ("Phone", summary.Phone ?? "—"),
            ("Location", detail.CurrentLocation ?? "—")
        };

        var professional = new List<(string, string)>
        {
            ("Experience", $"{detail.TotalExperienceYears:0.#} years"),
            ("Notice period", $"{detail.NoticePeriodDays} days"),
            ("Expected salary", detail.ExpectedCtc.HasValue ? detail.ExpectedCtc.Value.ToString("#,##0") : "—"),
            ("Documents", detail.Documents.Count.ToString())
        };

        foreach (var answer in detail.Answers)
        {
            professional.Add((answer.Key, answer.Value));
        }

        return ScreenResult<ApplicationDetailModel>.Live(new ApplicationDetailModel(
            summary.Id.ToString(),
            summary.CandidateName,
            $"{summary.JobTitle} - {summary.Department}",
            DateOnly.FromDateTime(summary.AppliedUtc.LocalDateTime),
            summary.Stage,
            personal,
            professional,
            new AddressModel("Provided during onboarding", "—", "—", "—")));
    }

    // ---- Onboarding -------------------------------------------------------

    public async Task<ScreenResult<OnboardingDto>> GetOnboardingAsync(
        long applicationId, CancellationToken ct = default)
    {
        var result = await applicationsApi.GetOnboardingByApplicationAsync(applicationId, ct);
        return result.IsSuccess && result.Data is not null
            ? ScreenResult<OnboardingDto>.Live(result.Data)
            : ScreenResult<OnboardingDto>.Failed(result.Outcome, result.Problem);
    }

    public Task<ApiResult<MutationResultDto>> DecideOnboardingTaskAsync(
        long onboardingId, long taskId, string decision, string? reason, CancellationToken ct = default) =>
        applicationsApi.DecideTaskAsync(onboardingId, taskId, decision, reason, ct);

    public Task<ApiResult<MutationResultDto>> ChangeApplicationStageAsync(
        long applicationId, string toStage, string? reason, CancellationToken ct = default) =>
        applicationsApi.ChangeStageAsync(applicationId, toStage, reason, ct);

    private static ApplicantModel MapApplicant(HrApplication item) => new(
        item.Id.ToString(),
        item.CandidateName,
        $"{item.JobTitle} - {item.Department}",
        item.Email,
        item.Phone,
        item.Rating,
        item.Stage switch
        {
            "Shortlisted" or "Offered" or "OfferAccepted" or "Onboarding" or "Hired" => ApplicantState.Shortlisted,
            "Rejected" or "OfferDeclined" or "Withdrawn" => ApplicantState.Rejected,
            _ => ApplicantState.PendingReview
        },
        item.Stage);

    // ---- REF-08 Notifications --------------------------------------------

    public async Task<ScreenResult<IReadOnlyList<NotificationModel>>> GetNotificationsAsync(CancellationToken ct = default)
    {
        if (UseSampleData)
        {
            return ScreenResult<IReadOnlyList<NotificationModel>>.Sample(SampleContent.Notifications);
        }

        var result = await homeApi.GetNotificationsAsync(cancellationToken: ct);
        if (!result.IsSuccess || result.Data is null)
        {
            return ScreenResult<IReadOnlyList<NotificationModel>>.Failed(result.Outcome, result.Problem);
        }

        return ScreenResult<IReadOnlyList<NotificationModel>>.Live(
            result.Data.Items.Select(MapNotification).ToList());
    }

    public Task<ApiResult<MutationResultDto>> MarkAllNotificationsReadAsync(CancellationToken ct = default) =>
        homeApi.MarkAllReadAsync(ct);

    private static NotificationModel MapNotification(NotificationListItemDto item)
    {
        var local = item.OccurredUtc.ToLocalTime();

        return new NotificationModel(
            item.Id,
            item.Title,
            TimeOnly.FromDateTime(local.DateTime),
            DateOnly.FromDateTime(local.DateTime),
            item.Preview);
    }

    // ----------------------------------------------------------------------

    private async Task<ApiResult<Contracts.Mobile.Shared.MobileFeatureStatusDto>> ProbeAsync(
        string moduleKey,
        CancellationToken ct)
    {
        var module = ApiCatalog.Find(moduleKey);
        return module is null
            ? ApiResult<Contracts.Mobile.Shared.MobileFeatureStatusDto>.Failure(ApiOutcome.NotFound)
            : await featureApi.ProbeAsync(module, ct);
    }

    // JobListItemDto carries no salary, no expiry date, and only a published
    // flag, so three of the reference card's fields cannot be populated live.
    private static JobCardModel MapJob(JobListItemDto dto) => new(
        dto.Id,
        dto.Title,
        dto.Department,
        dto.Location,
        null,                                   // ExpiresOn — not in DTO
        dto.EmploymentType,
        dto.Experience,
        null,                                   // SalaryRange — not in DTO
        dto.IsPublished ? JobState.Active : JobState.Draft,  // only 2 of 4 states
        DateOnly.FromDateTime(dto.UpdatedUtc.LocalDateTime));
}
