namespace MediClinic.Domain;

public sealed class Patient : Person
{
    public Patient(Guid id, string firstName, string lastName, DateOnly dateOfBirth)
        : base(id, firstName, lastName)
    {
        if (dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentOutOfRangeException(nameof(dateOfBirth), "Birth date cannot be in the future.");
        DateOfBirth = dateOfBirth;
    }

    public DateOnly DateOfBirth { get; }
    public override string Role => "Patient";
}
