namespace MediClinic.Web.Controllers;

// Map codes rather than displaying provider descriptions, which can include account data.
public static class TurkishIdentityErrors
{
    public static string Message(string code) => code switch
    {
        "PasswordTooShort" => "Parola en az 12 karakter olmalıdır.",
        "PasswordRequiresDigit" => "Parola en az bir rakam içermelidir.",
        "PasswordRequiresLower" => "Parola en az bir küçük harf içermelidir.",
        "PasswordRequiresUpper" => "Parola en az bir büyük harf içermelidir.",
        "PasswordRequiresNonAlphanumeric" => "Parola en az bir özel karakter içermelidir.",
        "PasswordRequiresUniqueChars" => "Parola daha fazla farklı karakter içermelidir.",
        "InvalidEmail" or "InvalidUserName" => "Geçerli bir e-posta adresi giriniz.",
        _ => "Hesap işlemi tamamlanamadı. Bilgilerinizi kontrol edip yeniden deneyiniz."
    };
}
