using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Home;

public sealed class NotificationListQuery : MobileListQuery
{
    [StringLength(40)] public string? Tab { get; set; }
    public bool UnreadOnly { get; set; }
}

public sealed class MarkNotificationsReadRequest
{
    public IReadOnlyList<string> NotificationIds { get; set; } = [];
    public bool All { get; set; }
}

public sealed class GlobalSearchQuery : MobileListQuery
{
    public IReadOnlyList<string> Scopes { get; set; } = [];
}

public sealed class CalendarRangeQuery
{
    [Required] public DateOnly From { get; set; }
    [Required] public DateOnly To { get; set; }
    [StringLength(64)] public string? TimeZoneId { get; set; }
    public IReadOnlyList<string> EventTypes { get; set; } = [];
}

public sealed class TaskListQuery : MobileListQuery
{
    [StringLength(40)] public string? State { get; set; }
    public bool OverdueOnly { get; set; }
}

public sealed class CompleteTaskRequest
{
    [StringLength(2000)] public string? Comment { get; set; }
    public IReadOnlyList<string> EvidenceUploadIds { get; set; } = [];
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed record TaskDetailDto(
    string Id,
    string Title,
    string? Description,
    string WorkflowType,
    StatusDto Status,
    DateTimeOffset? DueUtc,
    PersonReferenceDto? Owner,
    string? DeepLink,
    IReadOnlyList<AttachmentMetadataDto> Attachments,
    IReadOnlyList<ActionAvailabilityDto> Actions,
    string? RowVersion);
