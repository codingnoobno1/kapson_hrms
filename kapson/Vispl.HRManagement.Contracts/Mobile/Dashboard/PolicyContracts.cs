using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Mobile.Dashboard;

/// <summary>
/// One policy card. <c>IsAcknowledged</c> is this caller's own read state against the policy's
/// current <c>Version</c> — a caller who read version 1 sees <c>false</c> again the moment
/// version 2 is published, because their acknowledgement of the old text says nothing about the
/// new one.
/// </summary>
public sealed record PolicySummaryDto(
    string Id,
    string Code,
    string Title,
    string Icon,
    string Excerpt,
    string Version,
    DateOnly UpdatedOn,
    bool IsPublished,
    bool IsAcknowledged,
    DateTimeOffset? AcknowledgedUtc);

/// <summary>
/// One numbered block inside a section — "1. Casual Leave (CL)" and the bullets under it.
/// </summary>
public sealed record PolicySubsectionDto(string Label, IReadOnlyList<string> Points);

/// <summary>
/// A section renders as: heading, an optional lead line, its own bullets, then any numbered
/// subsections. Most policies only need the heading and bullets; leave-type tables need the rest.
/// </summary>
public sealed record PolicySectionDto(
    string Heading,
    IReadOnlyList<string> Points,
    string? Intro = null,
    IReadOnlyList<PolicySubsectionDto>? Subsections = null);

public sealed record PolicyDetailDto(
    PolicySummaryDto Summary,
    IReadOnlyList<PolicySectionDto> Sections);

/// <summary>
/// The Admin editor's save contract. <c>Id</c> is null for a new policy. Saving content that
/// differs from what is stored bumps <c>Version</c> server-side — the editor never sets the
/// version itself, so it cannot accidentally re-use one and silently keep everyone "acknowledged".
/// </summary>
public sealed class SavePolicyRequest
{
    public string? Id { get; set; }

    [Required, StringLength(40), RegularExpression("^[A-Z0-9_-]+$")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(8)]
    public string Icon { get; set; } = "📄";

    [Required, StringLength(400)]
    public string Excerpt { get; set; } = string.Empty;

    public List<PolicySectionDto> Sections { get; set; } = [];

    public bool IsPublished { get; set; }
}
