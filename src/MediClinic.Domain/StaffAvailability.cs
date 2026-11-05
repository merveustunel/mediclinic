namespace MediClinic.Domain;

public sealed class StaffAvailability : Entity
{
    public StaffAvailability(Guid id, Staff staff, DateTimeOffset startsAt, DateTimeOffset endsAt) : base(id)
    {
        ArgumentNullException.ThrowIfNull(staff);
        Guard.TimeRange(startsAt, endsAt);
        Staff = staff;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public Staff Staff { get; }
    public Guid StaffId => Staff.Id;
    public DateTimeOffset StartsAt { get; }
    public DateTimeOffset EndsAt { get; }

    public bool Covers(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        Guard.TimeRange(startsAt, endsAt);
        return startsAt >= StartsAt && endsAt <= EndsAt;
    }
}
