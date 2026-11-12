using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Infrastructure;

public sealed partial class AppointmentBooking(MediClinicDbContext db, TimeProvider clock) : IAppointmentBooking
{
    public async Task<IReadOnlyList<StaffAppointment>> StaffAppointments(string userId, AppointmentStatus? status, DateOnly? date)
    {
        var query = (await Scoped(userId, true)).AsNoTracking().Include(x => x.Patient).AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (date.HasValue)
        {
            var start = Scheduling.ToUtc(date.Value.ToDateTime(TimeOnly.MinValue));
            var end = Scheduling.ToUtc(date.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(x => x.StartsAt >= start && x.StartsAt < end);
        }
        return (await query.OrderByDescending(x => x.StartsAt).ThenBy(x => x.Id).ToListAsync())
            .Select(x => new StaffAppointment(Summary(x), x.Patient.FullName)).ToList();
    }

    public async Task<StaffAppointment?> StaffAppointment(string userId, Guid id)
    {
        var item = await (await Scoped(userId, true)).AsNoTracking().Include(x => x.Patient).SingleOrDefaultAsync(x => x.Id == id);
        return item == null ? null : new(Summary(item), item.Patient.FullName);
    }
    // Missing days remain unrestricted for compatibility with pre-M10 schedules.
    public static bool BranchOpen(IReadOnlyList<WorkingHours> hours, DateTimeOffset start, DateTimeOffset end)
    {
        var localStart = Scheduling.Local(start); var localEnd = Scheduling.Local(end);
        // Inspect each touched local day; an interval ending at midnight does not touch the next day.
        for (var date = localStart.Date; date < localEnd; date = date.AddDays(1))
        {
            var day = hours.SingleOrDefault(x => x.Day == date.DayOfWeek);
            if (day is null) continue;
            if (day.Closed || localStart < date.Add(day.Opens!.Value.ToTimeSpan()) ||
                localEnd > date.Add(day.Closes!.Value.ToTimeSpan())) return false;
        }
        return true;
    }

    private async Task<ApplicationUser> User(string id, string role, bool allowAdmin = false)
    {
        var user = await db.Users.Include(x => x.Patient).Include(x => x.Staff).ThenInclude(x => x!.Department)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (user == null || !await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                where ur.UserId == id && (r.Name == role || (allowAdmin && role == AuthRoles.Staff && r.Name == AuthRoles.ClinicAdmin)) select ur).AnyAsync()
            || (role == AuthRoles.Patient ? user.Patient == null : user.Staff == null))
            throw new BookingException("This account is not authorized for this operation.");
        return user;
    }

    public async Task<IReadOnlyList<BookingOption>> Options(string userId)
    {
        await User(userId, AuthRoles.Patient);
        var doctors = await db.Doctors.Include(x => x.Services).Include(x => x.Department).ThenInclude(x => x.Branch).ThenInclude(x => x.Clinic).ToListAsync();
        var services = await db.MedicalServices.Include(x => x.Department).ToListAsync();
        var closedDays = await db.WorkingHours.AsNoTracking().Where(h => h.Closed).ToListAsync();
        return (from doctor in doctors from service in services where doctor.Offers(service)
            let department = doctor.Department let branch = department.Branch
            select new BookingOption(branch.Clinic.Id, branch.Id, department.Id, doctor.Id, service.Id,
                branch.Clinic.Name, branch.Name, department.Name, doctor.FullName, service.Name, service.Duration, service.Price, closedDays.Where(h => h.BranchId == branch.Id).Select(h => h.Day).ToArray())).ToList();
    }

    private IQueryable<Appointment> Blocking(Guid doctorId) => db.Appointments.Where(x =>
        EF.Property<Guid>(x, "DoctorKey") == doctorId &&
        (x.Status == AppointmentStatus.Pending || x.Status == AppointmentStatus.Confirmed || x.Status == AppointmentStatus.Completed));

    public async Task<IReadOnlyList<DateTime>> Slots(string userId, Guid doctorId, Guid serviceId, DateOnly date)
    {
        await User(userId, AuthRoles.Patient);
        var doctor = await db.Doctors.Include(x => x.Services).Include(x => x.Department).ThenInclude(x => x.Branch).SingleOrDefaultAsync(x => x.Id == doctorId);
        var service = await db.MedicalServices.Include(x => x.Department).SingleOrDefaultAsync(x => x.Id == serviceId);
        if (doctor == null || service == null || !doctor.Offers(service)) throw new BookingException("Invalid doctor/service selection.");
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
        var hours = await db.WorkingHours.AsNoTracking().Where(x => x.BranchId == doctor.Department.BranchId).ToListAsync();
        var slots = new List<DateTime>();
        for (var time = start; time < end; time = time.AddMinutes(Scheduling.SlotIntervalMinutes))
        {
            DateTimeOffset finish;
            try { finish = time.Add(service.Duration); }
            catch (ArgumentOutOfRangeException) { continue; }
            if (BranchOpen(hours, time, finish) && time > clock.GetUtcNow() && time <= clock.GetUtcNow().AddYears(1) && availability.Any(x => x.Covers(time, finish)) && !busy.Any(x => x.StartsAt < finish && time < x.EndsAt))
                slots.Add(Scheduling.Local(time));
        }
        return slots;
    }

