using System.Globalization;

namespace MediClinic.Domain;

/// <summary>Explicit, reviewed medical vocabulary. Unknown/custom names are preserved.</summary>
public static class MedicalNames
{
    public static readonly IReadOnlyList<(string Turkish, string English)> Translations = new[]
    {
        ("Dermatoloji", "Dermatology"),
        ("Dermatoloji Muayenesi", "Dermatology Consultation"),
        ("Kardiyoloji", "Cardiology"),
        ("Kardiyoloji Muayenesi", "Cardiology Consultation"),
        ("Çocuk Sağlığı ve Hastalıkları", "Pediatrics"),
        ("Çocuk Sağlığı ve Hastalıkları Muayenesi", "Pediatrics Consultation"),
        ("Göz Hastalıkları", "Ophthalmology"),
        ("Göz Hastalıkları Muayenesi", "Ophthalmology Consultation"),
        ("Kulak Burun Boğaz", "Ear, Nose and Throat"),
        ("Kulak Burun Boğaz Muayenesi", "Ear, Nose and Throat Consultation"),
        ("Ortopedi ve Travmatoloji", "Orthopedics and Traumatology"),
        ("Ortopedi ve Travmatoloji Muayenesi", "Orthopedics and Traumatology Consultation"),
        ("Dahiliye", "Internal Medicine"),
        ("Dahiliye Muayenesi", "Internal Medicine Consultation")
    };

    public static string Display(Department department, string? culture = null) => Select(department.Name, department.NameTr, department.NameEn, culture);
    public static string Display(MedicalService service, string? culture = null) => Select(service.Name, service.NameTr, service.NameEn, culture);
    private static string Select(string name, string? tr, string? en, string? culture)
    {
        var translation = (culture ?? CultureInfo.CurrentUICulture.Name).StartsWith("en", StringComparison.OrdinalIgnoreCase) ? en : tr;
        return string.IsNullOrWhiteSpace(translation) ? Display(name, culture) : translation;
    }

    public static string Display(string name, string? culture = null)
    {
        var english = (culture ?? CultureInfo.CurrentUICulture.Name).StartsWith("en", StringComparison.OrdinalIgnoreCase);
        foreach (var pair in Translations)
            if (string.Equals(name, pair.Turkish, StringComparison.OrdinalIgnoreCase) || string.Equals(name, pair.English, StringComparison.OrdinalIgnoreCase))
                return english ? pair.English : pair.Turkish;
        return name;
    }
}
