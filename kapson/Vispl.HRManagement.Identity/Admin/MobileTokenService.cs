using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.Identity.Admin;

public sealed record TokenGrant(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    string TokenType,
    bool MustChangePassword,
    string DisplayName,
    string? TenantKey,
    string Portal,
    IReadOnlyList<string> Permissions);

public enum TokenFailure
{
    None,
    InvalidCredentials,
    LockedOut,
    Disabled,
    NotAllowedPortal,
    InvalidRefreshToken,

    /// <summary>The account is fine; nobody has granted it a phone.</summary>
    MobileAccessNotGranted
}

public sealed record TokenResult(TokenGrant? Grant, TokenFailure Failure)
{
    public bool Succeeded => Grant is not null;
    public static TokenResult Ok(TokenGrant grant) => new(grant, TokenFailure.None);
    public static TokenResult Fail(TokenFailure failure) => new(null, failure);
}

/// <summary>
/// What a mobile password change produced. On success the caller gets a fresh grant, so the
/// app can carry on without a second sign-in; on failure, the reasons to show the person.
/// </summary>
public sealed record PasswordChangeResult(TokenGrant? Grant, IReadOnlyList<string> Errors, bool AccountUnavailable)
{
    public bool Succeeded => Grant is not null;
    public static PasswordChangeResult Ok(TokenGrant grant) => new(grant, [], false);
    public static PasswordChangeResult Invalid(IReadOnlyList<string> errors) => new(null, errors, false);
    public static PasswordChangeResult Unavailable() => new(null, [], true);
}

public interface IMobileTokenService
{
    Task<TokenResult> IssueAsync(LoginIdentifier login, string password, string? deviceId, CancellationToken cancellationToken = default);
    Task<TokenResult> RefreshAsync(string refreshToken, string? deviceId, CancellationToken cancellationToken = default);
    Task RevokeAsync(string refreshToken, bool revokeFamily, CancellationToken cancellationToken = default);
    Task<PasswordChangeResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword, string? deviceId, CancellationToken cancellationToken = default);
}

