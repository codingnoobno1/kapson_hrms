namespace Vispl.HRManagement.DL.Recruitment;

public sealed class OnboardingEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long ApplicationId { get; set; }
    public long CandidateId { get; set; }

    /// <summary>InProgress, Completed, or Cancelled.</summary>
    public string Stage { get; set; } = "InProgress";

    public DateOnly? JoiningOn { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class OnboardingTaskEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long OnboardingId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    /// <summary>"Document" or "Form".</summary>
    public string TaskType { get; set; } = "Document";

    /// <summary>"Candidate" or "Hr".</summary>
    public string OwnerKind { get; set; } = "Candidate";

    public bool IsRequired { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>Pending, Submitted, Approved, or Rejected.</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Set when TaskType is Document and the candidate has uploaded.</summary>
    public long? DocumentId { get; set; }

    /// <summary>Set when TaskType is Form. A JSON object of the submitted values.</summary>
    public string? FormValuesJson { get; set; }

    public string? DecisionReason { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
