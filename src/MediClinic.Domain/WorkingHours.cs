namespace MediClinic.Domain;

public sealed class WorkingHours : Entity
{
    private WorkingHours(Guid id) : base(id) { }
    public WorkingHours(Guid id, Guid branchId, DayOfWeek day, bool closed, TimeOnly? opens, TimeOnly? closes) : base(id)
    {
        if (branchId == Guid.Empty || !Enum.IsDefined(day)) throw new ArgumentException("Geçersiz Şube veya gün.");
        BranchId = branchId; Day = day; Update(closed, opens, closes);
    }
    public Guid BranchId { get; private set; }
    public DayOfWeek Day { get; private set; }
    public bool Closed { get; private set; }
    public TimeOnly? Opens { get; private set; }
    public TimeOnly? Closes { get; private set; }
    public void Update(bool closed, TimeOnly? opens, TimeOnly? closes)
    {
        if (!closed && (opens is null || closes is null || opens >= closes)) throw new ArgumentException("Açılış saati kapanış saatinden önce olmalıdır.");
        Closed = closed; Opens = closed ? null : opens; Closes = closed ? null : closes;
    }
}
