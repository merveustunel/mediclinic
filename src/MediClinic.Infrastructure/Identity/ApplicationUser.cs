using MediClinic.Domain;
using Microsoft.AspNetCore.Identity;
namespace MediClinic.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public Guid? PatientId { get; private set; }
    public Patient? Patient { get; private set; }
    public Guid? StaffId { get; private set; }
    public Staff? Staff { get; private set; }
    public static ApplicationUser ForPatient(string email, Patient patient) => new() { UserName = email, Email = email, Patient = patient, PatientId = patient.Id };
    public static ApplicationUser ForStaff(string email, Staff staff) => new() { UserName = email, Email = email, Staff = staff, StaffId = staff.Id };
    public static ApplicationUser Administrator(string email) => new() { UserName = email, Email = email };
}
public static class AuthRoles
{
    public const string Patient = "Patient";
    public const string Staff = "Staff";
    public const string ClinicAdmin = "ClinicAdmin";
    public static readonly string[] All = [Patient, Staff, ClinicAdmin];
}
