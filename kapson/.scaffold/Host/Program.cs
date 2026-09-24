using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Vispl.HRManagement.Composition.Shared;
using Vispl.HRManagement.UI.Components;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

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
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/", () => Results.Redirect("/kapson_home"));
app.MapHrManagement();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
