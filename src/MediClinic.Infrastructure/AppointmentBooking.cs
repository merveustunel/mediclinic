using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Infrastructure;

public sealed class AppointmentBooking(MediClinicDbContext db, TimeProvider clock) : IAppointmentBooking
{
    private async Task<ApplicationUser> User(string id, string role)
    {
        var user = await db.Users.Include(x => x.Patient).Include(x => x.Staff).ThenInclude(x => x!.Department)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (user == null || !await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                where ur.UserId == id && r.Name == role select ur).AnyAsync()
            || (role == AuthRoles.Patient ? user.Patient == null : user.Staff == null))
            throw new BookingException("This account is not authorized for this operation.");
        return user;
    }

    public async Task<IReadOnlyList<BookingOption>> Options(string userId)
    {
        await User(userId, AuthRoles.Patient);
        var doctors = await db.Doctors.Include(x => x.Department).ThenInclude(x => x.Branch).ThenInclude(x => x.Clinic).ToListAsync();
        var services = await db.MedicalServices.Include(x => x.Department).ToListAsync();
        return (from doctor in doctors from service in services where doctor.CanProvide(service)
            let department = doctor.Department let branch = department.Branch
            select new BookingOption(branch.Clinic.Id, branch.Id, department.Id, doctor.Id, service.Id,
                branch.Clinic.Name, branch.Name, department.Name, doctor.FullName, service.Name, service.Duration)).ToList();
    }

    private IQueryable<Appointment> Blocking(Guid doctorId) => db.Appointments.Where(x =>
        EF.Property<Guid>(x, "DoctorKey") == doctorId &&
        (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Confirmed || x.Status == AppointmentStatus.Completed));

    public async Task<IReadOnlyList<DateTime>> Slots(string userId, Guid doctorId, Guid serviceId, DateOnly date)
    {
        await User(userId, AuthRoles.Patient);
        var doctor = await db.Doctors.Include(x => x.Department).SingleOrDefaultAsync(x => x.Id == doctorId);
        var service = await db.MedicalServices.Include(x => x.Department).SingleOrDefaultAsync(x => x.Id == serviceId);
        if (doctor == null || service == null || !doctor.CanProvide(service)) throw new BookingException("Invalid doctor/service selection.");
        if (date < DateOnly.FromDateTime(Scheduling.Local(clock.GetUtcNow())) || date > DateOnly.FromDateTime(Scheduling.Local(clock.GetUtcNow())).AddYears(1))
            throw new BookingException("Choose a scheduling date within the next year.");
        var start = Scheduling.ToUtc(date.ToDateTime(TimeOnly.MinValue));
        var end = Scheduling.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var availability = await db.StaffAvailabilities.Where(x => EF.Property<Guid>(x, "StaffKey") == doctorId && x.StartsAt < end && x.EndsAt > start).ToListAsync();
        // A slot starting late in the selected day can extend into the following day.
        DateTimeOffset latestEnd;
        try { latestEnd = end.Add(service.Duration); }
        catch (ArgumentOutOfRangeException) { latestEnd = DateTimeOffset.MaxValue; }
        var busy = await Blocking(doctorId).Where(x => x.StartsAt < latestEnd && x.EndsAt > start).ToListAsync();
        var slots = new List<DateTime>();
        for (var time = start; time < end; time = time.AddMinutes(Scheduling.SlotIntervalMinutes))
        {
            DateTimeOffset finish;
            try { finish = time.Add(service.Duration); }
            catch (ArgumentOutOfRangeException) { continue; }
            if (time > clock.GetUtcNow() && availability.Any(x => x.Covers(time, finish)) && !busy.Any(x => x.StartsAt < finish && time < x.EndsAt))
                slots.Add(Scheduling.Local(time));
        }
        return slots;
    }

    public async Task<Guid> Book(string userId, BookingRequest request)
    {
        var user = await User(userId, AuthRoles.Patient);
        var doctor = await db.Doctors.Include(x => x.Department).ThenInclude(x => x.Branch).ThenInclude(x => x.Clinic).SingleOrDefaultAsync(x => x.Id == request.DoctorId);
        var service = await db.MedicalServices.Include(x => x.Department).ThenInclude(x => x.Branch).SingleOrDefaultAsync(x => x.Id == request.ServiceId);
        if (doctor == null || service == null || !doctor.CanProvide(service) || service.Department.Id != request.DepartmentId ||
            service.Department.Branch.Id != request.BranchId || doctor.Department.Branch.Clinic.Id != request.ClinicId)
            throw new BookingException("Invalid clinic, branch, department, doctor or service selection.");
        var start = Scheduling.ToUtc(request.LocalStart);
        if (start <= clock.GetUtcNow() || start > clock.GetUtcNow().AddYears(1)) throw new BookingException("Choose a future time within the next year.");
        DateTimeOffset end;
        try { end = start.Add(service.Duration); }
        catch (ArgumentOutOfRangeException) { throw new BookingException("The appointment duration is outside the supported range."); }
        if (!await db.StaffAvailabilities.AnyAsync(x => EF.Property<Guid>(x, "StaffKey") == doctor.Id && x.StartsAt <= start && x.EndsAt >= end))
            throw new BookingException("The doctor is unavailable at this time.");
        if (await Blocking(doctor.Id).AnyAsync(x => x.StartsAt < end && start < x.EndsAt)) throw new BookingException("This time is already reserved.");
        var appointment = new Appointment(Guid.NewGuid(), user.Patient!, doctor, service, start);
        db.Appointments.Add(appointment);
        await Save();
        return appointment.Id;
    }

    private async Task<IQueryable<Appointment>> Scoped(string userId, bool staff)
    {
        var user = await User(userId, staff ? AuthRoles.Staff : AuthRoles.Patient);
        var query = db.Appointments.Include(x => x.Doctor).Include(x => x.Service).ThenInclude(x => x.Department).ThenInclude(x => x.Branch);
        return staff ? query.Where(x => x.Service.Department.Id == user.Staff!.Department.Id)
            : query.Where(x => EF.Property<Guid>(x, "PatientKey") == user.PatientId);
    }
    private static AppointmentSummary Summary(Appointment x) => new(x.Id, x.Service.Name, x.Doctor.FullName, x.Branch.Name, x.StartsAt, x.EndsAt, x.Status, x.DecisionReason);
    public async Task<IReadOnlyList<AppointmentSummary>> List(string userId, bool staff) =>
        (await (await Scoped(userId, staff)).OrderByDescending(x => x.StartsAt).ToListAsync()).Select(Summary).ToList();
    public async Task<AppointmentSummary?> Find(string userId, Guid id, bool staff)
    {
        var appointment = await (await Scoped(userId, staff)).SingleOrDefaultAsync(x => x.Id == id);
        return appointment == null ? null : Summary(appointment);
    }
    public async Task Act(string userId, Guid id, string action, string? reason)
    {
        var appointment = await (await Scoped(userId, action != "Cancel")).SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BookingException("Appointment not found or outside your scope.");
        try
        {
            switch (action)
            {
                case "Confirm": appointment.Confirm(); break;
                case "Reject": appointment.Reject(reason!); break;
                case "Cancel": appointment.Cancel(reason!); break;
                case "Complete": appointment.Complete(clock.GetUtcNow()); break;
                default: throw new BookingException("Unknown appointment action.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { throw new BookingException(ex.Message); }
        await Save();
    }
    private async Task Save()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw new BookingException("The appointment changed. Refresh and try again."); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqlite &&
            (sqlite.SqliteErrorCode is 5 or 6 || sqlite.Message.Contains("appointment_overlap")))
        { db.ChangeTracker.Clear(); throw new BookingException("This time was reserved or scheduling is busy. Refresh and try again."); }
    }
}
