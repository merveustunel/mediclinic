using Microsoft.Extensions.DependencyInjection;

namespace MediClinic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Register infrastructure implementations here when persistence is introduced.
        return services;
    }
}
