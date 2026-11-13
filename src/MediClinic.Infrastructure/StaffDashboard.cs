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
        => await DashboardData((await AdminScope(userId)).AsNoTracking(), true);

    private async Task<IQueryable<Appointment>> AdminScope(string userId)
    {
        var hasRole = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
            where ur.UserId == userId && r.Name == AuthRoles.ClinicAdmin select ur).AnyAsync();
        var clinics = db.ClinicAdministrators.Where(a => a.UserId == userId);
        if (!hasRole || !await clinics.AnyAsync())
            throw new BookingException("This account has no clinic administration scope.");
        return db.Appointments.Where(a => clinics.Any(c => c.ClinicId == EF.Property<Guid>(a.Service.Department.Branch, "ClinicKey")))
            .Include(a => a.Patient).Include(a => a.Doctor).Include(a => a.Service).ThenInclude(s => s.Department).ThenInclude(d => d.Branch).ThenInclude(b => b.Clinic);
    }

    public async Task<IReadOnlyList<StaffAppointment>> AdminAppointments(string userId, AppointmentStatus? status, DateOnly? from, DateOnly? to)
    {
        var query = (await AdminScope(userId)).AsNoTracking();
        if (status.HasValue) query = query.Where(a => a.Status == status);
        if (from.HasValue) { var start = Scheduling.ToUtc(from.Value.ToDateTime(TimeOnly.MinValue)); query = query.Where(a => a.StartsAt >= start); }
        if (to.HasValue) { var end = Scheduling.ToUtc(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)); query = query.Where(a => a.StartsAt < end); }
        return (await query.OrderBy(a => a.StartsAt).ThenBy(a => a.Id).ToListAsync()).Select(a => new StaffAppointment(Summary(a), a.Patient.FullName)).ToList();
    }
    public async Task<StaffAppointment?> AdminAppointment(string userId, Guid id)
    {
        var item = await (await AdminScope(userId)).AsNoTracking().SingleOrDefaultAsync(a => a.Id == id);
        return item is null ? null : new(Summary(item), item.Patient.FullName);
    }
    public async Task AdminAct(string userId, Guid id, string action, string? reason)
    {
        if (action is not ("Confirm" or "Reject")) throw new BookingException("Geçersiz işlem.");
        var item = await (await AdminScope(userId)).SingleOrDefaultAsync(a => a.Id == id)
            ?? throw new BookingException("Randevu bulunamadı.");
        await ApplyAction(item, action, reason);
    }

    private async Task<StaffDashboardData> DashboardData(IQueryable<Appointment> query, bool chronological = false)
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
        var pendingItems = await pending.OrderBy(x => !chronological && x.StartsAt < now).ThenBy(x => x.StartsAt).ThenBy(x => x.Id).Take(6)
            .Select(x => new StaffDashboardAppointment(x.Id, x.Patient.FirstName + " " + x.Patient.LastName, x.Doctor.FirstName + " " + x.Doctor.LastName,
                x.Service.Name, x.Service.Department.Branch.Name, x.StartsAt, x.Status)).ToListAsync();
        var todayItems = await today.OrderBy(x => x.StartsAt).ThenBy(x => x.Id)
            .Select(x => new StaffDashboardAppointment(x.Id, x.Patient.FirstName + " " + x.Patient.LastName, x.Doctor.FirstName + " " + x.Doctor.LastName,
                x.Service.Name, x.Service.Department.Branch.Name, x.StartsAt, x.Status)).ToListAsync();
        return new(todayCount, pendingCount, confirmedCount, completedCount, pendingItems, todayItems);
    }
}
