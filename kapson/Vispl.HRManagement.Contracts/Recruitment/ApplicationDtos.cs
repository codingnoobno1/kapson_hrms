using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Recruitment;

public static class ApplicationStages
{
    public const string Applied = "Applied";
    public const string Screening = "Screening";
    public const string Shortlisted = "Shortlisted";
    public const string Interviewing = "Interviewing";
    public const string Offered = "Offered";
    public const string OfferAccepted = "OfferAccepted";
    public const string OfferDeclined = "OfferDeclined";
    public const string Onboarding = "Onboarding";
    public const string Hired = "Hired";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";

    public static IReadOnlyList<string> All { get; } =
    [
        Applied, Screening, Shortlisted, Interviewing, Offered,
        OfferAccepted, OfferDeclined, Onboarding, Hired, Rejected, Withdrawn
    ];

    /// <summary>Stages HR may move an application into, keyed by the current stage.</summary>
    private static readonly Dictionary<string, string[]> HrTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [Applied] = [Screening, Rejected],
        [Screening] = [Shortlisted, Rejected],
        [Shortlisted] = [Interviewing, Rejected],
        [Interviewing] = [Offered, Rejected],
        [Offered] = [Rejected]
    };

    public static bool IsHrTransitionAllowed(string from, string to) =>
        HrTransitions.TryGetValue(from, out var allowed) &&
        allowed.Contains(to, StringComparer.OrdinalIgnoreCase);

    /// <summary>Making or withdrawing an offer needs the stronger permission.</summary>
    public static bool RequiresManagePermission(string to) =>
        string.Equals(to, Offered, StringComparison.OrdinalIgnoreCase);

    public static bool IsRejection(string to) =>
        string.Equals(to, Rejected, StringComparison.OrdinalIgnoreCase);

    /// <summary>A candidate may withdraw up to and including Offered.</summary>
    public static bool CanCandidateWithdraw(string from) =>
        from is Applied or Screening or Shortlisted or Interviewing or Offered;

    public static string Tone(string stage) => stage switch
    {
        Hired or OfferAccepted or Shortlisted => "success",
        Rejected or OfferDeclined or Withdrawn => "danger",
        Offered or Interviewing => "info",
        Screening or Applied => "pending",
        _ => "neutral"
    };
}

public static class ApplicationDocumentRules
{
    public const string ResumeCategory = "Resume";
    public const long MaximumFileBytes = 10L * 1024 * 1024;
    public const long MaximumSubmissionBytes = 25L * 1024 * 1024;

    /// <summary>Content types accepted for a CV or supporting document.</summary>
    public static IReadOnlyDictionary<string, string[]> AllowedTypes { get; } =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = [".pdf"],
            ["application/msword"] = [".doc"],
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"],
            ["image/png"] = [".png"],
            ["image/jpeg"] = [".jpg", ".jpeg"]
        };

    public static bool IsAllowed(string contentType, string fileName)
    {
        if (!AllowedTypes.TryGetValue(contentType, out var extensions))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}

// ---- Public apply ---------------------------------------------------------

public sealed record ScreeningQuestionDto(
    string Key,
    string Prompt,
    string Type,
    bool IsRequired,
    IReadOnlyList<string> Options);

public sealed record PublicOpeningDto(
    long Id,
    string Slug,
    string Title,
    string Department,
    string Location,
    string EmploymentType,
    string WorkMode,
    string Experience,
    string Summary,
    IReadOnlyList<string> Responsibilities,
    IReadOnlyList<string> Requirements,
    string? SalaryRange,
    DateOnly? ExpiresOn,
    IReadOnlyList<RequiredDocumentDto> RequiredDocuments,
    IReadOnlyList<ScreeningQuestionDto> ScreeningQuestions);

public sealed record RequiredDocumentDto(string CategoryCode, string DisplayName);

