using MediClinic.Application;
using MediClinic.Domain;

namespace MediClinic.Web.Models;

public sealed record PatientAppointmentsModel(
    IReadOnlyList<AppointmentSummary> Appointments, int TotalCount, string? Status)
{
    public static IReadOnlyList<(string Value, string Label)> Filters { get; } = Array.AsReadOnly(new[]
    {
        ("", "Tümü"), ("Pending", "Beklemede"), ("Confirmed", "Onaylandı"),
        ("Completed", "Tamamlandı"), ("Cancelled", "İptal edildi"), ("Rejected", "Reddedildi")
    });

    // Match names explicitly: enum parsing also accepts numeric and composite input.
    public static bool IsValidFilter(string? status) =>
        status is null || Filters.Any(x => x.Value == status);

    public static PatientAppointmentsModel Create(IReadOnlyList<AppointmentSummary> scoped, string? status)
    {
        if (!IsValidFilter(status)) throw new ArgumentException("Unknown status.", nameof(status));
        var items = string.IsNullOrEmpty(status) ? scoped
            : scoped.Where(x => x.Status == Enum.Parse<AppointmentStatus>(status)).ToArray();
        return new(items, scoped.Count, string.IsNullOrEmpty(status) ? null : status);
    }
}
