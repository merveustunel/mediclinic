using System.ComponentModel.DataAnnotations;
namespace MediClinic.Web.Models;

public sealed class WeeklyAvailabilityModel
{
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = "";
    [Required(ErrorMessage = "Başlangıç tarihi zorunludur.")] public DateOnly? From { get; set; }
    [Required(ErrorMessage = "Bitiş tarihi zorunludur.")] public DateOnly? To { get; set; }
    public List<DayOfWeek> Days { get; set; } = [];
    [Required(ErrorMessage = "Başlangıç saati zorunludur.")] public TimeOnly? Opens { get; set; }
    [Required(ErrorMessage = "Bitiş saati zorunludur.")] public TimeOnly? Closes { get; set; }
}
