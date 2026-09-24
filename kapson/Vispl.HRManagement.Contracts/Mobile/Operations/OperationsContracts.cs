using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Operations;

public sealed record ApprovalListItemDto(
    string Id,
    string Type,
    string Subject,
    PersonReferenceDto Requester,
    DateTimeOffset SubmittedUtc,
    DateTimeOffset? DueUtc,
    string Priority,
    string Summary);

public sealed record ApprovalDetailDto(
    ApprovalListItemDto Summary,
    IReadOnlyDictionary<string, string?> BeforeValues,
    IReadOnlyDictionary<string, string?> AfterValues,
    IReadOnlyList<AttachmentMetadataDto> Evidence,
    IReadOnlyList<WorkflowStepDto> Workflow,
    IReadOnlyList<ActionAvailabilityDto> Actions,
    string? RowVersion);

public sealed class ApprovalDecisionRequest
{
    [Required, RegularExpression("^(approve|reject|return)$")] public string Decision { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Comment { get; set; } = string.Empty;
    [Required, StringLength(200)] public string RowVersion { get; set; } = string.Empty;
}

public sealed record HrServiceDefinitionDto(
    string Code,
    string Name,
    string Category,
    string Eligibility,
    string? ServiceLevel,
    string Icon,
    string FormSchemaVersion);

public sealed record DynamicFieldDto(
    string Key,
    string Type,
    string Label,
    string? HelpText,
    bool IsRequired,
    IReadOnlyList<MasterReferenceDto> Options,
    string? ValidationPattern);

public sealed record DynamicFormSchemaDto(
    string ServiceCode,
    string Version,
    IReadOnlyList<DynamicFieldDto> Fields,
    int MaximumAttachments);

public sealed class CreateHrRequest
{
    [Required, StringLength(80)] public string ServiceCode { get; set; } = string.Empty;
    [Required, StringLength(80)] public string SchemaVersion { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Values { get; set; } = new Dictionary<string, object?>();
    public IReadOnlyList<string> AttachmentIds { get; set; } = [];
}

public sealed record HrRequestDetailDto(
    string Id,
    HrServiceDefinitionDto Service,
    IReadOnlyDictionary<string, object?> Values,
    StatusDto Status,
    IReadOnlyList<WorkflowStepDto> Workflow,
    IReadOnlyList<ActionAvailabilityDto> Actions,
    string? RowVersion);

public sealed record ReportDefinitionDto(
    string Code,
    string Name,
    string Category,
    string RequiredPermission,
    IReadOnlyList<string> SupportedFormats);

public sealed record ReportColumnDto(string Key, string Label, string DataType, bool IsSensitive);

public sealed record ReportResultDto(
    string ReportCode,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    string? NextCursor,
    DateTimeOffset GeneratedUtc);

public sealed record ExportJobDto(
    string Id,
    string Type,
    string Format,
    string State,
    decimal Progress,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ExpiresUtc,
    string? ErrorCode);

public sealed record OwnProfileDto(
    PersonReferenceDto User,
    string? Email,
    string? Phone,
    string Locale,
    string TimeZoneId,
    string ThemeMode,
    IReadOnlyList<FieldAccessDto> FieldAccess,
    string? RowVersion);

public sealed record HelpArticleSummaryDto(
    string Id,
    string Title,
    string Category,
    DateTimeOffset UpdatedUtc);

public sealed record SyncManifestDto(
    string DeltaToken,
    IReadOnlyList<string> ApprovedEntitySets,
    DateTimeOffset ServerUtc,
    long ConfigurationVersion);
