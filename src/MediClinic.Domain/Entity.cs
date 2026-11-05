namespace MediClinic.Domain;

public abstract class Entity
{
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("An entity requires a non-empty identifier.", nameof(id));
        Id = id;
    }

    public Guid Id { get; }
}
