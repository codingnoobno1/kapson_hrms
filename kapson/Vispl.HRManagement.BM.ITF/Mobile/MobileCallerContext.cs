namespace Vispl.HRManagement.BM.ITF.Mobile;

/// <summary>
/// The resolved caller for one mobile API operation.
/// Controllers build this from the authenticated principal; business services must never
/// re-read claims or trust an identifier supplied in a request body.
/// </summary>
public sealed record MobileCallerContext(
    string TenantKey,
    string UserId,
    string? EmployeeId,
    IReadOnlyCollection<string> Permissions,
    string CorrelationId,
    string? IdempotencyKey = null,
    string? RowVersion = null)
{
    public bool Has(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the caller is acting on their own record.</summary>
    public bool IsSelf(string? employeeId) =>
        employeeId is not null &&
        EmployeeId is not null &&
        string.Equals(employeeId, EmployeeId, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Raised by a business service whose module is registered but not yet implemented.
/// The API layer maps this to a 501 problem response carrying the missing components,
/// so an unimplemented module fails loudly and uniformly instead of returning empty data.
/// </summary>
public sealed class MobileFeatureNotReadyException : Exception
{
    public MobileFeatureNotReadyException(string module, params string[] requiredComponents)
        : base($"The {module} mobile feature is not ready.")
    {
        Module = module;
        RequiredComponents = requiredComponents;
    }

    public string Module { get; }

    public IReadOnlyList<string> RequiredComponents { get; }
}

/// <summary>
/// The caller is authenticated but lacks the permission this operation needs. Distinct from a
/// missing token, so the API can answer 403 rather than challenge for credentials again.
/// </summary>
public sealed class MobileAccessDeniedException(string permission)
    : Exception($"This operation requires the {permission} permission.")
{
    public string Permission { get; } = permission;
}

/// <summary>A request the caller can correct: a rule was broken, not a system failure.</summary>
public sealed class MobileValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// The resource does not exist, or does not exist for this tenant. Both cases answer the same
/// way, so the API never reveals that a record exists in someone else's tenant.
/// </summary>
public sealed class MobileNotFoundException(string message) : Exception(message);

