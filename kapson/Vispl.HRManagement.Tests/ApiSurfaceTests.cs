using Xunit;

namespace Vispl.HRManagement.Tests;

/// <summary>
/// The API exists and keeps its shape. These need no database and no listening port.
/// </summary>
public sealed class ApiSurfaceTests
{
    [Fact]
    public void Api_assembly_exposes_controllers()
    {
        Assert.NotEmpty(ApiSurface.All);

        // A wrong assembly or a broken reflection helper would otherwise pass everything
        // below by finding nothing to contradict.
        Assert.True(
            ApiSurface.All.Count > 100,
            $"Expected a substantial API surface, found {ApiSurface.All.Count} routes.");
    }

    [Theory]
    [MemberData(nameof(ImplementedRoutes))]
    public void Implemented_route_still_exists(string method, string template)
    {
        Assert.True(
            ApiSurface.Has(method, template),
            $"{method} /{template} is gone. It backed working behaviour, so removing or " +
            "renaming it is a breaking change to the mobile app.");
    }

    [Fact]
    public void No_route_is_declared_twice_for_the_same_verb()
    {
        // Two controllers claiming one route is how a working endpoint gets shadowed by a
        // not-yet-implemented one.
        var duplicates = ApiSurface.All
            .GroupBy(route => $"{route.Method} /{route.Template}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} declared by " +
                             string.Join(", ", group.Select(item => $"{item.Controller}.{item.Action}")))
            .ToList();

        Assert.True(duplicates.Count == 0, string.Join(Environment.NewLine, duplicates));
    }

    [Fact]
    public void Public_and_candidate_routes_are_versioned_and_prefixed()
    {
        var stray = ApiSurface.All
            .Where(route => !route.Template.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            .Select(route => $"{route} ({route.Controller})")
            .ToList();

        Assert.True(stray.Count == 0,
            "Every controller route must sit under api/: " + string.Join(", ", stray));
    }

    public static TheoryData<string, string> ImplementedRoutes()
    {
        var data = new TheoryData<string, string>();
        foreach (var (method, template) in ImplementedEndpoints.Routes)
        {
            data.Add(method, template);
        }

        return data;
    }
}
