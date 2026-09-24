namespace Vispl.HRManagement.Identity.Admin;

/// <summary>
/// How a caller identifies itself when asking for a token.
///
/// Staff sign in with the username an administrator gave them. Candidates never had a username
/// chosen for them — they identify by the company they applied to plus the address they applied
/// with, and the account is resolved by lookup. Composing a username from those parts would tie
/// sign-in to a storage detail, so it deliberately is not done here or by any client.
/// </summary>
public readonly record struct LoginIdentifier
{
    private LoginIdentifier(string? userName, string? tenantKey, string? email)
    {
        UserName = userName;
        TenantKey = tenantKey;
        Email = email;
    }

    public string? UserName { get; }
    public string? TenantKey { get; }
    public string? Email { get; }

    public bool IsCandidateLogin => UserName is null;

    public static LoginIdentifier ForUserName(string userName) =>
        new(userName.Trim(), null, null);

    public static LoginIdentifier ForCandidate(string tenantKey, string email) =>
        new(null, tenantKey.Trim().ToLowerInvariant(), email.Trim().ToLowerInvariant());
}
