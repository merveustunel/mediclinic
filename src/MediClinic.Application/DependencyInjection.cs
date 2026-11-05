using Microsoft.Extensions.DependencyInjection;

namespace MediClinic.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register application services here when use cases are introduced.
        return services;
    }
}
