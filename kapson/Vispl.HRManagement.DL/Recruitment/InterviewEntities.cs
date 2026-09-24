namespace Vispl.HRManagement.DL.Recruitment;

public sealed class InterviewEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;

    public long ApplicationId { get; set; }
    public long CandidateId { get; set; }
    public long JobOpeningId { get; set; }

    /// <summary>Master code for the round, e.g. SCREENING or TECHNICAL.</summary>
    public string RoundCode { get; set; } = string.Empty;

    /// <summary>Display label as the reference card shows it, e.g. "Round 1 - Screening".</summary>
    public string RoundLabel { get; set; } = string.Empty;

    public DateTimeOffset StartUtc { get; set; }
    public int DurationMinutes { get; set; } = 60;

    /// <summary>IANA zone the interview was scheduled in, so it renders in local time.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Online, Onsite or Phone.</summary>
    public string Mode { get; set; } = "Online";

    /// <summary>A meeting link for Online, an address for Onsite.</summary>
    public string? LocationOrLink { get; set; }

    public string? Instructions { get; set; }

    /// <summary>Scheduled, Completed, Cancelled or NoShow.</summary>
    public string Status { get; set; } = "Scheduled";

    public string? CancellationReasonCode { get; set; }
    public string? CancellationComment { get; set; }

    public string? ScheduledBy { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class InterviewPanelistEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long InterviewId { get; set; }

    /// <summary>Identity account of the panel member. Scalar, not a navigation property.</summary>
    public string PanelMemberId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public bool IsLead { get; set; }
    public DateTimeOffset AddedUtc { get; set; }
}

public sealed class InterviewEvaluationEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long InterviewId { get; set; }

    /// <summary>One evaluation per panel member per interview.</summary>
    public string PanelMemberId { get; set; } = string.Empty;

    /// <summary>Scores per criterion, as a JSON array.</summary>
    public string ScoresJson { get; set; } = "[]";

    public string? Recommendation { get; set; }
    public string? Notes { get; set; }

    /// <summary>Draft or Submitted. A submitted evaluation is no longer editable.</summary>
    public string State { get; set; } = "Draft";

    public DateTimeOffset? SubmittedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
