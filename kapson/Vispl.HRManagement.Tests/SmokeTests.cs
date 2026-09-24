using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Vispl.HRManagement.Tests;

/// <summary>
/// Hits a running host. These assert the endpoints answer for real rather than 501, which is
/// the one thing reflection cannot tell you.
///
/// Requires a host and the SMOKE_BASE_URL environment variable, for example:
///   SMOKE_BASE_URL=http://localhost:7000 dotnet test --filter Category=Smoke
///
/// Optional, to cover the authenticated endpoints:
///   SMOKE_USERNAME, SMOKE_PASSWORD
///
/// Run everything except these with: dotnet test --filter Category!=Smoke
/// </summary>
[Trait("Category", "Smoke")]
public sealed class SmokeTests : IAsyncLifetime
{
    private HttpClient client = null!;
    private string? accessToken;

    private static string? BaseUrl => Environment.GetEnvironmentVariable("SMOKE_BASE_URL");

    public async Task InitializeAsync()
    {
        Assert.True(
            !string.IsNullOrWhiteSpace(BaseUrl),
            "SMOKE_BASE_URL is not set. These tests need a running host; see the class comment.");

        client = new HttpClient { BaseAddress = new Uri(BaseUrl!), Timeout = TimeSpan.FromSeconds(20) };

        var userName = Environment.GetEnvironmentVariable("SMOKE_USERNAME");
        var password = Environment.GetEnvironmentVariable("SMOKE_PASSWORD");

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var response = await client.PostAsJsonAsync(
            "/api/mobile/v1/auth/token",
            new { userName, password, deviceId = "smoke-test" });

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        accessToken = document.RootElement.GetProperty("accessToken").GetString();
    }

    public Task DisposeAsync()
    {
        client?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Host_is_reachable()
    {
        var response = await client.GetAsync("/api/mobile/v1/discovery/metadata");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_endpoints_answer_for_real()
    {
        var metadata = await client.GetAsync("/api/mobile/v1/discovery/metadata");
        Assert.Equal(HttpStatusCode.OK, metadata.StatusCode);

        var tenant = await client.PostAsJsonAsync(
            "/api/mobile/v1/discovery/resolve-tenant", new { companyCodeOrDomain = "kapson" });
        Assert.Equal(HttpStatusCode.OK, tenant.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_are_protected()
    {
        // No token: these must challenge rather than leak or 404.
        foreach (var path in new[]
                 {
                     "/api/mobile/v1/masters",
                     "/api/mobile/v1/recruitment/jobs",
                     "/api/mobile/v1/recruitment/applications",
                     "/api/mobile/v1/session/bootstrap"
                 })
        {
            var response = await client.GetAsync(path);
            Assert.True(
                response.StatusCode == HttpStatusCode.Unauthorized,
                $"{path} returned {(int)response.StatusCode}; expected 401. " +
                "A 404 means the route is gone.");
        }
    }

    [Fact]
    public async Task Credentials_are_not_an_account_oracle()
    {
        var unknown = await client.PostAsJsonAsync(
            "/api/mobile/v1/auth/token",
            new { userName = "no-such-account", password = "Whatever#12345", deviceId = "smoke" });

        var wrong = await client.PostAsJsonAsync(
            "/api/mobile/v1/auth/token",
            new { userName = "admin", password = "Whatever#12345", deviceId = "smoke" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(
            await unknown.Content.ReadAsStringAsync() is var a ? Body(a) : string.Empty,
            Body(await wrong.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Implemented_endpoints_do_not_answer_not_implemented()
    {
        Assert.True(
            accessToken is not null,
            "SMOKE_USERNAME and SMOKE_PASSWORD are not set, so the authenticated endpoints " +
            "cannot be checked. Set them to run this test.");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/mobile/v1/masters");
        request.Headers.Authorization = new("Bearer", accessToken);

        foreach (var path in new[]
                 {
                     "/api/mobile/v1/masters",
                     "/api/mobile/v1/recruitment/jobs",
                     "/api/mobile/v1/recruitment/applications",
                     "/api/mobile/v1/session/bootstrap"
                 })
        {
            using var probe = new HttpRequestMessage(HttpMethod.Get, path);
            probe.Headers.Authorization = new("Bearer", accessToken);
            var response = await client.SendAsync(probe);

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{path} returned {(int)response.StatusCode}. A 501 means its service fell " +
                "back to the not-ready proxy; a 404 means the route is gone.");
        }
    }

    /// <summary>Strips the correlation id so two error bodies can be compared.</summary>
    private static string Body(string json)
    {
        using var document = JsonDocument.Parse(json);
        return string.Join('|', document.RootElement
            .EnumerateObject()
            .Where(property => property.Name is not ("correlationId" or "instance"))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}={property.Value}"));
    }
}
