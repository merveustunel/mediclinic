using MediClinic.Domain;
using MediClinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Tests;

internal sealed class DomainFixture
{
    internal Clinic Clinic { get; } = new(Guid.NewGuid(), "Example Clinic");
    internal Department Department { get; }
    internal Doctor Doctor { get; }
    internal Patient Patient { get; } = new(Guid.NewGuid(), "Sample", "Patient", new DateOnly(2000, 1, 1));
    internal MedicalService Service { get; }
    internal DateTimeOffset Start { get; } = new(2030, 1, 2, 9, 0, 0, TimeSpan.Zero);

    internal DomainFixture()
    {
        Department = Clinic.AddBranch(Guid.NewGuid(), "Central", "Example address")
            .AddDepartment(Guid.NewGuid(), "General medicine");
        Doctor = new Doctor(Guid.NewGuid(), "Sample", "Doctor", Department, "General medicine");
        Service = Department.AddService(Guid.NewGuid(), "Consultation", TimeSpan.FromMinutes(30));
    }

    internal Appointment Appointment() => new(Guid.NewGuid(), Patient, Doctor, Service, Start);

    // Seed the historical schema without using today's EF model/columns.
    internal async Task SeedLegacy(MediClinicDbContext db, params Appointment[] appointments)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Clinics (Id, Name) VALUES ({Clinic.Id}, {Clinic.Name})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Branches (Id, ClinicId, Name, Address) VALUES ({Department.BranchId}, {Clinic.Id}, {Department.Branch.Name}, {Department.Branch.Address})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Departments (Id, BranchId, Name) VALUES ({Department.Id}, {Department.BranchId}, {Department.Name})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO MedicalServices (Id, DepartmentId, Name, Duration) VALUES ({Service.Id}, {Department.Id}, {Service.Name}, {Service.Duration.Ticks})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO People (Id, FirstName, LastName, PersonType, DateOfBirth) VALUES ({Patient.Id}, {Patient.FirstName}, {Patient.LastName}, {"Patient"}, {Patient.DateOfBirth})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO People (Id, FirstName, LastName, PersonType, DepartmentId, JobTitle, Specialty) VALUES ({Doctor.Id}, {Doctor.FirstName}, {Doctor.LastName}, {"Doctor"}, {Department.Id}, {"Doctor"}, {Doctor.Specialty})");
        foreach (var appointment in appointments)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Appointments (Id, PatientId, DoctorId, ServiceId, StartsAt, EndsAt, Status) VALUES ({appointment.Id}, {Patient.Id}, {Doctor.Id}, {Service.Id}, {appointment.StartsAt.UtcTicks}, {appointment.EndsAt.UtcTicks}, {"Pending"})");
    }
}