public sealed class SubmitApplicationRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 7)]
    public string Phone { get; set; } = string.Empty;

    [Range(0, 60)]
    public decimal TotalExperienceYears { get; set; }

    [StringLength(180)]
    public string? CurrentLocation { get; set; }

    [Range(0, 365)]
    public int NoticePeriodDays { get; set; }

    [Range(0, 999999999)]
    public decimal? ExpectedCtc { get; set; }

    public Dictionary<string, string> Answers { get; set; } = [];

    /// <summary>Must be true. The submission is refused without explicit consent.</summary>
    public bool ConsentGranted { get; set; }

    [StringLength(40)]
    public string? ConsentPolicyVersion { get; set; }
}

public sealed record SubmitApplicationResultDto(
    long ApplicationId,
    string Reference,
    string Stage,
    DateTimeOffset AppliedUtc,
    bool WasUpdate);

/// <summary>An uploaded file handed to the service layer, decoupled from ASP.NET types.</summary>
public sealed record UploadedFile(
    string CategoryCode,
    string FileName,
    string ContentType,
    long Length,
    Stream Content);

// ---- HR review ------------------------------------------------------------

public sealed record ApplicationListItemDto(
    long Id,
    long JobOpeningId,
    string JobTitle,
    string Department,
    long CandidateId,
    string CandidateName,
    string? Email,
    string? Phone,
    decimal? Rating,
    string Stage,
    string StageTone,
    DateTimeOffset AppliedUtc);

public sealed record ApplicationDocumentDto(
    long Id,
    string CategoryCode,
    string FileName,
    string ContentType,
    long SizeBytes,
    string ScanState,
    bool IsDownloadable,
    DateTimeOffset UploadedUtc);

public sealed record ApplicationTimelineEntryDto(
    string FromStage,
    string ToStage,
    string? Reason,
    string ActorKind,
    DateTimeOffset OccurredUtc);

public sealed record ApplicationDetailDto(
    ApplicationListItemDto Summary,
    string? CurrentLocation,
    decimal TotalExperienceYears,
    int NoticePeriodDays,
    decimal? ExpectedCtc,
    IReadOnlyDictionary<string, string> Answers,
    IReadOnlyList<ApplicationDocumentDto> Documents,
    IReadOnlyList<ApplicationTimelineEntryDto> Timeline,
    IReadOnlyList<string> AllowedNextStages,
    string? RowVersion);

public sealed class ChangeStageRequest
{
    [Required, StringLength(40)]
    public string ToStage { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Reason { get; set; }
}

// ---- Onboarding -----------------------------------------------------------

public static class OnboardingTaskStatuses
{
    public const string Pending = "Pending";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public sealed record OnboardingTaskDto(
    long Id,
    string Code,
    string Title,
    string Instructions,
    string TaskType,
    string OwnerKind,
    bool IsRequired,
    string Status,
    long? DocumentId,
    string? DecisionReason,
    string? RowVersion);

public sealed record OnboardingDto(
    long Id,
    long ApplicationId,
    long CandidateId,
    string CandidateName,
    string JobTitle,
    string Stage,
    DateOnly? JoiningOn,
    decimal CompletionPercent,
    int OutstandingRequiredCount,
    IReadOnlyList<OnboardingTaskDto> Tasks,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc);

/// <summary>One application, as the person who submitted it is allowed to see it.</summary>
public sealed record CandidateApplicationDto(
    long ApplicationId,
    string Reference,
    string JobTitle,
    string Department,
    string Stage,
    string StageTone,

    /// <summary>Plain words for the stage. A candidate should not have to read our vocabulary.</summary>
    string StageExplanation,

    DateTimeOffset AppliedUtc,

    /// <summary>True while the offer is theirs to accept or decline.</summary>
    bool AwaitingMyDecision);

/// <summary>
/// What a candidate sees about themselves: their applications, and the checklist once one
/// exists. Nothing about anybody else, and nothing internal — no interviewer notes, no
/// ratings, no other applicants.
/// </summary>
public sealed record CandidateProgressDto(
    string CandidateName,
    string Email,
    IReadOnlyList<CandidateApplicationDto> Applications,
    OnboardingDto? Onboarding);

public sealed class OnboardingTaskDecisionRequest
{
    [Required, RegularExpression("^(approve|reject)$")]
    public string Decision { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Reason { get; set; }
}
