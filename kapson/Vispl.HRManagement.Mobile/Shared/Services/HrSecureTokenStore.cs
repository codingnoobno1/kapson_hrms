using System.Text.Json;
using Vispl.HRManagement.Mobile.Client.Auth;

namespace Vispl.HRManagement.Mobile.Shared.Services;

/// <summary>
/// Tokens in the platform's own protected store — Keychain, Keystore-backed
/// EncryptedSharedPreferences, DPAPI.
///
/// Keyed separately from the employee app so the two can be installed side by side on one
/// device without one signing the other out.
/// </summary>
public sealed class HrSecureTokenStore : ITokenStore
{
    private const string Key = "kapsons.hr.tokens.v1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly InMemoryTokenStore fallback = new();

    private bool secureStorageWorks = true;

    public async Task<MobileTokens?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!secureStorageWorks)
        {
            return await fallback.LoadAsync(cancellationToken);
        }

        try
        {
            var payload = await SecureStorage.Default.GetAsync(Key);

            return string.IsNullOrWhiteSpace(payload)
                ? null
                : JsonSerializer.Deserialize<MobileTokens>(payload, Json);
        }
        catch (Exception exception)
        {
            Degrade(exception);
            return await fallback.LoadAsync(cancellationToken);
        }
    }

    public async Task SaveAsync(MobileTokens tokens, CancellationToken cancellationToken = default)
    {
        if (!secureStorageWorks)
        {
            await fallback.SaveAsync(tokens, cancellationToken);
            return;
        }

        try
        {
            await SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(tokens, Json));
        }
        catch (Exception exception)
        {
            Degrade(exception);
            await fallback.SaveAsync(tokens, cancellationToken);
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SecureStorage.Default.Remove(Key);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Secure storage clear failed: {exception.Message}");
        }

        await fallback.ClearAsync(cancellationToken);
    }

    /// <summary>
    /// Falls back to memory when the platform store is unavailable. That costs a sign-in per
    /// launch, which is the safe way to be wrong; writing a refresh token somewhere
    /// unprotected instead would not be.
    /// </summary>
    private void Degrade(Exception exception)
    {
        // Logged without the payload: a message carrying the token would put it in exactly the
        // place this class exists to keep it out of.
        System.Diagnostics.Debug.WriteLine(
            $"Secure storage unavailable ({exception.GetType().Name}); tokens are session-only.");

        secureStorageWorks = false;
    }
}

/// <summary>
/// This installation's identity, stable across launches.
///
/// A refresh token is issued against a device, so this must not change on restart. It is not a
/// hardware id: those are restricted on modern platforms and identify a person across apps,
/// which this has no business doing.
/// </summary>
public static class HrDeviceIdentity
{
    private const string Key = "kapsons.hr.deviceid.v1";

    public static string Resolve()
    {
        try
        {
            var existing = Preferences.Default.Get<string?>(Key, null);

            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            var created = Guid.NewGuid().ToString("n");
            Preferences.Default.Set(Key, created);
            return created;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Device id unavailable: {exception.Message}");
            return Guid.NewGuid().ToString("n");
        }
    }
}
