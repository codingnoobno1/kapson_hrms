using Vispl.HRManagement.Mobile.Client.Auth;

namespace Vispl.HRManagement.Mobile.Shared.Services;

/// <summary>
/// Signs an HR user in against the real token endpoint.
///
/// This replaced <c>PreviewAuthenticationService</c>, which accepted any email with the
/// password <c>Demo@1234</c>, never contacted a server, and issued no token — so every screen
/// behind it was showing hardcoded data to somebody who had not actually authenticated.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>Returns null on success, or the reason to show the person.</summary>
    Task<string?> SignInAsync(string userName, string password, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    /// <summary>Restores a stored session at launch, refreshing the token if it has expired.</summary>
    Task<bool> TryRestoreAsync(CancellationToken cancellationToken = default);
}

public sealed class HrAuthenticationService(MobileAuthenticator authenticator) : IAuthenticationService
{
    public async Task<string?> SignInAsync(
        string userName, string password, CancellationToken cancellationToken = default)
    {
        var outcome = await authenticator.SignInAsync(userName, password, cancellationToken);

        if (!outcome.Succeeded)
        {
            return outcome.Message ?? "Could not sign in.";
        }

        if (outcome.MustChangePassword)
        {
            // The server will refuse real work until the password is changed, and there is no
            // change-password screen here yet. Saying so beats an app that silently fails.
            await authenticator.SignOutAsync(false, cancellationToken);
            return "Your password must be changed before you can use the app. Ask your administrator.";
        }

        return null;
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        authenticator.SignOutAsync(false, cancellationToken);

    public Task<bool> TryRestoreAsync(CancellationToken cancellationToken = default) =>
        authenticator.TryRestoreAsync(cancellationToken);
}
