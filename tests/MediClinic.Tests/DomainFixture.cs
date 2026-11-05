using MediClinic.Domain;

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
}
