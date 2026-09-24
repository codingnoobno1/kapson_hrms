using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Vispl.HRManagement.BM.ITF.Recruitment;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.Identity.Admin;

namespace Vispl.HRManagement.Identity.Candidate;

public enum ActivationFailure { None, InvalidToken, WeakPassword, AlreadyActivated }

public sealed record ActivationResult(ActivationFailure Failure, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Failure == ActivationFailure.None;
    public static ActivationResult Ok() => new(ActivationFailure.None, []);
    public static ActivationResult Fail(ActivationFailure failure, params string[] errors) => new(failure, errors);
}

public interface ICandidateActivationService
{
    /// <summary>
    /// Issues a one-time activation link if a candidate with this address exists. Returns the
    /// raw token only so the caller can deliver it; it is never returned to the requester,
    /// because that would let anyone activate someone else's account.
    /// </summary>
    Task<string?> RequestAsync(string tenantKey, string email, CancellationToken cancellationToken = default);

    Task<ActivationResult> ActivateAsync(
        string tenantKey, string token, string password, CancellationToken cancellationToken = default);
}

public sealed class CandidateActivationService(
    AdminIdentityDbContext dbContext,
    UserManager<AdminUser> userManager,
    ICandidateDirectory candidates,
    ILogger<CandidateActivationService> logger) : ICandidateActivationService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public async Task<string?> RequestAsync(
        string tenantKey, string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var candidate = await candidates.FindByEmailAsync(tenantKey, normalized, cancellationToken);

        // No candidate, or already activated: the caller is told nothing either way.
        if (candidate is null || !string.IsNullOrWhiteSpace(candidate.AccountId))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        // Requesting a new link invalidates any outstanding one, so only the newest works.
        var outstanding = await dbContext.CandidateActivations
            .Where(item => item.TenantKey == tenantKey &&
                           item.CandidateId == candidate.CandidateId &&
                           item.ConsumedUtc == null &&
                           item.InvalidatedUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var item in outstanding)
        {
            item.InvalidatedUtc = now;
        }

        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        dbContext.CandidateActivations.Add(new CandidateActivationEntity
        {
            TokenHash = Hash(raw),
            TenantKey = tenantKey,
            CandidateId = candidate.CandidateId,
            Email = normalized,
            CreatedUtc = now,
            ExpiresUtc = now.Add(Lifetime)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return raw;
    }

    public async Task<ActivationResult> ActivateAsync(
        string tenantKey, string token, string password, CancellationToken cancellationToken = default)
    {
        var hash = Hash(token);
        var now = DateTimeOffset.UtcNow;

        var activation = await dbContext.CandidateActivations
            .FirstOrDefaultAsync(item => item.TokenHash == hash && item.TenantKey == tenantKey, cancellationToken);

        if (activation is null || !activation.IsUsable(now))
        {
            return ActivationResult.Fail(ActivationFailure.InvalidToken, "This link is not valid or has expired.");
        }

        var candidate = await candidates.FindByEmailAsync(tenantKey, activation.Email, cancellationToken);
        if (candidate is null)
        {
            return ActivationResult.Fail(ActivationFailure.InvalidToken, "This link is not valid or has expired.");
        }

        if (!string.IsNullOrWhiteSpace(candidate.AccountId))
        {
            activation.ConsumedUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ActivationResult.Fail(ActivationFailure.AlreadyActivated, "This account is already active.");
        }

        var user = new AdminUser
        {
            // Identity usernames are globally unique, but the same person may apply to two
            // tenants with one address, so the tenant is part of the username. The separator
            // must be inside Identity's default AllowedUserNameCharacters, which excludes ':'.
            UserName = CandidateUserName(tenantKey, activation.Email),
            Email = activation.Email,
            DisplayName = candidate.FullName,
            TenantKey = tenantKey,
            Portal = AccountPortal.Candidate,
            CandidateId = candidate.CandidateId,
            IsActive = true,
            // The candidate chose this password themselves, so there is nothing to force.
            MustChangePassword = false,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        // The same password policy as every other account: length, complexity, and lockout.
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            return ActivationResult.Fail(
                ActivationFailure.WeakPassword,
                created.Errors.Select(error => error.Description).ToArray());
        }

        activation.ConsumedUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        await candidates.LinkAccountAsync(tenantKey, candidate.CandidateId, user.Id, cancellationToken);

        logger.LogInformation(
            "Candidate {CandidateId} activated an account for tenant {TenantKey}.",
            candidate.CandidateId, tenantKey);

        return ActivationResult.Ok();
    }

    /// <summary>The login a candidate signs in with. The mobile app composes the same value.</summary>
    public static string CandidateUserName(string tenantKey, string email) =>
        $"{tenantKey.Trim().ToLowerInvariant()}_{email.Trim().ToLowerInvariant()}";

    private static string Hash(string value) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
