using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.BM.ITF.Mobile;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Stands in for a mobile business service whose module has no implementation yet.
/// Every member throws <see cref="MobileFeatureNotReadyException"/>, which the API layer
/// maps to a 501 response naming the missing components.
///
/// This exists so the HTTP contract can be complete and generated into the mobile client
/// before the owning module is built. Replacing it is a one-line registration change:
/// delete the AddNotReady call and register the real implementation instead.
/// </summary>
// Not sealed: DispatchProxy generates a subclass of this type at runtime.
public class NotReadyProxy : DispatchProxy
{
    internal string Module { get; set; } = "Module";

    internal string[] RequiredComponents { get; set; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new MobileFeatureNotReadyException(Module, RequiredComponents);
}

public static class NotReadyServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TService"/> as a not-yet-implemented module.
    /// Does nothing if a real implementation is already registered, so module teams can
    /// land their service without editing this file.
    /// </summary>
    public static IServiceCollection AddNotReady<TService>(
        this IServiceCollection services,
        string module,
        params string[] requiredComponents)
        where TService : class
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(TService)))
        {
            return services;
        }

        services.AddSingleton(_ => Create<TService>(module, requiredComponents));
        return services;
    }

    internal static TService Create<TService>(string module, string[] requiredComponents)
        where TService : class
    {
        var proxy = DispatchProxy.Create<TService, NotReadyProxy>();
        var notReady = (NotReadyProxy)(object)proxy;
        notReady.Module = module;
        notReady.RequiredComponents = requiredComponents;
        return proxy;
    }
}
