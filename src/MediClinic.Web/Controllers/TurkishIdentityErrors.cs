using MediClinic.Web.Localization;
namespace MediClinic.Web.Controllers;

// Map codes rather than displaying provider descriptions, which can include account data.
public static class TurkishIdentityErrors
{
    public static string Message(string code) => code switch
    {
        "PasswordTooShort" => Ui.Text("Parola en az 12 karakter olmalıdır."),
        "PasswordRequiresDigit" => Ui.Text("Parola en az bir rakam içermelidir."),
        "PasswordRequiresLower" => Ui.Text("Parola en az bir küçük harf içermelidir."),
        "PasswordRequiresUpper" => Ui.Text("Parola en az bir büyük harf içermelidir."),
        "PasswordRequiresNonAlphanumeric" => Ui.Text("Parola en az bir özel karakter içermelidir."),
        "PasswordRequiresUniqueChars" => Ui.Text("Parola daha fazla farklı karakter içermelidir."),
        "InvalidEmail" or "InvalidUserName" => Ui.Text("Geçerli bir e-posta adresi giriniz."),
        _ => Ui.Text("Hesap işlemi tamamlanamadı. Bilgilerinizi kontrol edip yeniden deneyiniz.")
    };
}
