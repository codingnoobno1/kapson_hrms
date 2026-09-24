using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.MobileApp.Services.Clients;

namespace Vispl.HRManagement.MobileApp.Services;

/// <summary>
/// The single HTTP surface of the app. Every feature client goes through here so that
/// authentication, correlation and problem-details handling stay in one place.
/// </summary>
public sealed class HrApiClient(
    HttpClient http,
    ITokenStore tokenStore,
    AppSession session,
    MobileAppOptions options,
    ILogger<HrApiClient> logger)
{
    private const string AuthBasePath = "api/mobile/v1/auth";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The server rotates the refresh token on every use, so two concurrent refreshes would
    /// spend each other's token and sign the user out. Screens load in parallel and expire
    /// together, so that race is the normal case rather than an edge one.
    /// </summary>
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    public string BaseAddress => session.GetBaseAddress(options);

    public Task<ApiResult<T>> GetAsync<T>(string path, string? etag = null, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Get, path, null, etag, cancellationToken);

    public Task<ApiResult<T>> PostAsync<T>(string path, object? body, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Post, path, body, null, cancellationToken);

    /// <summary>
    /// For endpoints that return their payload directly rather than inside an ApiEnvelope.
    /// The auth endpoints do, because a token response is not tenant-scoped data.
    /// </summary>
    public Task<ApiResult<T>> PostRawAsync<T>(string path, object? body, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Post, path, body, null, cancellationToken, unwrapEnvelope: false);

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        string? etag,
        CancellationToken cancellationToken,
        bool unwrapEnvelope = true,
        bool allowRefresh = true)
    {
        if (!Uri.TryCreate(new Uri(BaseAddress), path, out var requestUri))
        {
            return ApiResult<T>.Failure(ApiOutcome.Failed);
        }

        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", Guid.NewGuid().ToString("N"));
        request.Headers.TryAddWithoutValidation("X-App-Version", options.AppVersion);
        request.Headers.TryAddWithoutValidation("X-App-Platform", DeviceInfo.Platform.ToString().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        var token = await tokenStore.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Request to {Path} failed to reach the server.", path);
            return ApiResult<T>.Failure(ApiOutcome.Offline);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return ApiResult<T>.Failure(ApiOutcome.NotModified);
            }

            if (response.IsSuccessStatusCode)
            {
                if (!unwrapEnvelope)
                {
                    // A 204 carries no body, so default(T) is the correct result rather than a failure.
                    if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        return ApiResult<T>.Success(default!);
                    }

                    var raw = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
                    return raw is null
                        ? ApiResult<T>.Failure(ApiOutcome.Failed)
                        : ApiResult<T>.Success(raw);
                }

                var envelope = await response.Content
                    .ReadFromJsonAsync<ApiEnvelope<T>>(Json, cancellationToken);

                return envelope is null
                    ? ApiResult<T>.Failure(ApiOutcome.Failed)
                    : ApiResult<T>.Success(
                        envelope.Data,
                        envelope.Meta.ConfigurationVersion,
                        response.Headers.ETag?.ToString());
            }

            // An expired access token is the expected state on every relaunch, so it is worth
            // one silent renewal before the user is sent back to the sign-in screen.
            var expired = response.StatusCode == HttpStatusCode.Unauthorized &&
                          allowRefresh &&
                          !path.StartsWith(AuthBasePath, StringComparison.OrdinalIgnoreCase);

            if (!expired)
            {
                var problem = await ReadProblemAsync(response, cancellationToken);
                var outcome = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => ApiOutcome.Unauthenticated,
                    HttpStatusCode.Forbidden => ApiOutcome.Forbidden,
                    HttpStatusCode.NotFound => ApiOutcome.NotFound,
                    HttpStatusCode.NotImplemented => ApiOutcome.FeatureNotReady,
                    HttpStatusCode.Locked => ApiOutcome.Failed,
                    _ => ApiOutcome.Failed
                };

                return ApiResult<T>.Failure(outcome, problem);
            }
        }

        if (!await TryRefreshAsync(token, cancellationToken))
        {
            return ApiResult<T>.Failure(ApiOutcome.Unauthenticated);
        }

        // allowRefresh is false on the retry, so a server that keeps answering 401 cannot
        // drive an endless renew-and-retry loop.
        return await SendAsync<T>(method, path, body, etag, cancellationToken, unwrapEnvelope, allowRefresh: false);
    }

    /// <summary>
    /// Exchanges the stored refresh token for a new pair. Returns false when the refresh token
    /// is missing, spent or rejected — the cases where only a fresh sign-in will do.
    /// </summary>
    private async Task<bool> TryRefreshAsync(string? staleAccessToken, CancellationToken cancellationToken)
    {
        await RefreshGate.WaitAsync(cancellationToken);
        try
        {
            // A caller that queued behind the gate may find the work already done.
            var current = await tokenStore.GetAccessTokenAsync();
            if (!string.IsNullOrWhiteSpace(current) && current != staleAccessToken)
            {
                return true;
            }

            var refreshToken = await tokenStore.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return false;
            }

            if (!Uri.TryCreate(new Uri(BaseAddress), $"{AuthBasePath}/refresh", out var requestUri))
            {
                return false;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(
                    new { refreshToken, deviceId = session.DeviceId }, options: Json)
            };

            using var response = await http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // The token was revoked or has already been spent. Clearing the session now
                // means the app shows the sign-in screen rather than looping on 401s.
                logger.LogInformation("Refresh rejected with {Status}; signing out.", response.StatusCode);
                await session.SignOutAsync();
                return false;
            }

            var grant = await response.Content.ReadFromJsonAsync<TokenResponseDto>(Json, cancellationToken);
            if (grant is null)
            {
                return false;
            }

            await session.SignInAsync(
                grant.AccessToken, grant.RefreshToken, grant.DisplayName, grant.Portal, grant.Permissions);

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Offline. The session is left intact so it can renew once the server is reachable.
            logger.LogWarning(exception, "Could not reach the server to refresh the session.");
            return false;
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private static async Task<ProblemInfo?> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            var root = document.RootElement;
            var components = root.TryGetProperty("requiredComponents", out var list) &&
                             list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray()
                : [];

            return new ProblemInfo(
                ReadString(root, "title") ?? response.ReasonPhrase ?? "Request failed",
                ReadString(root, "detail"),
                ReadString(root, "code"),
                ReadString(root, "module"),
                components,
                (int)response.StatusCode);
        }
        catch (JsonException)
        {
            return new ProblemInfo(
                response.ReasonPhrase ?? "Request failed",
                null, null, null, [], (int)response.StatusCode);
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
