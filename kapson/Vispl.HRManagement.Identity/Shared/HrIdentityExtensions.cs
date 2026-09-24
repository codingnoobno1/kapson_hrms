using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.Identity.Admin;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.Identity.Shared;

public static class HrIdentityExtensions
{
    public const string AdminAuthenticatedPolicy = "AdminAuthenticated";
    public const string AdminPortalPolicy = "AdminPortal";

    /// <summary>Signed in to the HR portal, before the forced password change is satisfied.</summary>
    public const string HrAuthenticatedPolicy = "HrAuthenticated";

    /// <summary>Signed in to the HR portal and cleared to use it.</summary>
    public const string HrPortalPolicy = "HrPortal";

    public static IServiceCollection AddHrIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HrManagement")
            ?? throw new InvalidOperationException("ConnectionStrings:HrManagement is required for Identity.");

        services.AddDbContext<AdminIdentityDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
                sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            }));

        services
            .AddIdentityCore<AdminUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<HrRole>()
            .AddEntityFrameworkStores<AdminIdentityDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserClaimsPrincipalFactory<AdminUser>, AdminClaimsPrincipalFactory>();
        services.AddScoped<AdminIdentityInitializer>();
        services.AddScoped<DevelopmentHrAccountSeeder>();
        services.AddScoped<IAccessAccountService, AccessAccountService>();
        services.AddScoped<IAccessRoleService, AccessRoleService>();
        services.AddSingleton<MobileSigningKeyProvider>();
        services.AddScoped<IMobileTokenService, MobileTokenService>();
        services.AddScoped<Candidate.ICandidateActivationService, Candidate.CandidateActivationService>();
        services.AddScoped<BM.ITF.Candidate.ICandidateInvitation, Candidate.CandidateInvitation>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Shared.IPermissionAudience, PermissionAudience>();
        services.AddCascadingAuthenticationState();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = "Vispl.HR.Admin.Auth.v1";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.LoginPath = "/admin";
                options.AccessDeniedPath = "/admin/access-denied";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.Events.OnValidatePrincipal = async context =>
                {
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AdminUser>>();
                    var user = string.IsNullOrWhiteSpace(userId) ? null : await userManager.FindByIdAsync(userId);

                    // Deactivating an account ends its session at the next request. The portal
                    // the account belongs to is not checked here — one cookie serves the admin
                    // and HR portals, and which one a session may enter is decided by the
                    // authorization policy on the page, not by tearing the session down.
                    if (user is null || !user.IsActive)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                        return;
                    }

                    // Switching the portal off ends sessions already open, at their next
                    // request. A toggle that only stopped new sign-ins would leave whoever was
                    // already inside working for the rest of the day.
                    if (user.Portal == AccountPortal.Hr)
                    {
                        var tenants = context.HttpContext.RequestServices
                            .GetRequiredService<Vispl.HRManagement.BM.ITF.Admin.ITenantService>();

                        if (!await IsPortalEnabledAsync(tenants, user.TenantKey))
                        {
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                            return;
                        }
                    }

                    // Refresh when the cookie's view of the account has gone stale: a moved
                    // portal, a completed password change, or a permission granted or revoked
                    // since sign-in. Permissions are compared against a freshly built principal,
                    // so a revoked grant stops working at the next request, not the next sign-in.
                    var principalFactory = context.HttpContext.RequestServices
                        .GetRequiredService<IUserClaimsPrincipalFactory<AdminUser>>();
                    var current = await principalFactory.CreateAsync(user);

                    var cookiePermissions = context.Principal!.FindAll("permission").Select(claim => claim.Value);
                    var currentPermissions = current.FindAll("permission").Select(claim => claim.Value);

                    if (!context.Principal.HasClaim("account_portal", user.Portal.ToString()) ||
                        !context.Principal.HasClaim("account_active", "true") ||
                        context.Principal.HasClaim("must_change_password", "true") != user.MustChangePassword ||
                        !cookiePermissions.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(currentPermissions))
                    {
                        context.ReplacePrincipal(current);
                        context.ShouldRenew = true;
                    }
                };
                options.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    // An HR user who deep-links into the portal is sent to the HR sign-in page,
                    // not to the platform one they could never sign in to.
                    if (context.Request.Path.StartsWithSegments("/hr"))
                    {
                        var returnUrl = Uri.EscapeDataString(
                            context.Request.Path + context.Request.QueryString);

                        // A deep link like /hr/kapson/dashboard sends the caller to that
                        // client's own sign-in page, not to the general one.
                        var segments = context.Request.Path.Value?.Split(
                            '/', StringSplitOptions.RemoveEmptyEntries) ?? [];

                        var client = segments.Length >= 2 ? Sanitise(segments[1]) : null;

                        // Every route registered as "/hr/{page}" (no tenant segment) must be
                        // listed here, or an unauthenticated deep link to it is misread as a
                        // tenant key and redirected back to itself — an infinite loop, since the
                        // page requires the same sign-in this redirect was meant to reach. This
                        // list has to be kept in step with HrNav.razor and every @page "/hr/..."
                        // route that does not also declare a "/hr/{TenantKey}/..." sibling for
                        // itself further down the path (e.g. the masters catalogue's own
                        // "/hr/settings/masters/{MasterKey}" still resolves correctly here,
                        // because segments[1] is still "settings").
                        var reserved = client is "dashboard" or "change-password" or "access-denied"
                            or "people" or "recruitment" or "attendance" or "leave" or "settings"
                            or "coming-soon" or "payroll" or "employees" or "reports";
                        var target = client is null || reserved ? "/hr" : $"/hr/{client}";

                        context.Response.Redirect($"{target}?returnUrl={returnUrl}");
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }

                    if (context.Request.Path.StartsWithSegments("/hr"))
                    {
                        context.Response.Redirect("/hr/access-denied");
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminAuthenticatedPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AdminIdentityInitializer.AdministratorRole)
                .RequireClaim("account_portal", AccountPortal.Platform.ToString())
                .RequireClaim("account_active", "true"));
            options.AddPolicy(AdminPortalPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AdminIdentityInitializer.AdministratorRole)
                .RequireClaim("account_portal", AccountPortal.Platform.ToString())
                .RequireClaim("account_active", "true")
                .RequireClaim("must_change_password", "false"));

            // The HR portal is gated on the portal the account belongs to, not on a role. A
            // platform administrator therefore cannot walk into it, and an HR account cannot
            // walk into /admin — the separation the two policies exist to hold.
            options.AddPolicy(HrAuthenticatedPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("account_portal", AccountPortal.Hr.ToString())
                .RequireClaim("account_active", "true"));

            options.AddPolicy(HrPortalPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("account_portal", AccountPortal.Hr.ToString())
                .RequireClaim("account_active", "true")
                .RequireClaim("must_change_password", "false"));
        });

        return services;
    }

    public static IEndpointRouteBuilder MapHrIdentityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/identity/admin");

        group.MapPost("/login", async (
            [FromForm] string userName,
            [FromForm] string password,
            [FromForm] string? returnUrl,
            SignInManager<AdminUser> signInManager,
            UserManager<AdminUser> userManager) =>
        {
            var user = await userManager.FindByNameAsync(userName.Trim());
            if (user is null)
            {
                return Results.Redirect("/admin?error=invalid");
            }

            if (!user.IsActive || user.Portal != AccountPortal.Platform)
            {
                return Results.Redirect("/admin?error=invalid");
            }

            var result = await signInManager.PasswordSignInAsync(user, password, true, true);
            if (!result.Succeeded)
            {
                return Results.Redirect(result.IsLockedOut ? "/admin?error=locked" : "/admin?error=invalid");
            }

            user.PreviousLoginUtc = user.LastLoginUtc;
            user.LastLoginUtc = DateTimeOffset.UtcNow;
            user.UpdatedUtc = DateTimeOffset.UtcNow;
            await userManager.UpdateAsync(user);

            if (user.MustChangePassword)
            {
                return Results.Redirect("/admin/change-password");
            }

            return Results.Redirect(GetSafeReturnUrl(returnUrl));
        }).AllowAnonymous();

        group.MapPost("/change-password", async (
            HttpContext httpContext,
            [FromForm] string currentPassword,
            [FromForm] string newPassword,
            [FromForm] string confirmPassword,
            UserManager<AdminUser> userManager,
            SignInManager<AdminUser> signInManager) =>
        {
            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                return Results.Redirect("/admin/change-password?error=mismatch");
            }

            var user = await userManager.GetUserAsync(httpContext.User);
            if (user is null)
            {
                return Results.Redirect("/admin");
            }

            var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!result.Succeeded)
            {
                var message = Uri.EscapeDataString(string.Join(" ", result.Errors.Select(error => error.Description)));
                return Results.Redirect($"/admin/change-password?error={message}");
            }

            user.MustChangePassword = false;
            user.PasswordChangedUtc = DateTimeOffset.UtcNow;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
            }

            await signInManager.RefreshSignInAsync(user);
            return Results.Redirect("/admin/dashboard");
        }).RequireAuthorization(AdminAuthenticatedPolicy);

        // Any signed-in session may end itself. The cookie is shared across portals, so an HR
        // session that lands on /admin needs this to get back to the admin sign-in form.
        group.MapPost("/logout", async (SignInManager<AdminUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect("/admin");
        }).RequireAuthorization();

        MapPortalEndpoints(
            endpoints,
            prefix: "/identity/hr",
            portal: AccountPortal.Hr,
            loginPath: "/hr",
            homePath: "/hr/dashboard",
            changePasswordPath: "/hr/change-password",
            authenticatedPolicy: HrAuthenticatedPolicy);

        endpoints.MapGet("/identity/health", () =>
            Results.Ok(new { module = "Identity", status = "Healthy" }))
            .AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// Sign-in, password change and sign-out for one portal.
    ///
    /// The admin portal keeps its own hand-written group for now; this builder serves the HR
    /// portal and any portal added later, so a second portal cannot quietly acquire different
    /// lockout or enumeration behaviour from the first.
    /// </summary>
    private static void MapPortalEndpoints(
        IEndpointRouteBuilder endpoints,
        string prefix,
        AccountPortal portal,
        string loginPath,
        string homePath,
        string changePasswordPath,
        string authenticatedPolicy)
    {
        var group = endpoints.MapGroup(prefix);

        group.MapPost("/login", async (
            [FromForm] string userName,
            [FromForm] string password,
            [FromForm] string? returnUrl,
            [FromForm] string? tenantKey,
            SignInManager<AdminUser> signInManager,
            UserManager<AdminUser> userManager,
            Vispl.HRManagement.BM.ITF.Admin.ITenantService tenants) =>
        {
            // When the address names a client, every page in this flow stays inside it.
            var client = Sanitise(tenantKey);
            var basePath = client is null ? loginPath : $"{loginPath}/{client}";

            var user = await userManager.FindByNameAsync(userName.Trim());

            // Every rejection answers the same way. A different message for "no such user"
            // would let anyone test which usernames exist.
            if (user is null || !user.IsActive || user.Portal != portal)
            {
                return Results.Redirect($"{basePath}?error=invalid");
            }

            // An account belonging to another client cannot sign in here, even with the right
            // password. Answered identically to a wrong password, so the page cannot be used to
            // discover which client an account belongs to.
            if (client is not null &&
                !string.Equals(user.TenantKey, client, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Redirect($"{basePath}?error=invalid");
            }

            // A client whose portal is switched off is told so plainly. This is not account
            // enumeration: the caller has already proved nothing, and the answer is about the
            // client rather than about whether this particular username exists — every account
            // of that client gets the identical response.
            if (portal == AccountPortal.Hr && !await IsPortalEnabledAsync(tenants, user.TenantKey))
            {
                return Results.Redirect($"{basePath}?error=disabled");
            }

            var result = await signInManager.PasswordSignInAsync(user, password, true, true);
            if (!result.Succeeded)
            {
                return Results.Redirect(
                    result.IsLockedOut ? $"{basePath}?error=locked" : $"{basePath}?error=invalid");
            }

            user.PreviousLoginUtc = user.LastLoginUtc;
            user.LastLoginUtc = DateTimeOffset.UtcNow;
            user.UpdatedUtc = DateTimeOffset.UtcNow;
            await userManager.UpdateAsync(user);

            // From here the client comes from the account, not from the address: a signed-in
            // user belongs to exactly one client and that is the portal they land in.
            var scope = portal == AccountPortal.Hr ? Sanitise(user.TenantKey) : null;

            if (user.MustChangePassword)
            {
                return Results.Redirect(
                    scope is null ? changePasswordPath : $"{loginPath}/{scope}/change-password");
            }

            var home = scope is null ? homePath : $"{loginPath}/{scope}/dashboard";
            return Results.Redirect(SafeReturnUrl(returnUrl, home));
        }).AllowAnonymous().DisableAntiforgery();

        group.MapPost("/change-password", async (
            HttpContext httpContext,
            [FromForm] string currentPassword,
            [FromForm] string newPassword,
            [FromForm] string confirmPassword,
            UserManager<AdminUser> userManager,
            SignInManager<AdminUser> signInManager) =>
        {
            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                return Results.Redirect($"{changePasswordPath}?error=mismatch");
            }

            var user = await userManager.GetUserAsync(httpContext.User);
            if (user is null)
            {
                return Results.Redirect(loginPath);
            }

            var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!result.Succeeded)
            {
                var message = Uri.EscapeDataString(
                    string.Join(" ", result.Errors.Select(error => error.Description)));
                return Results.Redirect($"{changePasswordPath}?error={message}");
            }

            user.MustChangePassword = false;
            user.PasswordChangedUtc = DateTimeOffset.UtcNow;
            await userManager.UpdateAsync(user);

            // Re-issued so must_change_password stops gating the portal in the same request.
            await signInManager.RefreshSignInAsync(user);

            var scope = portal == AccountPortal.Hr ? Sanitise(user.TenantKey) : null;
            return Results.Redirect(scope is null ? homePath : $"{loginPath}/{scope}/dashboard");
        }).RequireAuthorization(authenticatedPolicy);

        group.MapPost("/logout", async (SignInManager<AdminUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect(loginPath);
        }).RequireAuthorization(authenticatedPolicy);
    }

    /// <summary>
    /// Only a same-site path is followed after sign-in. A protocol-relative or absolute URL
    /// would let a crafted link bounce a freshly authenticated user off-site.
    /// </summary>
    /// <summary>
    /// A client key is only accepted in the shape the platform issues them. Anything else is
    /// treated as absent, so a crafted path segment cannot be reflected back into a redirect.
    /// </summary>
    private static string? Sanitise(string? tenantKey)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return null;
        }

        var trimmed = tenantKey.Trim().ToLowerInvariant();

        return System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^[a-z0-9-]{1,80}$")
            ? trimmed
            : null;
    }

    /// <summary>
    /// Whether a client may use the HR portal. An unknown client is refused: an account whose
    /// tenant has been removed should not keep working.
    /// </summary>
    private static async Task<bool> IsPortalEnabledAsync(
        Vispl.HRManagement.BM.ITF.Admin.ITenantService tenants, string? tenantKey)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return false;
        }

        var tenant = await tenants.GetByKeyAsync(tenantKey);
        return tenant is { IsActive: true, HrPortalEnabled: true };
    }

    private static string SafeReturnUrl(string? returnUrl, string fallback) =>
        !string.IsNullOrWhiteSpace(returnUrl) &&
        returnUrl.StartsWith("/", StringComparison.Ordinal) &&
        !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : fallback;

    private static string GetSafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) &&
        returnUrl.StartsWith("/", StringComparison.Ordinal) &&
        !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : "/admin/dashboard";
}
