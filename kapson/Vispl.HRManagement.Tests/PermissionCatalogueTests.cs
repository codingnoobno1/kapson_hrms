using System.Reflection;
using System.Text.RegularExpressions;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.Contracts.Admin;
using Xunit;

namespace Vispl.HRManagement.Tests;

/// <summary>
/// A role can only be granted a permission the catalogue defines. So a controller that
/// requires an uncatalogued string is unreachable by every account — and it answers 403,
/// which reads as "you lack access" when the truth is "nobody can ever have this".
///
/// Thirty-three endpoints were in that state before these tests existed.
/// </summary>
public sealed class PermissionCatalogueTests
{
    private static IReadOnlyList<string> RequiredByControllers()
    {
        var assembly = typeof(MobileApiAssemblyMarker).Assembly;
        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in assembly.GetTypes())
        {
            foreach (var attribute in type.GetCustomAttributes<RequirePermissionAttribute>(true))
            {
                Add(required, attribute);
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var attribute in method.GetCustomAttributes<RequirePermissionAttribute>(true))
                {
                    Add(required, attribute);
                }
            }
        }

        return required.OrderBy(item => item, StringComparer.Ordinal).ToList();
    }

    private static void Add(HashSet<string> set, RequirePermissionAttribute attribute)
    {
        // The attribute stores the value as a policy name with a fixed prefix.
        var policy = attribute.Policy ?? string.Empty;
        var prefix = MobileAuthenticationDefaults.PermissionPolicyPrefix;

        if (policy.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            set.Add(policy[prefix.Length..]);
        }
    }

    [Fact]
    public void Every_permission_a_controller_requires_can_be_granted()
    {
        var catalogued = HrPermissionCatalog.All
            .Select(permission => permission.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphans = RequiredByControllers()
            .Where(permission => !catalogued.Contains(permission))
            .ToList();

        Assert.True(
            orphans.Count == 0,
            "These permissions are required by a route but absent from HrPermissionCatalog, so " +
            "no role can grant them and the routes answer 403 to everyone: " +
            string.Join(", ", orphans));
    }

    [Fact]
    public void Controllers_actually_require_permissions()
    {
        // Guards the test above from passing because reflection found nothing.
        Assert.True(RequiredByControllers().Count > 30);
    }

    [Fact]
    public void Catalogue_keys_are_unique_and_well_formed()
    {
        var duplicates = HrPermissionCatalog.All
            .GroupBy(permission => permission.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, "Duplicate permission keys: " + string.Join(", ", duplicates));

        var malformed = HrPermissionCatalog.All
            .Where(permission => !Regex.IsMatch(permission.Key, "^[A-Z][A-Z0-9_]*(\\.[A-Z][A-Z0-9_]*)+$"))
            .Select(permission => permission.Key)
            .ToList();

        Assert.True(malformed.Count == 0, "Malformed permission keys: " + string.Join(", ", malformed));
    }

    [Fact]
    public void Every_catalogued_permission_names_a_group_and_a_description()
    {
        // These are what a tenant administrator reads in the role builder.
        var incomplete = HrPermissionCatalog.All
            .Where(permission =>
                string.IsNullOrWhiteSpace(permission.Group) ||
                string.IsNullOrWhiteSpace(permission.Name) ||
                string.IsNullOrWhiteSpace(permission.Description))
            .Select(permission => permission.Key)
            .ToList();

        Assert.True(incomplete.Count == 0, "Incomplete catalogue entries: " + string.Join(", ", incomplete));
    }
}
