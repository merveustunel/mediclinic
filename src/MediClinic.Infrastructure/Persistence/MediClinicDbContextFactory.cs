using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MediClinic.Infrastructure.Persistence;

// Migration tooling does not need to start the web host or create a database.
public sealed class MediClinicDbContextFactory : IDesignTimeDbContextFactory<MediClinicDbContext>
{
    public MediClinicDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.FirstOrDefault()
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__MediClinic")
            ?? "Data Source=src/MediClinic.Web/mediclinic.db;Foreign Keys=True";
        return new MediClinicDbContext(new DbContextOptionsBuilder<MediClinicDbContext>()
            .UseSqlite(connectionString).Options);
    }
}
