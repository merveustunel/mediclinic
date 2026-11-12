using MediClinic.Domain;
namespace MediClinic.Infrastructure.Identity;

// Granted only by trusted provisioning or atomically when an already scoped admin creates a clinic.
public sealed class ClinicAdministrator
{
    public string UserId { get; set; } = null!;
    public Guid ClinicId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Clinic Clinic { get; set; } = null!;
}
