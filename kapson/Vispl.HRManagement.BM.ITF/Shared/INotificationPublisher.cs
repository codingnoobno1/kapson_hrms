namespace Vispl.HRManagement.BM.ITF.Shared;

public sealed record NotificationRequest(
    string TenantKey,
    string Category,
    string Title,
    string Body,
    string? ActionRoute = null,
    string Priority = "Normal",
    string? SubjectType = null,
    string? SubjectId = null);

/// <summary>
/// How a module raises a notification without knowing who reads it or how it is stored.
/// Recruitment says "an application arrived"; fan-out and delivery are not its concern.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>Delivers to named recipients.</summary>
    Task PublishAsync(
        NotificationRequest request, IReadOnlyList<string> recipientIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers to everyone in the tenant holding a permission, which is how "tell the
    /// recruiters" is expressed without a module hard-coding a list of people.
    /// </summary>
    Task PublishToPermissionAsync(
        NotificationRequest request, string permission, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves who holds a permission. Implemented by the identity module, so the business layer
/// can address an audience without reaching into the user store.
/// </summary>
public interface IPermissionAudience
{
    Task<IReadOnlyList<string>> GetAccountIdsWithPermissionAsync(
        string tenantKey, string permission, CancellationToken cancellationToken = default);
}
