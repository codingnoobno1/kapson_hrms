using Vispl.HRManagement.Contracts.Recruitment;

namespace Vispl.HRManagement.BM.ITF.Recruitment;

/// <summary>Raised when a submission breaks a rule the caller can correct.</summary>
public sealed class ApplicationRejectedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed record DocumentStream(Stream Content, string ContentType, string FileName);

public interface IApplicationService
{
    Task<PublicOpeningDto?> GetPublicOpeningAsync(
        string tenantKey, string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Public, unauthenticated. Validates the opening, the payload, the consent flag and every
    /// uploaded file before anything is written or stored.
    /// </summary>
    Task<SubmitApplicationResultDto> SubmitAsync(
        string tenantKey,
        string slug,
        SubmitApplicationRequest request,
        IReadOnlyList<UploadedFile> files,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApplicationListItemDto>> GetSummariesAsync(
        string tenantKey, string? stage, string? query, CancellationToken cancellationToken = default);

    Task<ApplicationDetailDto?> GetDetailAsync(
        string tenantKey, long applicationId, CancellationToken cancellationToken = default);

    Task ChangeStageAsync(
        string tenantKey,
        long applicationId,
        ChangeStageRequest request,
        string? actorId,
        bool canManage,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a document after its scan state has been checked. Never serves a static path.</summary>
    Task<DocumentStream?> OpenDocumentAsync(
        string tenantKey, long documentId, CancellationToken cancellationToken = default);
}
