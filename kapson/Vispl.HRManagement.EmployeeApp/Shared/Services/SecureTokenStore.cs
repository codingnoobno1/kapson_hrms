using System.Text.Json;
using Vispl.HRManagement.Mobile.Client.Auth;

namespace Vispl.HRManagement.EmployeeApp.Shared.Services;

/// <summary>
/// Tokens in the platform's own protected store — Keychain on iOS, Keystore-backed
/// EncryptedSharedPreferences on Android, DPAPI on Windows.
///
/// This is the only correct home for a refresh token. If the platform store is unavailable —
/// which happens on some Windows configurations and in previews — the tokens are held for the
/// process only. That costs a sign-in per launch, which is the safe way to be wrong; writing
/// them to unprotected storage instead would not be.
/// </summary>
public sealed class SecureTokenStore : ITokenStore
{
    private const string Key = "kapsons.employee.tokens.v1";

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
            var payload = Preferences.Default.Get<string?>(Key, null);

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
            Preferences.Default.Set(Key, JsonSerializer.Serialize(tokens, Json));
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
            Preferences.Default.Remove(Key);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Secure storage clear failed: {exception.Message}");
        }

        await fallback.ClearAsync(cancellationToken);
    }

    private void Degrade(Exception exception)
    {
        // Logged without the payload: a message that included the token would put it in exactly
        // the place this class exists to keep it out of.
        System.Diagnostics.Debug.WriteLine(
            $"Secure storage unavailable ({exception.GetType().Name}); tokens are session-only.");

        secureStorageWorks = false;
    }
}

/// <summary>
/// The installation's identity, stable across launches.
///
/// A punch is device-bound and a refresh token is issued against a device, so this must not
/// change when the app restarts. It is not a hardware id: those are restricted on modern
/// platforms and identify a person across apps, which this has no business doing.
/// </summary>
public static class DeviceIdentity
{
    private const string Key = "kapsons.employee.deviceid.v1";

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

            // A per-run id still lets the app work; it only means the server sees a new device
            // each launch, which is visible in the device list rather than silently wrong.
            return Guid.NewGuid().ToString("n");
        }
    }
}
