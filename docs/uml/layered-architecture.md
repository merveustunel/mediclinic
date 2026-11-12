# Layered architecture

Source: project references in [Web](../../src/MediClinic.Web/MediClinic.Web.csproj), [Application](../../src/MediClinic.Application/MediClinic.Application.csproj), [Infrastructure](../../src/MediClinic.Infrastructure/MediClinic.Infrastructure.csproj), [Domain](../../src/MediClinic.Domain/MediClinic.Domain.csproj), and the [composition root](../../src/MediClinic.Web/Program.cs).

```mermaid
flowchart TB
    Browser[Browser: HTTPS / forms / optional JavaScript]
    subgraph Web[MediClinic.Web]
        Host[Program: DI, Identity, policies, middleware, routes]
        MVC[Controllers / view models / Razor views / static assets]
        Tooling[Explicit Development DemoSeed and IdentityBootstrap]
    end
    subgraph App[MediClinic.Application]
        Contract[IAppointmentBooking / DTOs / Scheduling]
    end
    subgraph Infra[MediClinic.Infrastructure]
        Service[AppointmentBooking / StaffDashboard partial]
        Identity[ApplicationUser / Identity EF stores]
        Persistence[MediClinicDbContext / migrations / design-time factory]
    end
    Domain[MediClinic.Domain: entities / invariants / inheritance]
    SQLite[(SQLite: clinical and Identity tables / constraints / overlap triggers)]
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

Arrows indicate use/dependency; the dashed arrow indicates interface implementation. This is a layered solution with pragmatic outer-layer coupling: AccountController directly uses Identity and DbContext for transactional registration, and Web references Infrastructure for composition and development tooling. It is not a claim of strict Clean Architecture. Application's AddApplication currently adds no concrete use-case service; AddInfrastructure registers IAppointmentBooking, DbContext and TimeProvider.System. Domain has no framework or outer-layer dependencies. SQLite contains a People TPH hierarchy and separate catalog/appointment/Identity tables. Startup never applies migrations or seeds automatically.
