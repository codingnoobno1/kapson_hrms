using Microsoft.AspNetCore.DataProtection;
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

builder.Services.AddHrManagement(builder.Configuration);
builder.Services.AddMobileApi(builder.Configuration);
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(MobileApiAssemblyMarker).Assembly);

var app = builder.Build();

await app.InitializeHrManagementAsync();

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
app.MapHrManagement();
app.MapControllers();
app.MapHub<MobileRealtimeHub>("/hubs/hr");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
