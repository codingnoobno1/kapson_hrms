using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Vispl.HRManagement.Identity.Admin;

namespace Vispl.HRManagement.Api.Shared;

public static class MobileApiServiceCollectionExtensions
{
    public static IServiceCollection AddMobileApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MobileTokenOptions>()
            .Bind(configuration.GetSection(MobileTokenOptions.SectionName));

        services.AddOptions<MobileApiOptions>()
            .Bind(configuration.GetSection(MobileApiOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiVersion), "MobileApi:ApiVersion is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "MobileApi:Audience is required.")
            .ValidateOnStart();

        var mobileOptions = configuration
            .GetSection(MobileApiOptions.SectionName)
            .Get<MobileApiOptions>() ?? new MobileApiOptions();

        services.AddAuthentication()
            .AddJwtBearer(MobileAuthenticationDefaults.BearerScheme, options =>
            {
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = mobileOptions.RequireHttpsMetadata;
                options.Audience = mobileOptions.Audience;
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"].FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs/hr"))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };

                if (!string.IsNullOrWhiteSpace(mobileOptions.Authority))
                {
                    options.Authority = mobileOptions.Authority;
                }
                else
                {
                    // No external authority, so this host issues and validates its own tokens.
                    // The key is resolved by MobileSigningKeyProvider, which fails closed
                    // outside Development rather than inventing a key.
                    var signingKey = services
                        .BuildServiceProvider()
                        .GetRequiredService<MobileSigningKeyProvider>()
                        .Key;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = configuration["MobileApi:Issuer"] ?? "vispl.hr",
                        ValidateAudience = true,
                        ValidAudience = mobileOptions.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(signingKey),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                }
            });

        services.AddSingleton<IAuthorizationHandler, MobilePermissionHandler>();
        services.AddScoped<MobileApiExceptionFilter>();
        services.Configure<MvcOptions>(mvc => mvc.Filters.Add<MobileApiExceptionFilter>());
        services.AddSingleton<IAuthorizationPolicyProvider, MobilePermissionPolicyProvider>();
        services.AddProblemDetails();
        services.AddSignalR();
        return services;
    }
}

public sealed class MobileApiAssemblyMarker;