    public async Task<Guid> Book(string userId, BookingRequest request)
    {
        var user = await User(userId, AuthRoles.Patient);
        var doctor = await db.Doctors.Include(x => x.Services).Include(x => x.Department).ThenInclude(x => x.Branch).ThenInclude(x => x.Clinic).SingleOrDefaultAsync(x => x.Id == request.DoctorId);
        var service = await db.MedicalServices.Include(x => x.Department).ThenInclude(x => x.Branch).SingleOrDefaultAsync(x => x.Id == request.ServiceId);
        if (doctor == null || service == null || !doctor.Offers(service) || service.Department.Id != request.DepartmentId ||
            service.Department.Branch.Id != request.BranchId || doctor.Department.Branch.Clinic.Id != request.ClinicId)
            throw new BookingException("Invalid clinic, branch, department, doctor or service selection.");
        var start = Scheduling.ToUtc(request.LocalStart);
        if (start <= clock.GetUtcNow() || start > clock.GetUtcNow().AddYears(1)) throw new BookingException("Choose a future time within the next year.");
        DateTimeOffset end;
        try { end = start.Add(service.Duration); }
        catch (ArgumentOutOfRangeException) { throw new BookingException("The appointment duration is outside the supported range."); }
        if (!BranchOpen(await db.WorkingHours.AsNoTracking().Where(x => x.BranchId == request.BranchId).ToListAsync(), start, end))
            throw new BookingException("The branch is closed or the service extends beyond working hours.");
        if (!await db.StaffAvailabilities.AnyAsync(x => EF.Property<Guid>(x, "StaffKey") == doctor.Id && x.StartsAt <= start && x.EndsAt >= end))
            throw new BookingException("The doctor is unavailable at this time.");
        if (await Blocking(doctor.Id).AnyAsync(x => x.StartsAt < end && start < x.EndsAt)) throw new BookingException("This time is already reserved.");
        var appointment = new Appointment(Guid.NewGuid(), user.Patient!, doctor, service, start);
        db.Appointments.Add(appointment);
        // A deferred transaction lets competing booking INSERTs reach the existing overlap
        // trigger, while holding the write lock through the final schedule/assignment check.
        // Management writes use immediate transactions, so they cannot invalidate this check.
        await db.Database.OpenConnectionAsync();
        await using var sqliteTransaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await using var transaction = await db.Database.UseTransactionAsync(sqliteTransaction);
        await Save();
        if (!await db.Doctors.AnyAsync(d => d.Id == doctor.Id && d.Services.Any(s => s.Id == service.Id)) ||
            !await db.StaffAvailabilities.AnyAsync(x => EF.Property<Guid>(x, "StaffKey") == doctor.Id && x.StartsAt <= start && x.EndsAt >= end))
            throw new BookingException("The doctor's services or availability changed. Refresh and try again.");
        if (start <= clock.GetUtcNow()) throw new BookingException("Choose a future appointment time.");
        // Re-read after acquiring the SQLite write lock: catalog/hours may have changed since prechecks.
        if (!BranchOpen(await db.WorkingHours.AsNoTracking().Where(x => x.BranchId == request.BranchId).ToListAsync(), start, end))
            throw new BookingException("Branch hours changed. Refresh and try again.");
        var currentService = await db.MedicalServices.AsNoTracking().SingleAsync(x => x.Id == service.Id);
        if (currentService.Price != service.Price || currentService.Duration != service.Duration)
            throw new BookingException("Service details changed. Refresh and try again.");
        await transaction!.CommitAsync();
        return appointment.Id;
    }

    private async Task<IQueryable<Appointment>> Scoped(string userId, bool staff, bool allowAdmin = false)
    {
        var user = await User(userId, staff ? AuthRoles.Staff : AuthRoles.Patient, allowAdmin);
        var query = db.Appointments.Include(x => x.Doctor).Include(x => x.Service).ThenInclude(x => x.Department).ThenInclude(x => x.Branch).ThenInclude(x => x.Clinic);
        return staff ? query.Where(x => x.Service.Department.Id == user.Staff!.Department.Id)
            : query.Where(x => EF.Property<Guid>(x, "PatientKey") == user.PatientId);
    }
    private static AppointmentSummary Summary(Appointment x) => new(x.Id, x.Service.Name, x.Doctor.FullName, x.Branch.Name, x.StartsAt, x.EndsAt, x.Status, x.DecisionReason, x.Price, x.Branch.Clinic.Name, x.Service.Department.Name);
    public async Task<IReadOnlyList<AppointmentSummary>> List(string userId, bool staff) =>
        (await (await Scoped(userId, staff, true)).OrderByDescending(x => x.StartsAt).ToListAsync()).Select(Summary).ToList();
    public async Task<AppointmentSummary?> Find(string userId, Guid id, bool staff)
    {
        var appointment = await (await Scoped(userId, staff, true)).SingleOrDefaultAsync(x => x.Id == id);
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
