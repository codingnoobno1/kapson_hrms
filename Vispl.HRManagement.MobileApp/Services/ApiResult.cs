namespace Vispl.HRManagement.MobileApp.Services;

public enum ApiOutcome
{
    Success,
    NotModified,
    Unauthenticated,
    Forbidden,
    NotFound,
    FeatureNotReady,
    Offline,
    Failed
}

public sealed record ProblemInfo(
    string Title,
    string? Detail,
    string? Code,
    string? Module,
    IReadOnlyList<string> RequiredComponents,
    int StatusCode);

public sealed record ApiResult<T>(
    ApiOutcome Outcome,
    T? Data,
    ProblemInfo? Problem,
    long ConfigurationVersion = 0,
    string? ETag = null)
{
    public bool IsSuccess => Outcome == ApiOutcome.Success;

    public string Message => Problem?.Detail
        ?? Problem?.Title
        ?? Outcome switch
        {
            ApiOutcome.Unauthenticated => "Sign in to continue.",
            ApiOutcome.Forbidden => "You do not have permission for this area.",
            ApiOutcome.NotFound => "That item is no longer available.",
            ApiOutcome.Offline => "The server could not be reached.",
            _ => "The request could not be completed."
        };

    public static ApiResult<T> Success(T data, long version = 0, string? etag = null) =>
        new(ApiOutcome.Success, data, null, version, etag);

    public static ApiResult<T> Failure(ApiOutcome outcome, ProblemInfo? problem = null) =>
        new(outcome, default, problem);
}
