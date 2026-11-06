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
    [Required, DataType(DataType.DateTime)]
    public DateTime? LocalStart { get; set; }
    public IReadOnlyList<BookingOption> Options { get; set; } = [];
    public IReadOnlyList<DateTime> Slots { get; set; } = [];
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(Scheduling.Local(DateTimeOffset.UtcNow)).AddDays(1);
}

public sealed class AppointmentActionModel
{
    public Guid Id { get; set; }
    [Required] public string Action { get; set; } = "";
    [StringLength(2000)] public string? Reason { get; set; }
}
