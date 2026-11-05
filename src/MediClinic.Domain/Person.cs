namespace MediClinic.Domain;

public abstract class Person : Entity
{
    // For derived persistence constructors only.
    private protected Person(Guid id) : base(id) { }

    protected Person(Guid id, string firstName, string lastName) : base(id)
    {
        FirstName = Guard.Required(firstName, nameof(firstName));
        LastName = Guard.Required(lastName, nameof(lastName));
    }

    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string FullName => $"{FirstName} {LastName}";
    public abstract string Role { get; }

    public void Rename(string firstName, string lastName)
    {
        var validatedFirstName = Guard.Required(firstName, nameof(firstName));
        var validatedLastName = Guard.Required(lastName, nameof(lastName));
        FirstName = validatedFirstName;
        LastName = validatedLastName;
    }
}
