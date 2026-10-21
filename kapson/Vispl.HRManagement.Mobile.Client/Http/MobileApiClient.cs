using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Mobile.Client.Auth;

namespace Vispl.HRManagement.Mobile.Client.Http;

/// <summary>Options the app head supplies once.</summary>
public sealed class MobileApiOptions
{
    public const string DefaultBaseAddress = "https://untutelary-francisco-overtrustfully.ngrok-free.dev";

    /// <summary>Host root, without the api path. For example https://hr.kapsons.com.</summary>
    public string BaseAddress { get; set; } = DefaultBaseAddress;

    /// <summary>Identifies this installation to the server for device-bound actions.</summary>
    public string DeviceId { get; set; } = string.Empty;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Which account portals this app head accepts.
    ///
    /// The server already refuses a Platform account and gates every route on permissions, so
    /// this is not the security boundary — it is what stops an HR user signing in to the
    /// employee app, or an employee to the HR app, and finding a shell built for someone else.
    /// </summary>
    public IReadOnlyList<string> AllowedPortals { get; set; } = ["Employee", "Candidate"];

    public Uri ResolveUri(string relativePath)
    {
        var raw = string.IsNullOrWhiteSpace(BaseAddress) ? DefaultBaseAddress : BaseAddress.Trim();
        if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            raw = "https://" + raw;
        }

        var root = raw.TrimEnd('/') + "/";
        return new Uri(new Uri(root, UriKind.Absolute), relativePath.TrimStart('/'));
    }
}

