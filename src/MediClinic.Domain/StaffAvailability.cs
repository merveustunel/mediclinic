namespace MediClinic.Domain;

public sealed class StaffAvailability : Entity
{
    // EF materialization only; public creation still enforces domain invariants.
    private StaffAvailability(Guid id) : base(id) { }

    public StaffAvailability(Guid id, Staff staff, DateTimeOffset startsAt, DateTimeOffset endsAt) : base(id)
    {
        ArgumentNullException.ThrowIfNull(staff);
        Guard.TimeRange(startsAt, endsAt);
        Staff = staff;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public Staff Staff { get; } = null!;
    public Guid StaffId => Staff.Id;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }

    public void Reschedule(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        Guard.TimeRange(startsAt, endsAt);
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public bool Covers(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        Guard.TimeRange(startsAt, endsAt);
        return startsAt >= StartsAt && endsAt <= EndsAt;
    }
}
