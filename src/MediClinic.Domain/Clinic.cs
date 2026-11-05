namespace MediClinic.Domain;

public sealed class Clinic : Entity
{
    private readonly List<Branch> _branches = [];

    public Clinic(Guid id, string name) : base(id)
    {
        Name = Guard.Required(name, nameof(name));
    }

    public string Name { get; }
    public IReadOnlyList<Branch> Branches => _branches.AsReadOnly();

    public Branch AddBranch(Guid id, string name, string address)
    {
        if (_branches.Any(branch => branch.Id == id))
            throw new InvalidOperationException("A branch with this identifier already belongs to the clinic.");
        var branch = new Branch(id, this, name, address);
        _branches.Add(branch);
        return branch;
    }
}
