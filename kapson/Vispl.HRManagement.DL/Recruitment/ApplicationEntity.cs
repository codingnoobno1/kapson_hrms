namespace Vispl.HRManagement.DL.Recruitment;

public sealed class ApplicationEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;

    // Scalar identifiers only. No navigation properties across module boundaries.
    public long JobOpeningId { get; set; }
    public long CandidateId { get; set; }

    /// <summary>See ApplicationStage in the contracts. Persisted as its string name.</summary>
    public string Stage { get; set; } = "Applied";

    public string Source { get; set; } = "CareerSite";
    public int NoticePeriodDays { get; set; }
    public decimal? ExpectedCtc { get; set; }

    /// <summary>Answers to the opening's screening questions, as a JSON object.</summary>
    public string AnswersJson { get; set; } = "{}";

    public decimal? Rating { get; set; }
    public string? RejectionReason { get; set; }

    public bool ConsentGranted { get; set; }
    public string? ConsentPolicyVersion { get; set; }
    public DateTimeOffset? ConsentUtc { get; set; }

    public DateTimeOffset AppliedUtc { get; set; }
    public DateTimeOffset StageChangedUtc { get; set; }
    public string? StageChangedBy { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class ApplicationEventEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long ApplicationId { get; set; }
    public string FromStage { get; set; } = string.Empty;
    public string ToStage { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? ActorId { get; set; }
    public string ActorKind { get; set; } = "Hr";
    public DateTimeOffset OccurredUtc { get; set; }
}
