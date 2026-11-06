using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MediClinic.Infrastructure.Persistence;

namespace MediClinic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDbContext<MediClinicDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<MediClinic.Application.IAppointmentBooking, AppointmentBooking>();
        return services;
    }
}
