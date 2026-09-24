using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Operations;

// Approvals -------------------------------------------------------------------

public sealed class ApprovalInboxQuery : MobileListQuery
{
    [StringLength(40)] public string? WorkflowType { get; set; }
    [StringLength(20)] public string? Priority { get; set; }
    public bool OverdueOnly { get; set; }
    public bool IncludeDelegated { get; set; } = true;
}

public sealed record ApprovalDelegationDto(
    string Id,
    PersonReferenceDto Delegate,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<string> WorkflowTypes,
    bool IsActive,
    string? RowVersion);

public sealed class SaveApprovalDelegationRequest
{
    [Required, StringLength(64)] public string DelegateEmployeeId { get; set; } = string.Empty;
    [Required] public DateOnly From { get; set; }
    [Required] public DateOnly To { get; set; }
    public IReadOnlyList<string> WorkflowTypes { get; set; } = [];
    [StringLength(1000)] public string? Reason { get; set; }
}

// HR requests -----------------------------------------------------------------

public sealed class RequestCatalogQuery : MobileListQuery
{
    [StringLength(40)] public string? CategoryCode { get; set; }
    public bool FavouritesOnly { get; set; }
}

public sealed class HrRequestListQuery : MobileListQuery
{
    [StringLength(40)] public string? ServiceCode { get; set; }
    [StringLength(40)] public string? State { get; set; }
    public bool RaisedByMeOnly { get; set; }
}

public sealed class AddRequestCommentRequest
{
    [Required, StringLength(4000)] public string Comment { get; set; } = string.Empty;
    public IReadOnlyList<string> AttachmentUploadIds { get; set; } = [];
}

