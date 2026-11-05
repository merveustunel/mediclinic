namespace MediClinic.Domain;

public sealed class MedicalService : Entity
{
    // EF materialization only; public creation still enforces domain invariants.
    private MedicalService(Guid id) : base(id) { }

    internal MedicalService(Guid id, Department department, string name, TimeSpan duration) : base(id)
    {
        Department = department;
        Name = Guard.Required(name, nameof(name));
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Service duration must be positive.");
        Duration = duration;
    }

    public Department Department { get; } = null!;
    public Guid DepartmentId => Department.Id;
    public string Name { get; } = null!;
    public TimeSpan Duration { get; }
}
