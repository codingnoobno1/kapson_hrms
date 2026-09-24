namespace Vispl.HRManagement.MobileApp.Services.Clients;

/// <summary>
/// Staff identify by the username an administrator gave them. Candidates identify by the
/// company plus the address they applied with. The app sends whichever the user supplied and
/// never composes an internal username.
/// </summary>
public sealed record TokenResponseDto(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    bool MustChangePassword,
    string DisplayName,
    string? TenantKey,
    string Portal,
    IReadOnlyList<string> Permissions);

public sealed class AuthApi(HrApiClient client)
{
    private const string BasePath = "api/mobile/v1/auth";

    public Task<ApiResult<TokenResponseDto>> SignInAsStaffAsync(
        string userName, string password, string deviceId, CancellationToken cancellationToken = default) =>
        client.PostRawAsync<TokenResponseDto>(
            $"{BasePath}/token",
            new { userName, password, deviceId },
            cancellationToken);

    public Task<ApiResult<TokenResponseDto>> SignInAsCandidateAsync(
        string tenant, string email, string password, string deviceId, CancellationToken cancellationToken = default) =>
        client.PostRawAsync<TokenResponseDto>(
            $"{BasePath}/token",
            new { tenant, email, password, deviceId },
            cancellationToken);

    public Task<ApiResult<TokenResponseDto>> RefreshAsync(
        string refreshToken, string deviceId, CancellationToken cancellationToken = default) =>
        client.PostRawAsync<TokenResponseDto>(
            $"{BasePath}/refresh",
            new { refreshToken, deviceId },
            cancellationToken);

    public Task<ApiResult<object>> RevokeAsync(
        string refreshToken, bool allDevices, CancellationToken cancellationToken = default) =>
        client.PostRawAsync<object>(
            $"{BasePath}/revoke",
            new { refreshToken, allDevices },
            cancellationToken);
}
