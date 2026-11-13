using System.ComponentModel.DataAnnotations;
namespace MediClinic.Web.Models;
public sealed class RegisterModel
{
    [Required(ErrorMessage = "E-posta alanı zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Parola alanı zorunludur."), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required(ErrorMessage = "Ad alanı zorunludur.")] public string FirstName { get; set; } = "";
    [Required(ErrorMessage = "Soyad alanı zorunludur.")] public string LastName { get; set; } = "";
    [Required(ErrorMessage = "Doğum tarihi zorunludur."), DataType(DataType.Date)] public DateOnly? DateOfBirth { get; set; }
}
public sealed class LoginModel
{
    [Required(ErrorMessage = "E-posta alanı zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Parola alanı zorunludur."), DataType(DataType.Password)] public string Password { get; set; } = "";
}