public sealed class CancelHrRequest
{
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class FulfilHrRequest
{
    [StringLength(4000)] public string? Resolution { get; set; }
    public IReadOnlyList<string> AttachmentUploadIds { get; set; } = [];
    [StringLength(64)] public string? RowVersion { get; set; }
}

// Reports and exports -----------------------------------------------------------

public sealed class ReportCatalogQuery : MobileListQuery
{
    [StringLength(40)] public string? CategoryCode { get; set; }
    public bool FavouritesOnly { get; set; }
}

public sealed record ReportFilterFieldDto(
    string Code,
    string Name,
    string DataType,
    bool IsRequired,
    string? DefaultValue,
    IReadOnlyList<MasterReferenceDto> Options);

public sealed record ReportFilterSchemaDto(
    string ReportCode,
    long SchemaVersion,
    IReadOnlyList<ReportFilterFieldDto> Fields);

public sealed class RunReportRequest
{
    [Required, StringLength(40)] public string ReportCode { get; set; } = string.Empty;
    public long SchemaVersion { get; set; }
    public IReadOnlyDictionary<string, string?> Filters { get; set; } =
        new Dictionary<string, string?>();
    [Range(1, 200)] public int PageSize { get; set; } = 50;
}

public sealed class ReportPageQuery : MobileListQuery
{
    [Required, StringLength(120)] public string RunId { get; set; } = string.Empty;
    [StringLength(60)] public string? SortColumn { get; set; }
    public bool SortDescending { get; set; }
}

public sealed class ReportDrilldownRequest
{
    [Required, StringLength(120)] public string RunId { get; set; } = string.Empty;
    [Required, StringLength(60)] public string ColumnCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Value { get; set; } = string.Empty;
}

public sealed record ReportDrilldownDto(
    string ReportCode,
    string ColumnCode,
    string Value,
    ReportResultDto Result);

public sealed class CreateExportRequest
{
    [Required, StringLength(40)] public string SourceCode { get; set; } = string.Empty;
    [Required, StringLength(10)] public string Format { get; set; } = "xlsx";
    public IReadOnlyDictionary<string, string?> Parameters { get; set; } =
        new Dictionary<string, string?>();
}

public sealed class ExportListQuery : MobileListQuery
{
    [StringLength(40)] public string? State { get; set; }
}

// Profile ------------------------------------------------------------------------

public sealed class UpdateOwnProfileRequest
{
    [StringLength(120)] public string? PreferredName { get; set; }
    [StringLength(32)] public string? Phone { get; set; }
    [EmailAddress, StringLength(320)] public string? PersonalEmail { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed record AppPreferencesDto(
    string LocaleCode,
    string ThemeMode,
    string TimeZoneId,
    string DateFormat,
    bool ReduceMotion,
    bool BiometricUnlockEnabled);

public sealed class UpdateAppPreferencesRequest
{
    [StringLength(20)] public string? LocaleCode { get; set; }
    [StringLength(20)] public string? ThemeMode { get; set; }
    [StringLength(64)] public string? TimeZoneId { get; set; }
    [StringLength(40)] public string? DateFormat { get; set; }
    public bool? ReduceMotion { get; set; }
    public bool? BiometricUnlockEnabled { get; set; }
}

public sealed record NotificationChannelPreferenceDto(
    string EventCode,
    string EventName,
    bool Push,
    bool Email,
    bool InApp,
    bool IsLocked);

public sealed record NotificationPreferencesDto(
    IReadOnlyList<NotificationChannelPreferenceDto> Events,
    TimeOnly? QuietHoursFrom,
    TimeOnly? QuietHoursTo,
    string TimeZoneId);

public sealed class UpdateNotificationPreferencesRequest
{
    public IReadOnlyList<NotificationChannelPreferenceDto> Events { get; set; } = [];
    public TimeOnly? QuietHoursFrom { get; set; }
    public TimeOnly? QuietHoursTo { get; set; }
}

public sealed class AvatarUploadIntentRequest
{
    [Required, StringLength(120)] public string ContentType { get; set; } = string.Empty;
    [Range(1, 10_485_760)] public long SizeBytes { get; set; }
}

// Support -------------------------------------------------------------------------

public sealed class HelpSearchQuery : MobileListQuery
{
    [StringLength(40)] public string? CategoryCode { get; set; }
}

public sealed record HelpArticleDto(
    string Code,
    string Title,
    string CategoryCode,
    string BodyMarkdown,
    DateTimeOffset UpdatedUtc,
    IReadOnlyList<string> RelatedCodes);

public sealed class CreateSupportTicketRequest
{
    [Required, StringLength(40)] public string CategoryCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> AttachmentUploadIds { get; set; } = [];
    public bool IncludeDiagnostics { get; set; }
}

public sealed record SupportTicketResultDto(
    string TicketId,
    string Reference,
    StatusDto Status,
    DateTimeOffset CreatedUtc);

public sealed class SubmitDiagnosticsRequest
{
    [Required] public bool ConsentGiven { get; set; }
    [StringLength(120)] public string? TicketReference { get; set; }
    [StringLength(4000)] public string? Note { get; set; }
}

// Sync and files ---------------------------------------------------------------------

public sealed class SyncChangesQuery
{
    [StringLength(500)] public string? DeltaToken { get; set; }
    public IReadOnlyList<string> Entities { get; set; } = [];
    [Range(1, 500)] public int PageSize { get; set; } = 100;
}

public sealed record SyncChangeDto(
    string Entity,
    string Id,
    string ChangeType,
    DateTimeOffset ChangedUtc);

public sealed record SyncChangePageDto(
    IReadOnlyList<SyncChangeDto> Changes,
    string? NextDeltaToken,
    bool HasMore);

public sealed class SyncMutationStatusQuery
{
    public IReadOnlyList<string> IdempotencyKeys { get; set; } = [];
}

public sealed record SyncMutationStatusDto(
    string IdempotencyKey,
    string State,
    string? ResourceId,
    string? ErrorCode,
    DateTimeOffset? CompletedUtc);

public sealed class FileTicketRequest
{
    [Required, StringLength(64)] public string FileId { get; set; } = string.Empty;
    [StringLength(40)] public string? Purpose { get; set; }
}

public sealed record FileMetadataDto(
    string Id,
    string FileName,
    string ContentType,
    long Size,
    bool IsProtected,
    DateTimeOffset? ExpiresUtc);
