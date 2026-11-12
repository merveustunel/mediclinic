using System.ComponentModel.DataAnnotations;
using MediClinic.Domain;

namespace MediClinic.Web.Models;

public sealed class EmployeeEditModel
{
    public Guid? Id { get; set; }
    [Display(Name = "Doktor")] public bool IsDoctor { get; set; }
    [Required(ErrorMessage = "Ad zorunludur."), StringLength(100, ErrorMessage = "Ad en fazla 100 karakter olabilir."), Display(Name = "Ad")] public string FirstName { get; set; } = "";
    [Required(ErrorMessage = "Soyad zorunludur."), StringLength(100, ErrorMessage = "Soyad en fazla 100 karakter olabilir."), Display(Name = "Soyad")] public string LastName { get; set; } = "";
    [Display(Name = "Klinik / şube / bölüm")] public Guid DepartmentId { get; set; }
    [StringLength(100, ErrorMessage = "Görev en fazla 100 karakter olabilir."), Display(Name = "Görev")] public string? JobTitle { get; set; }
    [StringLength(150, ErrorMessage = "Uzmanlık en fazla 150 karakter olabilir."), Display(Name = "Uzmanlık")] public string? Specialty { get; set; }
    [Display(Name = "Hizmetler")] public List<Guid> ServiceIds { get; set; } = [];
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz."), Display(Name = "Hesap e-postası")] public string? Email { get; set; }
    [DataType(DataType.Password), Display(Name = "İlk parola (en az 12 karakter)")] public string? InitialPassword { get; set; }
    public List<Department> Departments { get; set; } = [];
    public List<MedicalService> Services { get; set; } = [];
    public bool HasAccount { get; set; }
}

public sealed class AvailabilityEditModel
{
    public Guid? Id { get; set; }
    public Guid DoctorId { get; set; }
    [Required(ErrorMessage = "Başlangıç zorunludur."), Display(Name = "Başlangıç (İstanbul)")] public DateTime? StartsAt { get; set; }
    [Required(ErrorMessage = "Bitiş zorunludur."), Display(Name = "Bitiş (İstanbul)")] public DateTime? EndsAt { get; set; }
    public string DoctorName { get; set; } = "";
}

public sealed record EmployeeListModel(IReadOnlyList<Staff> Employees, IReadOnlyDictionary<Guid, string> Emails);
public sealed record AvailabilityListModel(Doctor Doctor, DateOnly? Date, IReadOnlyList<StaffAvailability> Intervals);
