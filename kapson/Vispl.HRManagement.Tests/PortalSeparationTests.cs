using System.Net;
using Xunit;

namespace Vispl.HRManagement.Tests;

/// <summary>
/// The two web portals must stay separate: an HR account cannot reach platform administration,
/// and a platform administrator cannot walk into the HR portal.
///
/// This is a stated requirement of the product, and it is enforced by an authorization policy
/// that a future change could weaken without any compile error. Hence a test against a running
/// host rather than a code inspection.
///
/// Requires SMOKE_BASE_URL. Optional: SMOKE_HR_USERNAME / SMOKE_HR_PASSWORD and
/// SMOKE_ADMIN_USERNAME / SMOKE_ADMIN_PASSWORD to cover the signed-in cases.
/// </summary>
[Trait("Category", "Smoke")]
public sealed class PortalSeparationTests
{
    private static string? BaseUrl => Environment.GetEnvironmentVariable("SMOKE_BASE_URL");

    private static HttpClient CreateClient(CookieContainer cookies) =>
        new(new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(BaseUrl!),
            Timeout = TimeSpan.FromSeconds(20)
        };

    private static void RequireHost() =>
        Assert.True(
            !string.IsNullOrWhiteSpace(BaseUrl),
            "SMOKE_BASE_URL is not set. This test needs a running host; see the class comment.");

    [Fact]
    public async Task Hr_portal_pages_are_not_anonymous()
    {
        RequireHost();
        using var client = CreateClient(new CookieContainer());

        foreach (var path in new[] { "/hr/dashboard", "/hr/change-password" })
        {
            var response = await client.GetAsync(path);

            Assert.True(
                response.StatusCode is HttpStatusCode.Found or HttpStatusCode.Redirect,
                $"{path} answered {(int)response.StatusCode} anonymously; it must redirect to sign in.");
        }
    }

    [Fact]
    public async Task Hr_sign_in_page_is_reachable_and_admin_stays_separate()
    {
        RequireHost();
        using var client = CreateClient(new CookieContainer());

        var hr = await client.GetAsync("/hr");
        Assert.Equal(HttpStatusCode.OK, hr.StatusCode);

        var admin = await client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);

        // Two sign-in pages, two endpoints. If these ever collapse into one the separation is
        // gone whatever the policies say.
        var hrBody = await hr.Content.ReadAsStringAsync();
        Assert.Contains("/identity/hr/login", hrBody, StringComparison.Ordinal);
        Assert.DoesNotContain("/identity/admin/login", hrBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/identity/hr/login", "SMOKE_ADMIN_USERNAME", "SMOKE_ADMIN_PASSWORD", "/hr")]
    [InlineData("/identity/admin/login", "SMOKE_HR_USERNAME", "SMOKE_HR_PASSWORD", "/admin")]
    public async Task An_account_cannot_sign_in_to_the_other_portal(
        string endpoint, string userVariable, string passwordVariable, string loginPage)
    {
        RequireHost();

        var userName = Environment.GetEnvironmentVariable(userVariable);
        var password = Environment.GetEnvironmentVariable(passwordVariable);

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return; // Credentials not supplied; the anonymous checks above still ran.
        }

        var cookies = new CookieContainer();
        using var client = CreateClient(cookies);

        var token = await AntiforgeryTokenAsync(client, loginPage);

        var response = await client.PostAsync(endpoint, new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token ?? string.Empty),
            new KeyValuePair<string, string>("userName", userName),
            new KeyValuePair<string, string>("password", password)
        ]));

        var location = response.Headers.Location?.ToString() ?? string.Empty;

        Assert.True(
            location.Contains("error=invalid", StringComparison.Ordinal),
            $"An account from the other portal was accepted at {endpoint}; it redirected to '{location}'.");
    }

    private static async Task<string?> AntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = System.Text.RegularExpressions.Regex.Match(
            html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        return match.Success ? match.Groups[1].Value : null;
    }
}
