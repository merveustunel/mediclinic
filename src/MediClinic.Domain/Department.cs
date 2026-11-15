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
    public string? NameTr { get; private set; }
    public string? NameEn { get; private set; }
    public void Translate(string? turkish, string? english)
    {
        if (turkish?.Length > 200 || english?.Length > 200) throw new ArgumentException("Translation exceeds 200 characters.");
        NameTr = string.IsNullOrWhiteSpace(turkish) ? null : turkish.Trim();
        NameEn = string.IsNullOrWhiteSpace(english) ? null : english.Trim();
    }
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
