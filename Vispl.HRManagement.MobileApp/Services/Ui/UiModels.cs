namespace Vispl.HRManagement.MobileApp.Services.Ui;

/// <summary>
/// Presentation models for the reference screens in docs/screen-references/.
/// They describe what the design shows, which is deliberately wider than the
/// current mobile DTOs. Fields marked "not in DTO" in ScreenData are the
/// contract gaps phase two has to close.
/// </summary>

public enum JobState { Draft, Active, Inactive, Expired }

public enum ApplicantState { Shortlisted, PendingReview, Rejected }

public static class StateStyles
{
    public static string Css(JobState state) => state switch
    {
        JobState.Draft => "status-draft",
        JobState.Active => "status-active",
        JobState.Inactive => "status-inactive",
        _ => "status-expired"
    };

    public static string Label(JobState state) => state switch
    {
        JobState.Draft => "Draft",
        JobState.Active => "Active",
        JobState.Inactive => "Inactive",
        _ => "Expired"
    };

    public static string Css(ApplicantState state) => state switch
    {
        ApplicantState.Shortlisted => "status-shortlisted",
        ApplicantState.PendingReview => "status-pending",
        _ => "status-rejected"
    };

    public static string Label(ApplicantState state) => state switch
    {
        ApplicantState.Shortlisted => "Shortlisted",
        ApplicantState.PendingReview => "Pending Review",
        _ => "Rejected"
    };
}

public sealed record KpiModel(string Caption, string Value, string Icon, string Tone);

public sealed record AnalyticsRowModel(string Label, int Value, int Maximum);

public sealed record JobCardModel(
    long Id,
    string Title,
    string Department,
    string Location,
    DateOnly? ExpiresOn,
    string EmploymentType,
    string ExperienceBand,
    string? SalaryRange,
    JobState State,
    DateOnly? LastStatusUpdated);

public sealed record BulletGroup(string Lead, IReadOnlyList<string> Items);

public sealed record JobDetailModel(
    long Id,
    string Title,
    string Department,
    DateOnly? PostedOn,
    JobState State,
    string Location,
    string EmploymentType,
    string ExperienceBand,
    string? SalaryRange,
    string Description,
    IReadOnlyList<BulletGroup> Responsibilities,
    IReadOnlyList<BulletGroup> Qualifications);

public sealed record InterviewModel(
    string Id,
    string CandidateName,
    string Role,
    DateOnly Date,
    TimeOnly Time,
    string Round,
    string StatusLabel,

    // Whether cancelling is still meaningful. One already cancelled or completed is history,
    // and offering the action on it would only produce a refusal.
    bool CanCancel = false);

public sealed record ApplicantModel(
    string Id,
    string Name,
    string RoleAndDepartment,
    string? Email,
    string? Phone,
    decimal? Rating,
    ApplicantState State,

    // The server's own stage value, not the three-way ApplicantState the card colours by.
    // Which moves are legal depends on the exact stage, so collapsing eleven of them into
    // three would make the actions guesswork.
    string Stage = "Applied");

public sealed record AddressModel(
    string Line,
    string City,
    string State,
    string Pincode);

public sealed record ApplicationDetailModel(
    string Id,
    string CandidateName,
    string Role,
    DateOnly AppliedOn,
    string StatusLabel,
    IReadOnlyList<(string Label, string Value)> PersonalInformation,
    IReadOnlyList<(string Label, string Value)> ProfessionalDetails,
    AddressModel Address);

public sealed record NotificationModel(
    string Id,
    string Title,
    TimeOnly Time,
    DateOnly Date,
    string Body);

public sealed record DashboardModel(
    IReadOnlyList<KpiModel> Kpis,
    IReadOnlyList<InterviewModel> Interviews,
    IReadOnlyList<AnalyticsRowModel> JobTitleAnalytics);
