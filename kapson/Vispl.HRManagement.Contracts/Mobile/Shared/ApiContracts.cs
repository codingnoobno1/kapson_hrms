using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Mobile.Shared;

public sealed record ApiEnvelope<T>(T Data, ApiMetaDto Meta);

public sealed record ApiMetaDto(
    string CorrelationId,
    DateTimeOffset ServerUtc,
    long ConfigurationVersion,
    IReadOnlyList<ApiWarningDto> Warnings);

public sealed record ApiWarningDto(string Code, string Message, string? Field = null);

public sealed record CursorPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasMore,
    int PageSize,
    long? ApproximateTotal = null);

public class MobileListQuery
{
    [StringLength(200)]
    public string? Query { get; set; }

    [StringLength(500)]
    public string? Cursor { get; set; }

    [Range(1, 100)]
    public int PageSize { get; set; } = 25;
}

public sealed record DateRangeDto(DateOnly From, DateOnly To, string TimeZoneId);

public sealed record MoneyDto(decimal Amount, string CurrencyCode, bool IsMasked = false);

public sealed record PersonReferenceDto(
    string Id,
    string DisplayName,
    string? AvatarUrl = null,
    string? EmployeeNumber = null);

public sealed record MasterReferenceDto(
    string Category,
    string Code,
    string Name,
    bool IsActive = true);

public sealed record StatusDto(
    string Code,
    string Name,
    string Tone,
    DateTimeOffset? ChangedUtc = null);

public sealed record FieldAccessDto(
    string Field,
    bool CanView,
    bool CanEdit,
    bool IsMasked,
    string? Reason = null);

public sealed record ActionAvailabilityDto(
    string Action,
    bool IsAllowed,
    string? Reason = null,
    bool RequiresOnline = true,
    bool RequiresReason = false);

public sealed record AuditEntryDto(
    string Id,
    string Action,
    PersonReferenceDto? Actor,
    DateTimeOffset OccurredUtc,
    string Summary);

public sealed record WorkflowStepDto(
    string Code,
    string Name,
    string State,
    PersonReferenceDto? Actor,
    DateTimeOffset? DueUtc,
    DateTimeOffset? CompletedUtc);

public sealed record AttachmentMetadataDto(
    string Id,
    string FileName,
    string ContentType,
    long Size,
    string State,
    DateTimeOffset? ExpiresUtc = null);

public sealed record UploadIntentDto(
    string UploadId,
    string UploadUrl,
    IReadOnlyDictionary<string, string> RequiredHeaders,
    long MaximumBytes,
    IReadOnlyList<string> AllowedContentTypes,
    DateTimeOffset ExpiresUtc);

public sealed record ProtectedFileTicketDto(
    string Ticket,
    string DownloadUrl,
    DateTimeOffset ExpiresUtc,
    AttachmentMetadataDto File);

public sealed record MutationResultDto(
    string ResourceId,
    string State,
    string? RowVersion,
    DateTimeOffset OccurredUtc,
    string? Message = null);

public sealed record MobileFeatureStatusDto(
    string Module,
    string Status,
    string Code,
    string Message,
    IReadOnlyList<string> RequiredComponents);
