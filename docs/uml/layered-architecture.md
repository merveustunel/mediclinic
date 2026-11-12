# Layered architecture

Source: project references in [Web](../../src/MediClinic.Web/MediClinic.Web.csproj), [Application](../../src/MediClinic.Application/MediClinic.Application.csproj), [Infrastructure](../../src/MediClinic.Infrastructure/MediClinic.Infrastructure.csproj), [Domain](../../src/MediClinic.Domain/MediClinic.Domain.csproj), and the [composition root](../../src/MediClinic.Web/Program.cs).

```mermaid
flowchart TB
    Browser[Browser: HTTPS / forms / optional JavaScript]
    subgraph Web[MediClinic.Web]
        Host[Program: DI, Identity, policies, middleware, routes]
        MVC[Clinical and scoped management controllers / Razor / static assets]
        Tooling[Explicit Development DemoSeed and IdentityBootstrap]
    end
    subgraph App[MediClinic.Application]
        Contract[IAppointmentBooking / DTOs / Scheduling]
    end
    subgraph Infra[MediClinic.Infrastructure]
        Service[AppointmentBooking / StaffDashboard partial]
        Identity[ApplicationUser / ClinicAdministrator grants / Identity EF stores]
        Persistence[MediClinicDbContext / migrations / design-time factory]
    end
    Domain[MediClinic.Domain: entities / invariants / inheritance]
    SQLite[(SQLite: clinical / Identity / grants / hours / assignments / overlap triggers)]
    Tests[MediClinic.Tests: xUnit / WebApplicationFactory / temporary SQLite]
    Browser --> MVC
    Host --> Service
    Host --> Identity
    Tooling --> Persistence
    MVC --> Contract
    MVC --> Identity
    MVC --> Persistence
    MVC --> Domain
    Contract --> Domain
    Service -. implements .-> Contract
    Service --> Persistence
    Service --> Domain
    Identity --> Domain
    Persistence --> Domain
    Persistence --> SQLite
    Tests --> Web
    Tests --> App
    Tests --> Infra
    Tests --> Domain
```

Arrows indicate dependencies; the dashed arrow indicates interface implementation. AccountController uses Identity and DbContext for registration; ClinicManagementController and EmployeeManagementController perform scoped transactions directly through DbContext. This is pragmatic layering, not strict Clean Architecture. AddInfrastructure registers IAppointmentBooking, DbContext and TimeProvider.System. Domain has no framework dependencies. SQLite maps Person as People TPH and stores separate catalog, appointment, Identity, WorkingHours, DoctorServices and ClinicAdministrators tables. Six migrations reach M12. Startup never migrates or seeds automatically. The external [Python operator tool](../../tools/database-safety.py) provides read-only inspection, online backup and explicit verified grants; it is not a runtime dependency or web privilege API.
