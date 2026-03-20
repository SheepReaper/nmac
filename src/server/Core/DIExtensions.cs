using System.Reflection;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NMAC.Core;

public static class DIExtensions
{
    public static IServiceCollection AddAssemblyEndpoints(this IServiceCollection services) =>
        services.AddAssemblyEndpoints(Assembly.GetExecutingAssembly());

    public static IServiceCollection AddAssemblyEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var serviceDescriptors = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type));

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    public static IEndpointRouteBuilder MapRegisteredEndpoints(this IEndpointRouteBuilder builder)
    {
        foreach (var endpoint in builder.ServiceProvider.GetRequiredService<IEnumerable<IEndpoint>>())
            endpoint.MapEndpoint(builder);

        return builder;
    }

    public static IServiceCollection AddAssemblyUseCases(this IServiceCollection services) =>
        services.AddAssemblyUseCases(Assembly.GetExecutingAssembly());

    public static IServiceCollection AddAssemblyUseCases(this IServiceCollection services, Assembly assembly)
    {
        var serviceDescriptors = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                type.IsAssignableTo(typeof(IUseCase)))
            .Select(type => ServiceDescriptor.Scoped(type, type));

        foreach (var descriptor in serviceDescriptors)
            services.TryAdd(descriptor);

        return services;
    }
}