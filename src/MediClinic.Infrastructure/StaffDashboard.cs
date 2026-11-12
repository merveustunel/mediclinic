using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Infrastructure;

public sealed partial class AppointmentBooking
{
    public async Task<StaffDashboardData> Dashboard(string userId)
        => await DashboardData((await Scoped(userId, true)).AsNoTracking());

    public async Task<StaffDashboardData> ClinicAdminDashboard(string userId)
    {
        var hasRole = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
            where ur.UserId == userId && r.Name == AuthRoles.ClinicAdmin select ur).AnyAsync();
        var clinics = db.ClinicAdministrators.Where(a => a.UserId == userId);
        if (!hasRole || !await clinics.AnyAsync())
            throw new BookingException("This account has no clinic administration scope.");
        return await DashboardData(db.Appointments.AsNoTracking().Where(a =>
            clinics.Any(c => c.ClinicId == EF.Property<Guid>(a.Service.Department.Branch, "ClinicKey"))));
    }

    private async Task<StaffDashboardData> DashboardData(IQueryable<Appointment> query)
    {
        var now = clock.GetUtcNow();
        var localDate = Scheduling.Local(now).Date;
        var start = Scheduling.ToUtc(localDate);
        var end = Scheduling.ToUtc(localDate.AddDays(1));
        var today = query.Where(x => x.StartsAt >= start && x.StartsAt < end);
        var pending = query.Where(x => x.Status == AppointmentStatus.Pending);
        var todayCount = await today.CountAsync();
        var pendingCount = await pending.CountAsync();
        var confirmedCount = await today.CountAsync(x => x.Status == AppointmentStatus.Confirmed);
        var completedCount = await today.CountAsync(x => x.Status == AppointmentStatus.Completed);
        // Project names only after applying the caller's authorized scope.
        var pendingItems = await pending.OrderBy(x => x.StartsAt < now).ThenBy(x => x.StartsAt).ThenBy(x => x.Id).Take(6)
            .Select(x => new StaffDashboardAppointment(x.Id, x.Patient.FirstName + " " + x.Patient.LastName, x.Doctor.FirstName + " " + x.Doctor.LastName,
                x.Service.Name, x.Service.Department.Branch.Name, x.StartsAt, x.Status)).ToListAsync();
        var todayItems = await today.OrderBy(x => x.StartsAt).ThenBy(x => x.Id)
            .Select(x => new StaffDashboardAppointment(x.Id, x.Patient.FirstName + " " + x.Patient.LastName, x.Doctor.FirstName + " " + x.Doctor.LastName,
                x.Service.Name, x.Service.Department.Branch.Name, x.StartsAt, x.Status)).ToListAsync();
        return new(todayCount, pendingCount, confirmedCount, completedCount, pendingItems, todayItems);
    }
}
