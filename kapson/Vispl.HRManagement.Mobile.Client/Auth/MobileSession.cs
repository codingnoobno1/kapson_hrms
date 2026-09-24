using System.Text;
using System.Text.Json;

namespace Vispl.HRManagement.Mobile.Client.Auth;

/// <summary>The tokens a signed-in session holds. Never logged, never written unencrypted.</summary>
public sealed record MobileTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresUtc)
{
    /// <summary>
    /// Treated as expired a minute early, so a request is not sent with a token that dies in
    /// flight and comes back 401 for a reason the app could have predicted.
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresUtc.AddMinutes(-1);
}

/// <summary>
/// What the token endpoint says about the caller, alongside the tokens themselves.
///
/// The API returns these explicitly rather than leaving the client to read them out of the JWT,
/// so they are taken at face value and claim parsing is only the fallback.
/// </summary>
public sealed record MobileIdentity(
    string? TenantKey,
    string? DisplayName,
    string? Portal,
    IReadOnlyList<string> Permissions,
    bool MustChangePassword);

/// <summary>
/// Who is signed in.
///
/// Permissions come from what the server issued, because that is what it enforces against.
/// Deriving them anywhere else would let the app show a control the server then refuses.
/// </summary>
public sealed class MobileSession
{
    private MobileTokens? tokens;
    private HashSet<string> permissions = new(StringComparer.OrdinalIgnoreCase);

    public bool IsSignedIn => tokens is not null;

    public string? TenantKey { get; private set; }

    public string? EmployeeNumber { get; private set; }

    public string? UserId { get; private set; }

    public string? DisplayName { get; private set; }

    /// <summary>The server is demanding a password change before real work is allowed.</summary>
    public bool MustChangePassword { get; private set; }

    public MobileTokens? Tokens => tokens;

    public IReadOnlyCollection<string> Permissions => permissions;

    public bool Has(string permission) => permissions.Contains(permission);

    /// <summary>Raised when the session ends, so the shell can return to sign-in.</summary>
    public event EventHandler? SignedOut;

    /// <summary>Raised when a sign-in or refresh changes who is signed in or what they may do.</summary>
    public event EventHandler? Changed;

    public void Apply(MobileTokens issued, MobileIdentity? identity = null)
    {
        tokens = issued;

        // The response is authoritative where it speaks. The claims carry two things it does
        // not — the account id and the employee number — and both are needed to address the
        // caller's own records.
        var claims = ReadClaims(issued.AccessToken);

        TenantKey = identity?.TenantKey
                    ?? Single(claims, "tenant_id")
                    ?? Single(claims, "tenant_key");

        DisplayName = string.IsNullOrWhiteSpace(identity?.DisplayName)
            ? Single(claims, "display_name") ?? Single(claims, "name")
            : identity.DisplayName;

        EmployeeNumber = Single(claims, "employee_number");

        UserId = Single(claims, "sub")
                 ?? Single(claims, "nameid")
                 ?? Single(claims, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        permissions = new HashSet<string>(
            identity?.Permissions is { Count: > 0 } granted ? granted : All(claims, "permission"),
            StringComparer.OrdinalIgnoreCase);

        MustChangePassword = identity?.MustChangePassword ?? false;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        tokens = null;
        TenantKey = null;
        EmployeeNumber = null;
        UserId = null;
        DisplayName = null;
        MustChangePassword = false;
        permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Changed?.Invoke(this, EventArgs.Empty);
        SignedOut?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Reads the JWT payload without verifying it.
    ///
    /// That is deliberate and safe here: the token is only being read for what to draw. Every
    /// decision that matters is made server-side against the same token, so a client that
    /// mis-read it can show the wrong menu but cannot grant itself anything.
    /// </summary>
    private static Dictionary<string, List<string>> ReadClaims(string accessToken)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var parts = accessToken.Split('.');
        if (parts.Length < 2)
        {
            return result;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(FromBase64Url(parts[1]));
            using var document = JsonDocument.Parse(payload);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var values = new List<string>();

                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    values.AddRange(property.Value.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString()!)
                        .Where(item => item is not null));
                }
                else if (property.Value.ValueKind == JsonValueKind.String)
                {
                    values.Add(property.Value.GetString()!);
                }

                if (values.Count > 0)
                {
                    result[property.Name] = values;
                }
            }
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            // A token this app cannot parse still authenticates every call; it only means the
            // permission-shaped parts of the UI stay hidden until the next refresh.
        }

        return result;
    }

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }

    private static string? Single(Dictionary<string, List<string>> claims, string name) =>
        claims.TryGetValue(name, out var values) ? values.FirstOrDefault() : null;

    private static IEnumerable<string> All(Dictionary<string, List<string>> claims, string name) =>
        claims.TryGetValue(name, out var values) ? values : [];
}
