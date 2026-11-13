using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Localization;

namespace MediClinic.Web.Localization;

public static class Ui
{
    private static readonly ResourceManager Resources = new("MediClinic.Web.Resources.SharedResource", typeof(Ui).Assembly);
    public static string Text(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    public static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Text(key), args);
    public static string Money(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture) + (CultureInfo.CurrentCulture.Name == "tr-TR" ? " TL" : " TRY");
    public static string Date(DateTime value) => value.ToString("g", CultureInfo.CurrentCulture);
    public static IReadOnlyDictionary<string, string> Catalog(string culture)
        => Resources.GetResourceSet(CultureInfo.GetCultureInfo(culture), true, true)!
            .Cast<System.Collections.DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!);
}