/// <summary>
/// Every call the employee app makes to <c>api/mobile/v1</c> goes through here.
///
/// Three things happen in one place so that no screen has to remember them: the
/// <see cref="ApiEnvelope{T}"/> is unwrapped, a ProblemDetails is turned into the typed
/// exception the screen renders, and a 401 is refreshed once before the session is given up.
///
/// The envelope's <c>Meta</c> is not discarded — warnings surface to the caller, and the
/// correlation id is attached to every exception so a support ticket can name the request.
/// </summary>
public sealed class MobileApiClient(
    HttpClient http,
    MobileSession session,
    ITokenStore tokens,
    MobileApiOptions options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim refreshLock = new(1, 1);

    public MobileSession Session => session;

    public MobileApiOptions Options => options;

    /// <summary>Warnings from the most recent call, for a screen that wants to show them.</summary>
    public IReadOnlyList<ApiWarningDto> LastWarnings { get; private set; } = [];

    // ---- Verbs ------------------------------------------------------------

    public Task<T> GetAsync<T>(string path, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Get, path, null, null, ct);

    public Task<T> PostAsync<T>(
        string path, object? body = null, string? idempotencyKey = null, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Post, path, body, idempotencyKey ?? NewIdempotencyKey(), ct);

    public Task<T> PutAsync<T>(
        string path, object? body = null, string? idempotencyKey = null, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Put, path, body, idempotencyKey ?? NewIdempotencyKey(), ct);

    public Task<T> DeleteAsync<T>(string path, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Delete, path, null, NewIdempotencyKey(), ct);

    /// <summary>A mutation whose response body is not needed.</summary>
    public async Task SendNoContentAsync(
        HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        using var response = await SendRawAsync(method, path, body, NewIdempotencyKey(), null, ct);
        await EnsureSuccessAsync(response, ct);
    }

    /// <summary>
    /// A write that carries the row version it was read at, so a stale edit conflicts instead
    /// of silently overwriting whatever arrived in between.
    /// </summary>
    public async Task<MutationResultDto> MutateAsync(
        HttpMethod method,
        string path,
        object? body = null,
        string? rowVersion = null,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        using var response = await SendRawAsync(
            method, path, body, idempotencyKey ?? NewIdempotencyKey(), rowVersion, ct);

        return await ReadEnvelopeAsync<MutationResultDto>(response, ct);
    }

    // ---- Pipeline ---------------------------------------------------------

    private async Task<T> SendAsync<T>(
        HttpMethod method, string path, object? body, string? idempotencyKey, CancellationToken ct)
    {
        using var response = await SendRawAsync(method, path, body, idempotencyKey, null, ct);
        return await ReadEnvelopeAsync<T>(response, ct);
    }

    private async Task<HttpResponseMessage> SendRawAsync(
        HttpMethod method,
        string path,
        object? body,
        string? idempotencyKey,
        string? rowVersion,
        CancellationToken ct)
    {
        var response = await SendOnceAsync(method, path, body, idempotencyKey, rowVersion, ct);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // One refresh, then the session is over. Retrying a second time would loop against a
        // server that has already said no twice.
        response.Dispose();

        if (!await TryRefreshAsync(ct))
        {
            session.Clear();
            throw new MobileSignedOutException("The session has ended. Sign in again.");
        }

        var retried = await SendOnceAsync(method, path, body, idempotencyKey, rowVersion, ct);

        if (retried.StatusCode == HttpStatusCode.Unauthorized)
        {
            retried.Dispose();
            session.Clear();
            throw new MobileSignedOutException("The session has ended. Sign in again.");
        }

        return retried;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method,
        string path,
        object? body,
        string? idempotencyKey,
        string? rowVersion,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, options.ResolveUri(path));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (session.Tokens is { } current)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
        }

        // Every mutation is replay-safe: a retry over a flaky connection must not create a
        // second leave request.
        if (!string.IsNullOrWhiteSpace(idempotencyKey) && method != HttpMethod.Get)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        if (!string.IsNullOrWhiteSpace(rowVersion))
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{rowVersion}\"");
        }

        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException exception)
        {
            throw new MobileOfflineException("No connection to the Kapsons server.", exception);
        }
        catch (TaskCanceledException exception) when (!ct.IsCancellationRequested)
        {
            throw new MobileOfflineException("The server took too long to answer.", exception);
        }
    }

    // ---- Reading ----------------------------------------------------------

    private async Task<T> ReadEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(Json, ct);

        if (envelope is null)
        {
            throw new MobileServerException(
                "The server returned an empty response.", (int)response.StatusCode);
        }

        LastWarnings = envelope.Meta?.Warnings ?? [];
        return envelope.Data;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var problem = await ReadProblemAsync(response, ct);
        var correlationId = problem.CorrelationId;

        throw response.StatusCode switch
        {
            // The designed state, not a fault. The module names itself and the components it
            // still needs, so the screen can say which feature is missing.
            HttpStatusCode.NotImplemented => new MobileFeatureNotReadyException(
                problem.Module ?? "This feature", problem.RequiredComponents)
            {
                CorrelationId = correlationId
            },

            HttpStatusCode.Forbidden => new MobileAccessDeniedException(
                problem.Detail ?? problem.Title ?? "You do not have access to this.", problem.Permission)
            {
                CorrelationId = correlationId
            },

            HttpStatusCode.NotFound => new MobileNotFoundException(
                problem.Detail ?? problem.Title ?? "That was not found.")
            {
                CorrelationId = correlationId
            },

            HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed => new MobileConflictException(
                problem.Detail ?? problem.Title ?? "This changed while you were working on it.")
            {
                CorrelationId = correlationId
            },

            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
                new MobileValidationFailedException(
                    problem.Detail ?? problem.Title ?? "Some details need correcting.",
                    problem.Code,
                    problem.Errors)
                {
                    CorrelationId = correlationId
                },

            HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout =>
                new MobileOfflineException("The Kapsons server is not answering right now.")
                {
                    CorrelationId = correlationId
                },

            _ => new MobileServerException(
                problem.Detail ?? problem.Title ?? $"The server answered {(int)response.StatusCode}.",
                (int)response.StatusCode)
            {
                CorrelationId = correlationId
            }
        };
    }

    /// <summary>
    /// Reads the ProblemDetails the API returns, including the extensions it adds — <c>code</c>,
    /// <c>module</c>, <c>requiredComponents</c>, <c>correlationId</c>.
    /// </summary>
    private static async Task<ProblemPayload> ReadProblemAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);

            if (string.IsNullOrWhiteSpace(text))
            {
                return new ProblemPayload();
            }

            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            var payload = new ProblemPayload
            {
                Title = String(root, "title"),
                Detail = String(root, "detail"),
                Code = String(root, "code"),
                Module = String(root, "module"),
                Permission = String(root, "permission"),
                CorrelationId = String(root, "correlationId")
            };

            if (root.TryGetProperty("requiredComponents", out var components) &&
                components.ValueKind == JsonValueKind.Array)
            {
                payload.RequiredComponents = components.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!)
                    .ToList();
            }

            if (root.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                payload.Errors = errors.EnumerateObject().ToDictionary(
                    field => field.Name,
                    field => field.Value.ValueKind == JsonValueKind.Array
                        ? field.Value.EnumerateArray()
                            .Where(item => item.ValueKind == JsonValueKind.String)
                            .Select(item => item.GetString()!)
                            .ToArray()
                        : [field.Value.ToString()],
                    StringComparer.OrdinalIgnoreCase);
            }

            return payload;
        }
        catch (JsonException)
        {
            // A response that is not ProblemDetails is still a failure; it simply carries no
            // detail worth showing.
            return new ProblemPayload();
        }
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // ---- Refresh ----------------------------------------------------------

    /// <summary>
    /// Exchanges the refresh token, once at a time. Several screens loading at once would
    /// otherwise each try to refresh, and all but the first would present a rotated token.
    /// </summary>
    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        await refreshLock.WaitAsync(ct);

        try
        {
            var stored = session.Tokens ?? await tokens.LoadAsync(ct);

            if (stored is null || string.IsNullOrWhiteSpace(stored.RefreshToken))
            {
                return false;
            }

            // Another caller may have refreshed while this one waited on the lock.
            if (session.Tokens is { IsExpired: false } fresh && !ReferenceEquals(fresh, stored))
            {
                return true;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, options.ResolveUri("api/mobile/v1/auth/refresh"))
            {
                Content = JsonContent.Create(
                    new { refreshToken = stored.RefreshToken, deviceId = options.DeviceId }, options: Json)
            };

            using var response = await http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var issued = await response.Content.ReadFromJsonAsync<TokenResponsePayload>(Json, ct);

            if (issued is null || string.IsNullOrWhiteSpace(issued.AccessToken))
            {
                return false;
            }

            var next = new MobileTokens(
                issued.AccessToken,
                string.IsNullOrWhiteSpace(issued.RefreshToken) ? stored.RefreshToken : issued.RefreshToken,
                DateTimeOffset.UtcNow.AddSeconds(issued.ExpiresIn));

            // A refresh re-reads permissions, so a right removed on the HR portal reaches a
            // live session at its next refresh rather than only at the next sign-in.
            session.Apply(next, issued.ToIdentity());
            await tokens.SaveAsync(next, ct);
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private static string NewIdempotencyKey() => Guid.NewGuid().ToString("n");

    private sealed class ProblemPayload
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public string? Code { get; set; }
        public string? Module { get; set; }
        public string? Permission { get; set; }
        public string? CorrelationId { get; set; }
        public IReadOnlyList<string> RequiredComponents { get; set; } = [];
        public IReadOnlyDictionary<string, string[]> Errors { get; set; } =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Mirrors the API's <c>TokenResponse</c>. Auth is the one route that is not wrapped in an
    /// <see cref="ApiEnvelope{T}"/>, because it is answered before there is a session to
    /// correlate against.
    /// </summary>
    internal sealed class TokenResponsePayload
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; } = 1800;
        public bool MustChangePassword { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? TenantKey { get; set; }
        public string Portal { get; set; } = string.Empty;
        public IReadOnlyList<string> Permissions { get; set; } = [];

        public MobileIdentity ToIdentity() =>
            new(TenantKey, DisplayName, Portal, Permissions, MustChangePassword);
    }
}
