namespace MediClinic.Domain;

public sealed class Doctor : Staff
{
    // EF materialization only.
    private Doctor(Guid id) : base(id) { }

    public Doctor(Guid id, string firstName, string lastName, Department department, string specialty)
        : base(id, firstName, lastName, department, "Doctor")
    {
        Specialty = Guard.Required(specialty, nameof(specialty));
    }

    public string Specialty { get; private set; } = null!;
    private readonly List<MedicalService> _services = [];
    public IReadOnlyCollection<MedicalService> Services => _services.AsReadOnly();
    public void UpdateSpecialty(string specialty) => Specialty = Guard.Required(specialty, nameof(specialty));
    public void AssignServices(IEnumerable<MedicalService> services)
    {
        var selected = services.DistinctBy(s => s.Id).ToList();
        if (selected.Any(s => !CanProvide(s))) throw new ArgumentException("Services must belong to the doctor's department.");
        _services.Clear();
        _services.AddRange(selected);
    }
    public bool Offers(MedicalService service) => CanProvide(service) && _services.Any(s => s.Id == service.Id);
    public override string Role => "Doctor";

    public bool CanProvide(MedicalService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        return DepartmentId == service.DepartmentId;
    }
}
