using MediClinic.Domain;

namespace MediClinic.Application;

public sealed record StaffDashboardAppointment(Guid Id, string Patient, string Doctor, string Service,
    string Branch, DateTimeOffset StartsAt, AppointmentStatus Status);
public sealed record StaffDashboardData(int TodayCount, int PendingCount, int ConfirmedCount, int CompletedCount,
    IReadOnlyList<StaffDashboardAppointment> Pending, IReadOnlyList<StaffDashboardAppointment> Today);
