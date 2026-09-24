using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Identity.Admin;

namespace Vispl.HRManagement.Composition.Admin;

public static class AdminModule
{
    public static IServiceCollection AddAdminModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAdminIdentity(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/media/{id:long}", async (
            long id,
            IMediaLibraryService mediaLibrary,
            CancellationToken cancellationToken) =>
        {
            var file = await mediaLibrary.OpenReadAsync(id, cancellationToken);
            return file is null
                ? Results.NotFound()
                : Results.File(file.Content, file.ContentType, enableRangeProcessing: true);
        }).AllowAnonymous();

        endpoints.MapGroup("/admin")
            .MapGet("/health", () => Results.Ok(new { module = "Admin", status = "Healthy" }))
            .AllowAnonymous();
        return endpoints;
    }
}
