# Actors and use cases

Mermaid flowchart equivalent of a use-case diagram. Source: [controllers](../../src/MediClinic.Web/Controllers), [policies](../../src/MediClinic.Web/Program.cs), and [service scope checks](../../src/MediClinic.Infrastructure/AppointmentBooking.cs).

```mermaid
flowchart LR
    Visitor[Visitor]
    Patient[Patient role with linked Patient]
    Staff[Staff role with linked Staff]
    Admin[ClinicAdmin role]
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
        Catalog([Manage granted clinics / branches / departments / services / hours])
        Employees([Manage granted employees / doctor services / availability])
        Grant([Explicit trusted initial clinic grant])
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
    Admin -->|requires linked Staff| SD
    Admin -->|requires linked Staff| Review
    Admin -->|requires explicit clinic grant| Catalog
    Admin -->|requires explicit clinic grant| Employees
    Operator --> Seed
    Operator --> Bootstrap
    Operator --> Grant
```

All roles share the login endpoint. Clinical reads for ClinicAdmin require a Staff link and remain department-scoped; review mutations additionally require Staff role. Catalog/employee management instead requires explicit clinic grants. Bootstrap creates an unlinked ClinicAdmin without grants; [trusted provisioning](../database-migration-runbook.md#initial-administrator-provisioning) is separate. Doctor uses Staff Identity role. No public privileged registration, account-relinking or grant-management web use case exists.
