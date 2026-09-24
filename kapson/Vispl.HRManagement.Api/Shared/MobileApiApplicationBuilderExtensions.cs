using Microsoft.AspNetCore.Builder;

namespace Vispl.HRManagement.Api.Shared;

public static class MobileApiApplicationBuilderExtensions
{
    public static IApplicationBuilder UseMobileApiCorrelation(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api/mobile"))
            {
                await next();
                return;
            }

            var supplied = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 100 &&
                supplied.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.'))
            {
                context.TraceIdentifier = supplied;
            }

            context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
            await next();
        });
}
