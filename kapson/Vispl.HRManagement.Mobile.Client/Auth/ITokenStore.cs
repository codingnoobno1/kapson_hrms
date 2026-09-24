namespace Vispl.HRManagement.Mobile.Client.Auth;

/// <summary>
/// Where tokens live between launches.
///
/// Behind an interface because the only correct implementation is platform secure storage —
/// Keychain, Keystore, DPAPI — and this library must stay buildable and testable without a
/// device. The app head supplies the real one.
/// </summary>
public interface ITokenStore
{
    Task<MobileTokens?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(MobileTokens tokens, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Holds tokens for the life of the process only.
///
/// The fallback when secure storage is unavailable, and the implementation tests use. Signing
/// in again after a restart is a worse experience than persisting; writing a refresh token to
/// somewhere unprotected is a worse outcome than either.
/// </summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    private MobileTokens? tokens;

    public Task<MobileTokens?> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(tokens);

    public Task SaveAsync(MobileTokens value, CancellationToken cancellationToken = default)
    {
        tokens = value;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        tokens = null;
        return Task.CompletedTask;
    }
}
