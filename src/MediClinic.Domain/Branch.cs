namespace MediClinic.Domain;

public sealed class Branch : Entity
{
    // EF materialization only; public creation still enforces domain invariants.
    private Branch(Guid id) : base(id) { }

    private readonly List<Department> _departments = [];

    internal Branch(Guid id, Clinic clinic, string name, string address) : base(id)
    {
        Clinic = clinic;
        Name = Guard.Required(name, nameof(name));
        Address = Guard.Required(address, nameof(address));
    }

    public Clinic Clinic { get; } = null!;
    public Guid ClinicId => Clinic.Id;
    public string Name { get; private set; } = null!;
    public string Address { get; private set; } = null!;
    public IReadOnlyList<Department> Departments => _departments.AsReadOnly();

    public Department AddDepartment(Guid id, string name)
    {
        if (_departments.Any(department => department.Id == id))
            throw new InvalidOperationException("A department with this identifier already belongs to the branch.");
        var department = new Department(id, this, name);
        _departments.Add(department);
        return department;
    }
    public void Update(string name, string address) { Name = Guard.Required(name, nameof(name)); Address = Guard.Required(address, nameof(address)); }
}