public sealed class MobileTokenService(
    UserManager<AdminUser> userManager,
    SignInManager<AdminUser> signInManager,
    AdminIdentityDbContext dbContext,
    MobileSigningKeyProvider keyProvider,
    IOptions<MobileTokenOptions> options,
    ILogger<MobileTokenService> logger) : IMobileTokenService
{
    private readonly MobileTokenOptions settings = options.Value;

    public async Task<TokenResult> IssueAsync(
        LoginIdentifier login, string password, string? deviceId, CancellationToken cancellationToken = default)
    {
        var user = await ResolveUserAsync(login, cancellationToken);

        // A missing user and a wrong password return the same failure so the endpoint
        // cannot be used to enumerate accounts.
        if (user is null)
        {
            return TokenResult.Fail(TokenFailure.InvalidCredentials);
        }

        // HR, Employee and Candidate portals use tokens. Platform administrators sign in on
        // the web console instead, so their credentials never mint an API token.
        if (user.Portal is not (AccountPortal.Hr or AccountPortal.Employee or AccountPortal.Candidate))
        {
            return TokenResult.Fail(TokenFailure.NotAllowedPortal);
        }

        if (!user.IsActive)
        {
            return TokenResult.Fail(TokenFailure.Disabled);
        }

        // lockoutOnFailure: true so repeated guesses trip the configured 5-attempt lockout.
        var check = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (check.IsLockedOut)
        {
            return TokenResult.Fail(TokenFailure.LockedOut);
        }

        if (!check.Succeeded)
        {
            return TokenResult.Fail(TokenFailure.InvalidCredentials);
        }

        // Checked after the password, deliberately. Ahead of it, this endpoint would tell an
        // anonymous caller which usernames exist by answering differently for a real account
        // without mobile access than for one that does not exist.
        //
        // Candidates are exempt: their account is created by the act of applying, and gating it
        // would mean nobody could ever reach their own application.
        if (user.Portal is not AccountPortal.Candidate && !user.MobileAccessEnabled)
        {
            return TokenResult.Fail(TokenFailure.MobileAccessNotGranted);
        }

        user.LastLoginUtc = DateTimeOffset.UtcNow;
        user.UpdatedUtc = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        return TokenResult.Ok(await GrantAsync(user, Guid.NewGuid().ToString("N"), deviceId, cancellationToken));
    }

    public async Task<TokenResult> RefreshAsync(
        string refreshToken, string? deviceId, CancellationToken cancellationToken = default)
    {
        var hash = Hash(refreshToken);
        var now = DateTimeOffset.UtcNow;

        var stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            return TokenResult.Fail(TokenFailure.InvalidRefreshToken);
        }

        // Presenting an already-consumed token means it leaked and is being replayed.
        // Revoke the whole rotation family rather than just this link.
        if (stored.ConsumedUtc is not null)
        {
            await RevokeFamilyAsync(stored.FamilyId, "Replay detected", now, cancellationToken);
            logger.LogWarning(
                "Refresh token replay detected for user {UserId}; family {FamilyId} revoked.",
                stored.UserId, stored.FamilyId);
            return TokenResult.Fail(TokenFailure.InvalidRefreshToken);
        }

        if (!stored.IsActive(now))
        {
            return TokenResult.Fail(TokenFailure.InvalidRefreshToken);
        }

        var user = await userManager.FindByIdAsync(stored.UserId);
        // Revoking mobile access ends a live session at its next refresh rather than only at
        // the next sign-in, which is what makes the toggle a real control.
        if (user is not null &&
            user.Portal is not AccountPortal.Candidate &&
            !user.MobileAccessEnabled)
        {
            await RevokeFamilyAsync(stored.FamilyId, "Mobile access revoked", now, cancellationToken);
            return TokenResult.Fail(TokenFailure.MobileAccessNotGranted);
        }

        if (user is null || !user.IsActive)
        {
            await RevokeFamilyAsync(stored.FamilyId, "Account unavailable", now, cancellationToken);
            return TokenResult.Fail(TokenFailure.Disabled);
        }

        stored.ConsumedUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return TokenResult.Ok(await GrantAsync(user, stored.FamilyId, deviceId ?? stored.DeviceId, cancellationToken));
    }

    public async Task RevokeAsync(
        string refreshToken, bool revokeFamily, CancellationToken cancellationToken = default)
    {
        var hash = Hash(refreshToken);
        var now = DateTimeOffset.UtcNow;

        var stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            return;
        }

        if (revokeFamily)
        {
            await RevokeFamilyAsync(stored.FamilyId, "Signed out", now, cancellationToken);
            return;
        }

        stored.RevokedUtc = now;
        stored.RevokedReason = "Signed out";
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Changes the signed-in account's password and clears the forced-change flag.
    ///
    /// Every refresh token the account already holds is revoked, so a session opened with the
    /// old password (a temporary one HR handed out, say) cannot outlive it. A new grant is
    /// issued in their place for the device making the change.
    /// </summary>
    public async Task<PasswordChangeResult> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var user = string.IsNullOrWhiteSpace(userId) ? null : await userManager.FindByIdAsync(userId);

        if (user is null || !user.IsActive ||
            user.Portal is not (AccountPortal.Hr or AccountPortal.Employee or AccountPortal.Candidate) ||
            (user.Portal is not AccountPortal.Candidate && !user.MobileAccessEnabled))
        {
            return PasswordChangeResult.Unavailable();
        }

        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
        {
            return PasswordChangeResult.Invalid(["The new password must be different from the current one."]);
        }

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        if (!result.Succeeded)
        {
            // A wrong current password counts towards lockout, so a stolen access token cannot
            // be used to guess the password behind it without limit.
            if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
            {
                await userManager.AccessFailedAsync(user);
                return PasswordChangeResult.Invalid(["Your current password is not correct."]);
            }

            return PasswordChangeResult.Invalid(result.Errors.Select(error => error.Description).ToList());
        }

        var now = DateTimeOffset.UtcNow;
        user.MustChangePassword = false;
        user.PasswordChangedUtc = now;
        user.UpdatedUtc = now;
        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.UpdateAsync(user);

        var live = await dbContext.RefreshTokens
            .Where(item => item.UserId == user.Id && item.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var item in live)
        {
            item.RevokedUtc = now;
            item.RevokedReason = "Password changed";
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return PasswordChangeResult.Ok(
            await GrantAsync(user, Guid.NewGuid().ToString("N"), deviceId, cancellationToken));
    }

    /// <summary>
    /// Staff resolve by username. Candidates resolve by tenant plus email, scoped to the
    /// Candidate portal so an address shared with a staff account cannot cross over, and so
    /// the same address may apply to two tenants independently.
    /// </summary>
    private async Task<AdminUser?> ResolveUserAsync(LoginIdentifier login, CancellationToken cancellationToken)
    {
        if (!login.IsCandidateLogin)
        {
            var byUserName = await userManager.FindByNameAsync(login.UserName!);

            // A candidate's stored username is a uniqueness device, not a credential. Refusing
            // it here keeps that format an internal detail no client can come to depend on.
            return byUserName?.Portal == AccountPortal.Candidate ? null : byUserName;
        }

        if (string.IsNullOrWhiteSpace(login.TenantKey) || string.IsNullOrWhiteSpace(login.Email))
        {
            return null;
        }

        var normalizedEmail = userManager.NormalizeEmail(login.Email);

        return await userManager.Users.FirstOrDefaultAsync(
            item => item.Portal == AccountPortal.Candidate &&
                    item.TenantKey == login.TenantKey &&
                    item.NormalizedEmail == normalizedEmail,
            cancellationToken);
    }

    // ---- Internals --------------------------------------------------------

    private async Task<TokenGrant> GrantAsync(
        AdminUser user, string familyId, string? deviceId, CancellationToken cancellationToken)
    {
        var permissions = await ResolvePermissionsAsync(user, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("display_name", user.DisplayName),
            new("account_portal", user.Portal.ToString()),
            new("must_change_password", user.MustChangePassword ? "true" : "false")
        };

        // The API reads tenant_id. Emitting tenant_key here as well would invite the two
        // names to drift again, so only the name the API consumes is issued.
        if (!string.IsNullOrWhiteSpace(user.TenantKey))
        {
            claims.Add(new Claim("tenant_id", user.TenantKey));
        }

        if (!string.IsNullOrWhiteSpace(user.EmployeeNumber))
        {
            claims.Add(new Claim("employee_number", user.EmployeeNumber));
        }

        // Candidate-owned routes authorise on this claim, so it is the only thing that ties a
        // token to an applicant's own applications and onboarding.
        if (user.Portal == AccountPortal.Candidate && user.CandidateId.HasValue)
        {
            claims.Add(new Claim("candidate_id", user.CandidateId.Value.ToString()));
        }

        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(keyProvider.Key), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = await CreateRefreshTokenAsync(user, familyId, deviceId, now, cancellationToken);

        return new TokenGrant(
            accessToken,
            refreshToken,
            (int)(expires - now).TotalSeconds,
            "Bearer",
            user.MustChangePassword,
            user.DisplayName,
            user.TenantKey,
            user.Portal.ToString(),
            permissions);
    }

    private async Task<string> CreateRefreshTokenAsync(
        AdminUser user, string familyId, string? deviceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        dbContext.RefreshTokens.Add(new RefreshTokenEntity
        {
            TokenHash = Hash(raw),
            UserId = user.Id,
            TenantKey = user.TenantKey,
            FamilyId = familyId,
            DeviceId = deviceId,
            CreatedUtc = now,
            ExpiresUtc = now.AddDays(settings.RefreshTokenDays)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return raw;
    }

    private async Task<IReadOnlyList<string>> ResolvePermissionsAsync(
        AdminUser user, CancellationToken cancellationToken)
    {
        // A candidate never holds an HR permission. Returning early keeps that a property of
        // the code rather than of whatever roles happen to be assigned.
        if (user.Portal == AccountPortal.Candidate)
        {
            return [];
        }

        var roleNames = await userManager.GetRolesAsync(user);
        if (roleNames.Count == 0)
        {
            return [];
        }

        var roleIds = await dbContext.Roles
            .Where(role => roleNames.Contains(role.Name!))
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        return await dbContext.RolePermissions
            .Where(permission => roleIds.Contains(permission.RoleId))
            .Select(permission => permission.PermissionKey)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task RevokeFamilyAsync(
        string familyId, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var family = await dbContext.RefreshTokens
            .Where(item => item.FamilyId == familyId && item.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var item in family)
        {
            item.RevokedUtc = now;
            item.RevokedReason = reason;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(string value) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
