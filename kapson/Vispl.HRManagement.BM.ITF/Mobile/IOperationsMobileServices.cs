using Vispl.HRManagement.Contracts.Mobile.Operations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.BM.ITF.Mobile;

/// <summary>
/// The single decision surface for every workflow. Feature controllers expose reads and
/// submissions; they must not implement their own approve/reject contract.
/// </summary>
public interface IApprovalService
{
    Task<CursorPage<ApprovalListItemDto>> GetInboxAsync(
        MobileCallerContext caller,
        ApprovalInboxQuery query,
        CancellationToken ct);

    Task<ApprovalDetailDto> GetAsync(
        MobileCallerContext caller,
        string approvalId,
        CancellationToken ct);

    Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller,
        string approvalId,
        ApprovalDecisionRequest request,
        CancellationToken ct);

    Task<IReadOnlyList<ApprovalDelegationDto>> ListDelegationsAsync(
        MobileCallerContext caller,
        CancellationToken ct);

    Task<MutationResultDto> SaveDelegationAsync(
        MobileCallerContext caller,
        SaveApprovalDelegationRequest request,
        CancellationToken ct);

    Task RevokeDelegationAsync(
        MobileCallerContext caller,
        string delegationId,
        CancellationToken ct);
}

public interface IHrRequestService
{
    Task<CursorPage<HrServiceDefinitionDto>> GetCatalogAsync(
        MobileCallerContext caller,
        RequestCatalogQuery query,
        CancellationToken ct);

    Task<DynamicFormSchemaDto> GetSchemaAsync(
        MobileCallerContext caller,
        string serviceCode,
        CancellationToken ct);

    Task<MutationResultDto> CreateAsync(
        MobileCallerContext caller,
        CreateHrRequest request,
        CancellationToken ct);

    Task<CursorPage<HrRequestDetailDto>> ListAsync(
        MobileCallerContext caller,
        HrRequestListQuery query,
        CancellationToken ct);

    Task<HrRequestDetailDto> GetAsync(
        MobileCallerContext caller,
        string requestId,
        CancellationToken ct);

    Task<MutationResultDto> CommentAsync(
        MobileCallerContext caller,
        string requestId,
        AddRequestCommentRequest request,
        CancellationToken ct);

    Task<MutationResultDto> CancelAsync(
        MobileCallerContext caller,
        string requestId,
        CancelHrRequest request,
        CancellationToken ct);

    Task<MutationResultDto> FulfilAsync(
        MobileCallerContext caller,
        string requestId,
        FulfilHrRequest request,
        CancellationToken ct);
}

public interface IReportService
{
    Task<CursorPage<ReportDefinitionDto>> GetCatalogAsync(
        MobileCallerContext caller,
        ReportCatalogQuery query,
        CancellationToken ct);

    Task<ReportFilterSchemaDto> GetSchemaAsync(
        MobileCallerContext caller,
        string reportCode,
        CancellationToken ct);

    Task<ReportResultDto> RunAsync(
        MobileCallerContext caller,
        RunReportRequest request,
        CancellationToken ct);

    Task<ReportResultDto> GetPageAsync(
        MobileCallerContext caller,
        string reportCode,
        ReportPageQuery query,
        CancellationToken ct);

    Task<ReportDrilldownDto> DrillDownAsync(
        MobileCallerContext caller,
        string reportCode,
        ReportDrilldownRequest request,
        CancellationToken ct);
}

public interface IExportService
{
    Task<ExportJobDto> CreateAsync(
        MobileCallerContext caller,
        CreateExportRequest request,
        CancellationToken ct);

    Task<CursorPage<ExportJobDto>> ListAsync(
        MobileCallerContext caller,
        ExportListQuery query,
        CancellationToken ct);

    Task<ExportJobDto> GetAsync(MobileCallerContext caller, string exportId, CancellationToken ct);

    Task<ProtectedFileTicketDto> GetTicketAsync(
        MobileCallerContext caller,
        string exportId,
        CancellationToken ct);

    Task DeleteAsync(MobileCallerContext caller, string exportId, CancellationToken ct);
}

public interface IMobileProfileService
{
    Task<OwnProfileDto> GetAsync(MobileCallerContext caller, CancellationToken ct);

    Task<MutationResultDto> UpdateAsync(
        MobileCallerContext caller,
        UpdateOwnProfileRequest request,
        CancellationToken ct);

    Task<AppPreferencesDto> GetPreferencesAsync(MobileCallerContext caller, CancellationToken ct);

    Task<AppPreferencesDto> UpdatePreferencesAsync(
        MobileCallerContext caller,
        UpdateAppPreferencesRequest request,
        CancellationToken ct);

    Task<NotificationPreferencesDto> GetNotificationPreferencesAsync(
        MobileCallerContext caller,
        CancellationToken ct);

    Task<NotificationPreferencesDto> UpdateNotificationPreferencesAsync(
        MobileCallerContext caller,
        UpdateNotificationPreferencesRequest request,
        CancellationToken ct);

    Task<UploadIntentDto> CreateAvatarIntentAsync(
        MobileCallerContext caller,
        AvatarUploadIntentRequest request,
        CancellationToken ct);
}

public interface ISupportService
{
    Task<IReadOnlyList<MasterReferenceDto>> ListCategoriesAsync(
        MobileCallerContext caller,
        CancellationToken ct);

    Task<CursorPage<HelpArticleSummaryDto>> SearchArticlesAsync(
        MobileCallerContext caller,
        HelpSearchQuery query,
        CancellationToken ct);

    Task<HelpArticleDto> GetArticleAsync(
        MobileCallerContext caller,
        string articleCode,
        CancellationToken ct);

    Task<SupportTicketResultDto> CreateTicketAsync(
        MobileCallerContext caller,
        CreateSupportTicketRequest request,
        CancellationToken ct);

    Task<MutationResultDto> SubmitDiagnosticsAsync(
        MobileCallerContext caller,
        SubmitDiagnosticsRequest request,
        CancellationToken ct);
}

public interface ISyncService
{
    Task<SyncManifestDto> GetManifestAsync(MobileCallerContext caller, CancellationToken ct);

    Task<SyncChangePageDto> GetChangesAsync(
        MobileCallerContext caller,
        SyncChangesQuery query,
        CancellationToken ct);

    Task<IReadOnlyList<SyncMutationStatusDto>> GetMutationStatusAsync(
        MobileCallerContext caller,
        SyncMutationStatusQuery query,
        CancellationToken ct);
}

public interface IProtectedFileService
{
    Task<ProtectedFileTicketDto> CreateTicketAsync(
        MobileCallerContext caller,
        FileTicketRequest request,
        CancellationToken ct);

    Task<FileMetadataDto> GetMetadataAsync(
        MobileCallerContext caller,
        string fileId,
        CancellationToken ct);

    Task DeleteAsync(MobileCallerContext caller, string fileId, CancellationToken ct);
}
