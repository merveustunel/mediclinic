namespace MediClinic.Domain;

public sealed class Doctor : Staff
{
    public Doctor(Guid id, string firstName, string lastName, Department department, string specialty)
        : base(id, firstName, lastName, department, "Doctor")
    {
        Specialty = Guard.Required(specialty, nameof(specialty));
    }

    public string Specialty { get; }
    public override string Role => "Doctor";

    public bool CanProvide(MedicalService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        return DepartmentId == service.DepartmentId;
    }
}
