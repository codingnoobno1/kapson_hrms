using Microsoft.AspNetCore.DataProtection;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.Composition.Shared;
using Vispl.HRManagement.UI.Components;

var builder = WebApplication.CreateBuilder(args);

// Syncfusion (XlsIO Excel downloads). The key comes from configuration or the environment and is
// never committed: a licence key in source is a key in every clone and every build log. Without
// one the workbooks still generate but carry Syncfusion's trial notice.
var syncfusionLicenseKey = builder.Configuration["Syncfusion:LicenseKey"]
    ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://localhost:7000");
}

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Resolves the signed-in HR user into the caller context the business services expect, so a
// portal page and a mobile request reach those services the same way.
builder.Services.AddScoped<Vispl.HRManagement.UI.Components.Hr.Shared.HrPortalCaller>();
builder.Services.AddScoped<Vispl.HRManagement.UI.Components.Hr.Shared.HrToastService>();

var dataProtectionApplicationName = builder.Configuration["DataProtection:ApplicationName"]
    ?? "Vispl.HRManagement";
var configuredKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? ".data-protection-keys";
var dataProtectionKeysPath = Path.IsPathRooted(configuredKeysPath)
    ? configuredKeysPath
    : Path.Combine(builder.Environment.ContentRootPath, configuredKeysPath);

Directory.CreateDirectory(dataProtectionKeysPath);

builder.Services
    .AddDataProtection()
    .SetApplicationName(dataProtectionApplicationName)
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "Vispl.HR.Antiforgery.v1";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// Behind a reverse proxy or tunnel (Cloudflare, nginx, IIS ARR) TLS ends at the proxy and the app
// sees plain HTTP. Honour X-Forwarded-Proto/For so Secure cookies, antiforgery and redirects see the
// real HTTPS scheme. Only loopback proxies are trusted by default; add others under
// ForwardedHeaders:KnownProxies (IP addresses).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
    {
        if (IPAddress.TryParse(proxy, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }
});

builder.Services.AddHrManagement(builder.Configuration);
builder.Services.AddMobileApi(builder.Configuration);
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(MobileApiAssemblyMarker).Assembly);

var app = builder.Build();

await app.InitializeHrManagementAsync();

// Must run first so everything after it sees the original scheme and client IP.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseMobileApiCorrelation();
app.UseAuthentication();
app.UseAuthorization();
app.UseMobileRealtimeNotifications();
app.UseAntiforgery();

app.MapGet("/", () => Results.Redirect("/kapson_home"));

// Target of UseExceptionHandler. Without it the handler returned 404 and hid the real error.
app.Map("/Error", () => Results.Content(
    "<!doctype html><title>Error</title><h1>Something went wrong</h1><p>The error has been logged. Please try again.</p>",
    "text/html",
    statusCode: StatusCodes.Status500InternalServerError));
app.MapHrManagement();
app.MapControllers();
app.MapHub<MobileRealtimeHub>("/hubs/hr");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
