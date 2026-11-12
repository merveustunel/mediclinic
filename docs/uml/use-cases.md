# Actors and use cases

Mermaid flowchart equivalent of a use-case diagram. Source: [controllers](../../src/MediClinic.Web/Controllers), [policies](../../src/MediClinic.Web/Program.cs), and [service scope checks](../../src/MediClinic.Infrastructure/AppointmentBooking.cs).

```mermaid
flowchart LR
    Visitor[Visitor]
    Patient[Patient role with linked Patient]
    Staff[Staff role with linked Staff]
    Admin[ClinicAdmin role with linked Staff]
    Operator[Local development operator]
    subgraph MVC[MediClinic application]
        Home([View home])
        Register([Register patient account])
        Login([Log in])
        Logout([Log out])
        PD([View own patient dashboard])
        Book([Select catalog and available time / request appointment])
        Own([List and filter own appointments / view details])
        Cancel([Cancel own Pending or Confirmed appointment with reason])
        SD([View department staff dashboard])
        Review([List and filter department appointments / view details])
        Confirm([Confirm Pending appointment])
        Reject([Reject Pending appointment with reason])
        Complete([Complete Confirmed appointment at or after EndsAt])
        Seed([Explicit Development demo seeding])
        Bootstrap([Explicit Development identity bootstrap])
    end
    Visitor --> Home
    Visitor --> Register
    Visitor --> Login
    Patient --> Logout
    Patient --> PD
    Patient --> Book
    Patient --> Own
    Patient --> Cancel
    Staff --> Logout
    Staff --> SD
    Staff --> Review
    Staff --> Confirm
    Staff --> Reject
    Staff --> Complete
    Admin --> Logout
    Admin --> SD
    Admin --> Review
    Operator --> Seed
    Operator --> Bootstrap
```

All roles log in through the same account endpoint. ClinicAdmin alone has read access only, restricted to its linked Staff department; an unlinked administrator cannot load clinical dashboard/list data. Review mutations additionally require the Staff role. Bootstrap creates an unlinked ClinicAdmin; it does not grant global clinical access. Doctor is a domain subtype, not an additional Identity role. There is no catalog-management, availability-management or account-relinking web use case.
