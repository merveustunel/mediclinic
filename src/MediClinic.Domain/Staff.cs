namespace MediClinic.Domain;

public class Staff : Person
{
    public Staff(Guid id, string firstName, string lastName, Department department, string jobTitle)
        : base(id, firstName, lastName)
    {
        ArgumentNullException.ThrowIfNull(department);
        Department = department;
        JobTitle = Guard.Required(jobTitle, nameof(jobTitle));
    }

    public Department Department { get; }
    public Guid DepartmentId => Department.Id;
    public string JobTitle { get; }
    public override string Role => JobTitle;
}
