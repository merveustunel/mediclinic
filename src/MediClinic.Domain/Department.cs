namespace MediClinic.Domain;

public sealed class Department : Entity
{
    // EF materialization only; public creation still enforces domain invariants.
    private Department(Guid id) : base(id) { }

    private readonly List<MedicalService> _services = [];

    internal Department(Guid id, Branch branch, string name) : base(id)
    {
        Branch = branch;
        Name = Guard.Required(name, nameof(name));
    }

    public Branch Branch { get; } = null!;
    public Guid BranchId => Branch.Id;
    public string Name { get; private set; } = null!;
    public IReadOnlyList<MedicalService> Services => _services.AsReadOnly();

    public MedicalService AddService(Guid id, string name, TimeSpan duration)
    {
        if (_services.Any(service => service.Id == id))
            throw new InvalidOperationException("A service with this identifier already belongs to the department.");
        var service = new MedicalService(id, this, name, duration);
        _services.Add(service);
        return service;
    }
    public void Rename(string name) => Name = Guard.Required(name, nameof(name));
}
