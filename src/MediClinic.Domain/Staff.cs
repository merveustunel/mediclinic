namespace MediClinic.Domain;

public class Staff : Person
{
    // EF materialization only, also used by Doctor.
    private protected Staff(Guid id) : base(id) { }

    public Staff(Guid id, string firstName, string lastName, Department department, string jobTitle)
        : base(id, firstName, lastName)
    {
        ArgumentNullException.ThrowIfNull(department);
        Department = department;
        JobTitle = Guard.Required(jobTitle, nameof(jobTitle));
    }

    public Department Department { get; private set; } = null!;
    public Guid DepartmentId => Department.Id;
    public string JobTitle { get; private set; } = null!;
    public override string Role => JobTitle;

    public void UpdateEmployment(Department department, string jobTitle)
    {
        ArgumentNullException.ThrowIfNull(department);
        var title = this is Doctor ? "Doctor" : Guard.Required(jobTitle, nameof(jobTitle));
        Department = department;
        JobTitle = this is Doctor ? "Doctor" : title;
    }
}
