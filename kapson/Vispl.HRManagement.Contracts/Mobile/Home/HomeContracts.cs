using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Home;

public sealed record KpiDto(
    string Key,
    string Title,
    string Value,
    string? Unit,
    decimal? Trend,
    string Tone,
    string? TargetRoute,
    /// <summary>
    /// Set when the number cannot be computed because the module behind it does not exist yet.
    /// The tile is still returned so the screen keeps its shape, but it must render as
    /// unavailable rather than showing a value nobody stands behind.
    /// </summary>
    string? UnavailableReason = null);

public sealed record QuickActionDto(
    string Code,
    string Label,
    string Icon,
    string Route,
    string? RequiredPermission,
    bool RequiresOnline);

public sealed record HrHomeDto(
    IReadOnlyList<KpiDto> Kpis,
    IReadOnlyList<string> Alerts,
    IReadOnlyList<QuickActionDto> QuickActions);

public sealed record EmployeeHomeDto(
    StatusDto AttendanceToday,
    IReadOnlyList<KpiDto> LeaveBalances,
    IReadOnlyList<QuickActionDto> QuickActions);

public sealed record ManagerHomeDto(
    IReadOnlyList<KpiDto> TeamKpis,
    IReadOnlyList<QuickActionDto> QuickActions);

public sealed record NotificationListItemDto(
    string Id,
    string Category,
    string Title,
    string Preview,
    string Priority,
    bool IsRead,
    DateTimeOffset OccurredUtc,
    string? ActionRoute);

public sealed record NotificationDetailDto(
    string Id,
    string Category,
    string Title,
    string Body,
    DateTimeOffset OccurredUtc,
    bool IsRead,
    IReadOnlyList<ActionAvailabilityDto> Actions);

public sealed record SearchResultItemDto(
    string Type,
    string Id,
    string Title,
    string? Subtitle,
    string Route,
    string? Icon,
    StatusDto? Status);

public sealed record GlobalSearchResultDto(
    IReadOnlyDictionary<string, IReadOnlyList<SearchResultItemDto>> Groups,
    IReadOnlyDictionary<string, int> Facets);

public sealed record CalendarEventDto(
    string Id,
    string Type,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsAllDay,
    string? Route);

public sealed record TaskListItemDto(
    string Id,
    string Type,
    string Title,
    DateTimeOffset? DueUtc,
    string Priority,
    string State,
    string Route);
