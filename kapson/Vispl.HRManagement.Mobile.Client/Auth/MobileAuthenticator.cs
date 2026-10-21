using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Vispl.HRManagement.Mobile.Client.Http;

namespace Vispl.HRManagement.Mobile.Client.Auth;

/// <summary>What a sign-in attempt produced.</summary>
public sealed record SignInOutcome(bool Succeeded, string? Message, bool MustChangePassword)
{
    public static SignInOutcome Ok(bool mustChangePassword) => new(true, null, mustChangePassword);

    public static SignInOutcome Failed(string message) => new(false, message, false);
}

/// <summary>
/// Signs in, restores a session at launch, and signs out.
///
/// The employee app authenticates with a username and password against
/// <c>api/mobile/v1/auth/token</c>, which is what the platform issues employee accounts. The
/// wider plan calls for OIDC Authorization Code + PKCE; that changes this class alone, because
/// nothing above it ever sees a credential.
/// </summary>
public sealed class MobileAuthenticator(
    HttpClient http,
    MobileSession session,
    ITokenStore store,
    MobileApiOptions options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SignInOutcome> SignInAsync(
        string userName, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return SignInOutcome.Failed("Enter your username and password.");
        }

        try
        {
            using var response = await http.PostAsJsonAsync(
                options.ResolveUri("api/mobile/v1/auth/token"),
                new
                {
                    userName = userName.Trim(),
                    password,
                    deviceId = options.DeviceId
                },
                Json,
                ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                // Deliberately not distinguishing an unknown user from a wrong password: doing
                // so tells an attacker which usernames exist.
                return SignInOutcome.Failed("That username and password do not match.");
            }

            // The credentials were right but the account has no phone. Saying so is what stops
            // somebody retyping a correct password until they lock themselves out.
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return SignInOutcome.Failed(await DescribeForbiddenAsync(response, ct));
            }

            if (!response.IsSuccessStatusCode)
            {
                return SignInOutcome.Failed("Could not sign in right now. Try again shortly.");
            }

            var issued = await response.Content
                .ReadFromJsonAsync<MobileApiClient.TokenResponsePayload>(Json, ct);

            if (issued is null || string.IsNullOrWhiteSpace(issued.AccessToken))
            {
                return SignInOutcome.Failed("The server did not return a usable sign-in.");
            }

            // Each app head accepts only the portals it was built for, so signing in to the
            // wrong app fails plainly instead of opening a shell meant for somebody else.
            if (!string.IsNullOrWhiteSpace(issued.Portal) &&
                !options.AllowedPortals.Contains(issued.Portal, StringComparer.OrdinalIgnoreCase))
            {
                return SignInOutcome.Failed(
                    $"This is a {issued.Portal} account. It does not sign in to this app.");
            }

            var tokens = new MobileTokens(
                issued.AccessToken,
                issued.RefreshToken,
                DateTimeOffset.UtcNow.AddSeconds(issued.ExpiresIn));

            session.Apply(tokens, issued.ToIdentity());
            await store.SaveAsync(tokens, ct);

            return SignInOutcome.Ok(issued.MustChangePassword);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return SignInOutcome.Failed("No connection to the Kapsons server.");
        }
    }

    /// <summary>
    /// Restores a session at launch.
    ///
    /// A stored access token that is still valid is used as-is; an expired one is refreshed
    /// through the same path a mid-session 401 takes. Either way the app opens on Home rather
    /// than asking someone to sign in every morning.
    /// </summary>
    public async Task<bool> TryRestoreAsync(CancellationToken ct = default)
    {
        var stored = await store.LoadAsync(ct);

        if (stored is null)
        {
            return false;
        }

        session.Apply(stored);

        if (!stored.IsExpired)
        {
            return true;
        }

        try
        {
            using var response = await http.PostAsJsonAsync(
                options.ResolveUri("api/mobile/v1/auth/refresh"),
                new { refreshToken = stored.RefreshToken, deviceId = options.DeviceId },
                Json,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                await SignOutLocallyAsync(ct);
                return false;
            }

            var issued = await response.Content
                .ReadFromJsonAsync<MobileApiClient.TokenResponsePayload>(Json, ct);

            if (issued is null || string.IsNullOrWhiteSpace(issued.AccessToken))
            {
                await SignOutLocallyAsync(ct);
                return false;
            }

            var tokens = new MobileTokens(
                issued.AccessToken,
                string.IsNullOrWhiteSpace(issued.RefreshToken) ? stored.RefreshToken : issued.RefreshToken,
                DateTimeOffset.UtcNow.AddSeconds(issued.ExpiresIn));

            session.Apply(tokens, issued.ToIdentity());
            await store.SaveAsync(tokens, ct);
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Offline at launch. The tokens are kept — the person is not signed out because
            // their train went into a tunnel — and the shell shows the offline state instead.
            return false;
        }
    }

    /// <summary>
    /// Signs out, telling the server first so the refresh token is revoked rather than left
    /// live until it expires. A server that cannot be reached does not block the local sign-out.
    /// </summary>
    public async Task SignOutAsync(bool allDevices = false, CancellationToken ct = default)
    {
        var stored = session.Tokens ?? await store.LoadAsync(ct);

        if (stored is not null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, options.ResolveUri("api/mobile/v1/auth/revoke"))
                {
                    Content = JsonContent.Create(
                        new { refreshToken = stored.RefreshToken, allDevices }, options: Json)
                };

                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", stored.AccessToken);

                using var _ = await http.SendAsync(request, ct);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                // Revocation is best effort. Leaving the tokens on the device because the
                // network was down would be the worse failure.
            }
        }

        await SignOutLocallyAsync(ct);
    }

    /// <summary>
    /// Reads the ProblemDetails behind a 403 so the reason reaches the person, rather than a
    /// blanket "access denied" they cannot act on.
    /// </summary>
    private static async Task<string> DescribeForbiddenAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);

            if (!string.IsNullOrWhiteSpace(text))
            {
                using var document = System.Text.Json.JsonDocument.Parse(text);

                if (document.RootElement.TryGetProperty("detail", out var detail) &&
                    detail.ValueKind == System.Text.Json.JsonValueKind.String &&
                    detail.GetString() is { Length: > 0 } message)
                {
                    return message;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Not ProblemDetails. The generic message below still tells the truth.
        }

        return "This account cannot sign in to the app. Ask HR or your administrator.";
    }

    private async Task SignOutLocallyAsync(CancellationToken ct)
    {
        await store.ClearAsync(ct);
        session.Clear();
    }
}
