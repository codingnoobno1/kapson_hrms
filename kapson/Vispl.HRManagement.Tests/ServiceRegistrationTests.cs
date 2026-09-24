using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.BM.Shared;
using Xunit;

namespace Vispl.HRManagement.Tests;

/// <summary>
/// Catches the failure mode that does not break the build: a service that used to have a real
/// implementation quietly falling back to the not-ready proxy, turning a working endpoint into
/// a 501. This inspects registration descriptors, so it needs no database.
/// </summary>
public sealed class ServiceRegistrationTests
{
    private static ServiceCollection BusinessServices()
    {
        var services = new ServiceCollection();
        services.AddHrManagementBusiness();
        return services;
    }

    [Theory]
    [MemberData(nameof(RealServices))]
    public void Service_has_a_real_implementation(Type serviceType)
    {
        var descriptor = BusinessServices()
            .LastOrDefault(item => item.ServiceType == serviceType);

        Assert.True(descriptor is not null, $"{serviceType.Name} is not registered at all.");

        // AddNotReady registers through a factory that builds a DispatchProxy. A real
        // registration names a concrete type, so this distinguishes the two without
        // resolving anything.
        Assert.True(
            descriptor!.ImplementationType is not null,
            $"{serviceType.Name} resolves to the not-ready proxy. Endpoints backed by it " +
            "return 501, so a working feature has regressed.");
    }

    [Fact]
    public void Not_ready_registrations_never_shadow_a_real_one()
    {
        var services = BusinessServices();

        var shadowed = ImplementedEndpoints.RealServices
            .Select(type => new
            {
                Type = type,
                Descriptors = services.Where(item => item.ServiceType == type).ToList()
            })
            .Where(entry => entry.Descriptors.Count > 1 &&
                            entry.Descriptors[^1].ImplementationType is null)
            .Select(entry => entry.Type.Name)
            .ToList();

        Assert.True(shadowed.Count == 0,
            "A not-ready registration was added after a real one: " + string.Join(", ", shadowed));
    }

    [Fact]
    public void Business_registration_is_idempotent()
    {
        // Registering twice must not flip a real service to not-ready, since AddNotReady
        // only skips interfaces it can already see.
        var services = new ServiceCollection();
        services.AddHrManagementBusiness();
        services.AddHrManagementBusiness();

        foreach (var type in ImplementedEndpoints.RealServices)
        {
            var last = services.Last(item => item.ServiceType == type);
            Assert.True(
                last.ImplementationType is not null,
                $"{type.Name} became not-ready after a second registration pass.");
        }
    }

    public static TheoryData<Type> RealServices()
    {
        var data = new TheoryData<Type>();
        foreach (var type in ImplementedEndpoints.RealServices)
        {
            data.Add(type);
        }

        return data;
    }
}
