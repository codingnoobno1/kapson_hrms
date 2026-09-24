using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Vispl.HRManagement.Api.Shared;

namespace Vispl.HRManagement.Tests;

public sealed record ApiRoute(string Method, string Template, string Controller, string Action)
{
    public override string ToString() => $"{Method} /{Template}";
}

/// <summary>
/// Reads the routing attributes off the API assembly. Reflection rather than a booted host,
/// so the surface can be asserted without a database or a listening port.
/// </summary>
public static class ApiSurface
{
    private static readonly Lazy<IReadOnlyList<ApiRoute>> Routes = new(Discover);

    public static IReadOnlyList<ApiRoute> All => Routes.Value;

    public static bool Has(string method, string template) =>
        All.Any(route =>
            string.Equals(route.Method, method, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(route.Template, Normalise(template), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Drops route constraints and parameter names so a test states the shape of a route
    /// rather than its exact parameter spelling: {id:long} and {applicationId} both become {}.
    /// </summary>
    public static string Normalise(string template)
    {
        var trimmed = template.Trim('/');
        var segments = trimmed.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].StartsWith('{'))
            {
                segments[i] = "{}";
            }
        }

        return string.Join('/', segments);
    }

    private static IReadOnlyList<ApiRoute> Discover()
    {
        var assembly = typeof(MobileApiAssemblyMarker).Assembly;
        var results = new List<ApiRoute>();

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(ControllerBase).IsAssignableFrom(type))
            {
                continue;
            }

            var prefix = type.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var attribute in method.GetCustomAttributes().OfType<HttpMethodAttribute>())
                {
                    var suffix = attribute.Template ?? string.Empty;
                    var template = string.IsNullOrEmpty(suffix)
                        ? prefix
                        : $"{prefix.TrimEnd('/')}/{suffix.TrimStart('/')}";

                    foreach (var verb in attribute.HttpMethods)
                    {
                        results.Add(new ApiRoute(verb, Normalise(template), type.Name, method.Name));
                    }
                }
            }
        }

        return results;
    }
}
