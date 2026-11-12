using MediClinic.Application;
using MediClinic.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Infrastructure;

public sealed partial class AppointmentBooking
{
    public async Task<StaffDashboardData> Dashboard(string userId)
    {
        var now = clock.GetUtcNow();
        var localDate = Scheduling.Local(now).Date;
        var start = Scheduling.ToUtc(localDate);
        var end = Scheduling.ToUtc(localDate.AddDays(1));
        var query = (await Scoped(userId, true, true)).AsNoTracking();
        var today = query.Where(x => x.StartsAt >= start && x.StartsAt < end);
        var pending = query.Where(x => x.Status == AppointmentStatus.Pending);
        var todayCount = await today.CountAsync();
        var pendingCount = await pending.CountAsync();
        var confirmedCount = await today.CountAsync(x => x.Status == AppointmentStatus.Confirmed);
        var completedCount = await today.CountAsync(x => x.Status == AppointmentStatus.Completed);
        // Patient names are projected only for this staff-only read model.
        var pendingItems = await pending.OrderBy(x => x.StartsAt < now).ThenBy(x => x.StartsAt).ThenBy(x => x.Id).Take(6)
            .Select(x => new StaffDashboardAppointment(x.Id, x.Patient.FirstName + " " + x.Patient.LastName, x.Doctor.FirstName + " " + x.Doctor.LastName,
                x.Service.Name, x.Service.Department.Branch.Name, x.StartsAt, x.Status)).ToListAsync();
        var todayItems = await today.OrderBy(x => x.StartsAt).ThenBy(x => x.Id)
            .Select(x => new StaffDashboardAppointment(x.Id, x.Patient.FirstName + " " + x.Patient.LastName, x.Doctor.FirstName + " " + x.Doctor.LastName,
                x.Service.Name, x.Service.Department.Branch.Name, x.StartsAt, x.Status)).ToListAsync();
        return new(todayCount, pendingCount, confirmedCount, completedCount, pendingItems, todayItems);
    }
}
