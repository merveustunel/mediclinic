namespace MediClinic.Domain;

public sealed class Appointment : Entity
{
    // EF materialization only; public creation still enforces domain invariants.
    private Appointment(Guid id) : base(id) { }

    public Appointment(Guid id, Patient patient, Doctor doctor, MedicalService service, DateTimeOffset startsAt)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(doctor);
        ArgumentNullException.ThrowIfNull(service);
        if (!doctor.CanProvide(service))
            throw new ArgumentException("Doctor and service must belong to the same department.", nameof(service));

        Patient = patient;
        Doctor = doctor;
        Service = service;
        StartsAt = startsAt;
        EndsAt = startsAt.Add(service.Duration);
        Price = decimal.Round(service.Price, 2);
    }

    public Patient Patient { get; } = null!;
    public Guid PatientId => Patient.Id;
    public Doctor Doctor { get; } = null!;
    public Guid DoctorId => Doctor.Id;
    public MedicalService Service { get; } = null!;
    public Guid ServiceId => Service.Id;
    public Branch Branch => Service.Department.Branch;
    public Guid BranchId => Branch.Id;
    // Null means the historical charge is unknown; never infer it from the catalog.
    public decimal? Price { get; private set; }
    public DateTimeOffset StartsAt { get; }
    public DateTimeOffset EndsAt { get; }
    public AppointmentStatus Status { get; private set; } = AppointmentStatus.Pending;
    public string? DecisionReason { get; private set; }

    public void Confirm()
    {
        RequireStatus(AppointmentStatus.Pending);
        Status = AppointmentStatus.Confirmed;
    }

    public void Reject(string reason)
    {
        RequireStatus(AppointmentStatus.Pending);
        var validatedReason = Guard.Required(reason, nameof(reason));
        DecisionReason = validatedReason;
        Status = AppointmentStatus.Rejected;
    }

    public void Cancel(string reason)
    {
        if (Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            throw new InvalidOperationException("Only pending or confirmed appointments can be cancelled.");
        var validatedReason = Guard.Required(reason, nameof(reason));
        DecisionReason = validatedReason;
        Status = AppointmentStatus.Cancelled;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        RequireStatus(AppointmentStatus.Confirmed);
        if (completedAt < EndsAt)
            throw new ArgumentOutOfRangeException(nameof(completedAt), "An appointment cannot finish before its scheduled end.");
        Status = AppointmentStatus.Completed;
    }

    private void RequireStatus(AppointmentStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Appointment must be {expected}; current status is {Status}.");
    }
}
