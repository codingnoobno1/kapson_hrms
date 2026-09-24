using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Vispl.HRManagement.Api.Shared;

public sealed record MobileDataChanged(string Topic, string Path, DateTimeOffset ChangedUtc);

[Authorize(AuthenticationSchemes = MobileAuthenticationDefaults.BearerScheme)]
public sealed class MobileRealtimeHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenant = Context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrWhiteSpace(tenant))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenant));
        }

        var user = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(user))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{user}");
        }

        await base.OnConnectedAsync();
    }

    internal static string TenantGroup(string tenant) => $"tenant:{tenant.Trim().ToLowerInvariant()}";
}

public static class MobileRealtimeApplicationBuilderExtensions
{
    /// <summary>Publishes a compact invalidation only after a successful API mutation.</summary>
    public static IApplicationBuilder UseMobileRealtimeNotifications(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            await next();

            if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) ||
                context.Response.StatusCode is < 200 or >= 300 ||
                !context.Request.Path.StartsWithSegments("/api"))
            {
                return;
            }

            var tenant = context.User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrWhiteSpace(tenant) &&
                context.Request.Path.StartsWithSegments("/api/public/v1/openings"))
            {
                tenant = context.Request.Query["tenant"].FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(tenant)) return;

            var path = context.Request.Path.Value ?? string.Empty;
            var topic = TopicFor(path);
            var hub = context.RequestServices.GetRequiredService<IHubContext<MobileRealtimeHub>>();
            await hub.Clients.Group(MobileRealtimeHub.TenantGroup(tenant))
                .SendAsync("DataChanged", new MobileDataChanged(topic, path, DateTimeOffset.UtcNow));
        });

    private static string TopicFor(string path)
    {
        var normalized = path.ToLowerInvariant();
        if (normalized.Contains("application") || normalized.Contains("opening") || normalized.Contains("interview")) return "recruitment";
        if (normalized.Contains("onboarding") || normalized.Contains("mobile-access")) return "onboarding";
        if (normalized.Contains("attendance") || normalized.Contains("punch")) return "attendance";
        if (normalized.Contains("leave")) return "leave";
        if (normalized.Contains("notification")) return "notifications";
        if (normalized.Contains("report") || normalized.Contains("export")) return "reports";
        return "workspace";
    }
}
