namespace Vispl.HRManagement.MobileApp.Services;

public interface ITokenStore
{
    Task<string?> GetAccessTokenAsync();
    Task SetAccessTokenAsync(string? token);
    Task<string?> GetRefreshTokenAsync();
    Task SetRefreshTokenAsync(string? token);
    Task ClearAsync();
}

/// <summary>
/// Only short-lived tokens live here. Passwords and client secrets are never stored on the device.
/// </summary>
public sealed class SecureTokenStore(ILogger<SecureTokenStore> logger) : ITokenStore
{
    private const string AccessTokenKey = "vispl.hr.access_token";
    private const string RefreshTokenKey = "vispl.hr.refresh_token";

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(AccessTokenKey);
        }
        catch (Exception exception)
        {
            // SecureStorage is unavailable on some emulator images without a configured keystore.
            logger.LogWarning(exception, "Secure storage read failed.");
            return null;
        }
    }

    public async Task SetAccessTokenAsync(string? token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                SecureStorage.Default.Remove(AccessTokenKey);
                return;
            }

            await SecureStorage.Default.SetAsync(AccessTokenKey, token);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Secure storage write failed.");
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(RefreshTokenKey);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Secure storage read failed.");
            return null;
        }
    }

    public async Task SetRefreshTokenAsync(string? token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                SecureStorage.Default.Remove(RefreshTokenKey);
                return;
            }

            await SecureStorage.Default.SetAsync(RefreshTokenKey, token);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Secure storage write failed.");
        }
    }

    public async Task ClearAsync()
    {
        await SetAccessTokenAsync(null);
        await SetRefreshTokenAsync(null);
    }
}
