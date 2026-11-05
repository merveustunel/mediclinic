namespace MediClinic.Domain;

public sealed class MedicalService : Entity
{
    internal MedicalService(Guid id, Department department, string name, TimeSpan duration) : base(id)
    {
        Department = department;
        Name = Guard.Required(name, nameof(name));
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Service duration must be positive.");
        Duration = duration;
    }

    public Department Department { get; }
    public Guid DepartmentId => Department.Id;
    public string Name { get; }
    public TimeSpan Duration { get; }
}
