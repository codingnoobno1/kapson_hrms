namespace Vispl.HRManagement.Mail.Shared;

/// <summary>
/// The URLs that go into candidate mail.
///
/// One place, because a link in an email cannot be corrected once it is sent. Two callers
/// composing the same URL by hand is how one of them ends up pointing at a route that was
/// renamed — which is exactly what the logged <c>/career/{tenant}/activate</c> link did: it
/// named a page that was never built.
/// </summary>
public static class CandidateLinks
{
    /// <summary>
    /// Where a candidate sets their password. The token is in the query because it arrives by
    /// mail, and the page consumes it immediately rather than keeping it in the address.
    /// </summary>
    public static string Activation(string baseUrl, string tenantKey, string token) =>
        $"{baseUrl.TrimEnd('/')}/onboarding" +
        $"?tenant={Uri.EscapeDataString(tenantKey)}&token={Uri.EscapeDataString(token)}";

    /// <summary>Where they return to, once they have a password.</summary>
    public static string Onboarding(string baseUrl, string tenantKey) =>
        $"{baseUrl.TrimEnd('/')}/onboarding?tenant={Uri.EscapeDataString(tenantKey)}";
}
