using System.ComponentModel.DataAnnotations;
using MediClinic.Application;

namespace MediClinic.Web.Models;

public sealed class BookingModel
{
    public Guid ClinicId { get; set; }
    public Guid BranchId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid ServiceId { get; set; }
    [Required(ErrorMessage = "Lütfen bir randevu saati seçin."), DataType(DataType.DateTime)]
    public DateTime? LocalStart { get; set; }
    public int Stage { get; set; } = 1;
    public BookingOption? Selection => Options.FirstOrDefault(x => x.ClinicId == ClinicId && x.BranchId == BranchId
        && x.DepartmentId == DepartmentId && x.DoctorId == DoctorId && x.ServiceId == ServiceId);
    public IReadOnlyList<BookingOption> Options { get; set; } = [];
    public IReadOnlyList<DateTime> Slots { get; set; } = [];
    public IReadOnlyList<BookingTimeCard> TimeCards { get; private set; } = [];
    public DateOnly Date { get; set; }

    public void PrepareTimeCards(DateTimeOffset now)
    {
        // Elapsed grid markers describe time only, never historical availability.
        // Future cards come exclusively from the authoritative Slots service.
        var starts = Slots.ToHashSet();
        if (Date == DateOnly.FromDateTime(Scheduling.Local(now)))
        {
            for (var minute = 0; minute < 24 * 60; minute += Scheduling.SlotIntervalMinutes)
            {
                var local = Date.ToDateTime(TimeOnly.MinValue).AddMinutes(minute);
                if (Scheduling.ToUtc(local) > now) break;
                starts.Add(local);
            }
        }
        TimeCards = starts.Order().Select(local => new BookingTimeCard(local, Scheduling.ToUtc(local) <= now)).ToArray();
    }
}

public sealed record BookingTimeCard(DateTime LocalStart, bool IsPast);

public sealed class AppointmentActionModel
{
    public Guid Id { get; set; }
    [Required] public string Action { get; set; } = "";
    [StringLength(2000)] public string? Reason { get; set; }
}
