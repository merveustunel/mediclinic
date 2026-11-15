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
    public string Name { get; private set; } = null!;
    public string? NameTr { get; private set; }
    public string? NameEn { get; private set; }
    public void Translate(string? turkish, string? english)
    {
        if (turkish?.Length > 200 || english?.Length > 200) throw new ArgumentException("Translation exceeds 200 characters.");
        NameTr = string.IsNullOrWhiteSpace(turkish) ? null : turkish.Trim();
        NameEn = string.IsNullOrWhiteSpace(english) ? null : english.Trim();
    }
    public TimeSpan Duration { get; private set; }
    public decimal Price { get; private set; }
    public void Update(string name, int durationMinutes, decimal price)
    {
        if (durationMinutes < 1 || durationMinutes > 1440) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        if (price < 0 || price > 999999999.99m || decimal.Round(price, 2) != price) throw new ArgumentOutOfRangeException(nameof(price));
        Name = Guard.Required(name, nameof(name)); Duration = TimeSpan.FromMinutes(durationMinutes); Price = price;
    }
}
