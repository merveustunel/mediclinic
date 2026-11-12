using MediClinic.Application;
namespace MediClinic.Web.Models;
public sealed record StaffAppointmentsModel(IReadOnlyList<StaffAppointment> Appointments, string? Status, DateOnly? Date);
public sealed record StaffAppointmentDetailsModel(StaffAppointment Item, bool CanComplete, string? Reason = null);
