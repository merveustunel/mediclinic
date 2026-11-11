using System.ComponentModel.DataAnnotations;
namespace MediClinic.Web.Models;
public sealed class RegisterModel
{
    [Required(ErrorMessage = "E-posta alanı zorunludur."), EmailAddress] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Parola alanı zorunludur."), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required] public string FirstName { get; set; } = "";
    [Required] public string LastName { get; set; } = "";
    [Required, DataType(DataType.Date)] public DateOnly? DateOfBirth { get; set; }
}
public sealed class LoginModel
{
    [Required(ErrorMessage = "E-posta alanı zorunludur."), EmailAddress] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Parola alanı zorunludur."), DataType(DataType.Password)] public string Password { get; set; } = "";
}
