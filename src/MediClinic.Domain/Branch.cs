namespace MediClinic.Domain;

public sealed class Branch : Entity
{
    private readonly List<Department> _departments = [];

    internal Branch(Guid id, Clinic clinic, string name, string address) : base(id)
    {
        Clinic = clinic;
        Name = Guard.Required(name, nameof(name));
        Address = Guard.Required(address, nameof(address));
    }

    public Clinic Clinic { get; }
    public Guid ClinicId => Clinic.Id;
    public string Name { get; }
    public string Address { get; }
    public IReadOnlyList<Department> Departments => _departments.AsReadOnly();

    public Department AddDepartment(Guid id, string name)
    {
        if (_departments.Any(department => department.Id == id))
            throw new InvalidOperationException("A department with this identifier already belongs to the branch.");
        var department = new Department(id, this, name);
        _departments.Add(department);
        return department;
    }
}
