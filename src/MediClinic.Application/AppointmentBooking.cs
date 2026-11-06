using MediClinic.Domain;

namespace MediClinic.Application;

public sealed record BookingRequest(Guid ClinicId, Guid BranchId, Guid DepartmentId, Guid DoctorId, Guid ServiceId, DateTime LocalStart);
public sealed record BookingOption(Guid ClinicId, Guid BranchId, Guid DepartmentId, Guid DoctorId, Guid ServiceId,
    string Clinic, string Branch, string Department, string Doctor, string Service, TimeSpan Duration);
public sealed record AppointmentSummary(Guid Id, string Service, string Doctor, string Branch,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, AppointmentStatus Status, string? Reason);
public sealed class BookingException(string message) : Exception(message);

public interface IAppointmentBooking
{
    Task<IReadOnlyList<BookingOption>> Options(string userId);
    Task<IReadOnlyList<DateTime>> Slots(string userId, Guid doctorId, Guid serviceId, DateOnly date);
    Task<Guid> Book(string userId, BookingRequest request);
    Task<IReadOnlyList<AppointmentSummary>> List(string userId, bool staff);
    Task<AppointmentSummary?> Find(string userId, Guid id, bool staff);
    Task Act(string userId, Guid id, string action, string? reason);
}

public static class Scheduling
{
    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    public static DateTimeOffset ToUtc(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local) || Zone.IsAmbiguousTime(local))
            throw new BookingException("Choose an unambiguous local scheduling time.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone));
    }
    public static DateTime Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone).DateTime;
}
