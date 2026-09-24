namespace Vispl.HRManagement.Mobile.Client.Http;

/// <summary>
/// A failure the app has to render differently, rather than a stack trace.
///
/// Every one of these corresponds to a state a screen must draw: not-ready, denied, conflict,
/// validation, offline. Collapsing them into one exception is what produces an app that shows
/// "something went wrong" when the truth was "payroll has not been built yet".
/// </summary>
public abstract class MobileApiException(string message) : Exception(message)
{
    public string? CorrelationId { get; init; }
}

/// <summary>
/// 501. The route and its contract exist; the module behind them does not.
///
/// This is a designed state, not a fault: roughly a quarter of the employee app's surface
/// answers this during Phase 1-3, and the screen renders a panel naming the feature.
/// </summary>
public sealed class MobileFeatureNotReadyException(string module, IReadOnlyList<string> requiredComponents)
    : MobileApiException($"The {module} feature is not ready.")
{
    public string Module { get; } = module;

    public IReadOnlyList<string> RequiredComponents { get; } = requiredComponents;
}

/// <summary>403, or a missing tenant claim.</summary>
public sealed class MobileAccessDeniedException(string message, string? permission)
    : MobileApiException(message)
{
    public string? Permission { get; } = permission;
}

/// <summary>400. Carries field-level detail so a form can mark its own inputs.</summary>
public sealed class MobileValidationFailedException(
    string message, string? code, IReadOnlyDictionary<string, string[]> fields)
    : MobileApiException(message)
{
    public string? Code { get; } = code;

    public IReadOnlyDictionary<string, string[]> Fields { get; } = fields;
}

/// <summary>409. The row moved since it was read; reload, show what changed, ask again.</summary>
public sealed class MobileConflictException(string message) : MobileApiException(message);

/// <summary>404, or a record the caller may not see. Renders as empty, never as "deleted".</summary>
public sealed class MobileNotFoundException(string message) : MobileApiException(message);

/// <summary>The request never reached the server.</summary>
public sealed class MobileOfflineException(string message, Exception? inner = null)
    : MobileApiException(message)
{
    public Exception? Cause { get; } = inner;
}

/// <summary>401 that survived a refresh, or a refresh that itself failed.</summary>
public sealed class MobileSignedOutException(string message) : MobileApiException(message);

/// <summary>Anything else the server returned.</summary>
public sealed class MobileServerException(string message, int statusCode) : MobileApiException(message)
{
    public int StatusCode { get; } = statusCode;
}
