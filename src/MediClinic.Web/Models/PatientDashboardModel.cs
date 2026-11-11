using MediClinic.Application;
using MediClinic.Domain;

namespace MediClinic.Web.Models;

public sealed record PatientDashboardModel(int TotalAppointments, int PendingAppointments,
    int ConfirmedUpcomingAppointments, int CompletedAppointments, AppointmentSummary? NextAppointment)
{
    // Input must come from the authenticated patient's server-scoped booking query.
    public static PatientDashboardModel Create(IReadOnlyList<AppointmentSummary> appointments, DateTimeOffset now)
    {
        var upcoming = appointments.Where(x => x.StartsAt > now &&
            x.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed).ToArray();
        return new(appointments.Count,
            appointments.Count(x => x.Status == AppointmentStatus.Pending),
            upcoming.Count(x => x.Status == AppointmentStatus.Confirmed),
            appointments.Count(x => x.Status == AppointmentStatus.Completed),
            upcoming.OrderBy(x => x.StartsAt).ThenBy(x => x.Id).FirstOrDefault());
    }
}
