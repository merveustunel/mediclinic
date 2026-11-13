using System.ComponentModel.DataAnnotations;
using MediClinic.Domain;
namespace MediClinic.Web.Models;
public sealed class ClinicManagementModel
{
    public Guid? ClinicId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public IReadOnlyList<Clinic> Clinics { get; set; } = [];
    public IReadOnlyList<WorkingHours> Hours { get; set; } = [];
}
public sealed class CatalogEditModel
{
    public string Kind { get; set; } = "Clinic";
    public Guid? Id { get; set; }
    public Guid? ParentId { get; set; }
    [Required(ErrorMessage = "Ad zorunludur."), StringLength(200, ErrorMessage = "Ad en fazla 200 karakter olabilir.")]
    [Display(Name = "Ad")] public string Name { get; set; } = "";
    [StringLength(500, ErrorMessage = "Adres en fazla 500 karakter olabilir.")]
    [Display(Name = "Adres")] public string? Address { get; set; }
    [Range(1, 1440, ErrorMessage = "Süre 1-1440 dakika olmalıdır.")]
    [Display(Name = "Süre (dakika)")] public int DurationMinutes { get; set; } = 30;
    [Range(typeof(decimal), "0", "999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "Geçerli bir TRY tutarı giriniz.")]
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType = typeof(TryPriceBinder))]
    [Display(Name = "Ücret (TRY)")] public decimal Price { get; set; }
}

// HTML number controls submit an invariant decimal regardless of the OS culture.
public sealed class TryPriceBinder : Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinder
{
    public Task BindModelAsync(Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == Microsoft.AspNetCore.Mvc.ModelBinding.ValueProviderResult.None)
        {
            context.Result = Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult.Success(0m);
            return Task.CompletedTask;
        }
        context.ModelState.SetModelValue(context.ModelName, value);
        if (decimal.TryParse(value.FirstValue, System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign,
            System.Globalization.CultureInfo.InvariantCulture, out var price))
            context.Result = Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult.Success(price);
        else context.ModelState.TryAddModelError(context.ModelName, "Geçerli bir TRY tutarı giriniz.");
        return Task.CompletedTask;
    }
}

public sealed class WeeklyHoursModel
{
    public Guid BranchId { get; set; }
    public List<WorkingDayModel> Days { get; set; } = [];
}
public sealed class WorkingDayModel
{
    public DayOfWeek Day { get; set; }
    public bool Closed { get; set; }
    public TimeOnly? Opens { get; set; }
    public TimeOnly? Closes { get; set; }
}
