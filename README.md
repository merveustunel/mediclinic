# MediClinic

MediClinic is an ASP.NET Core MVC clinic appointment application prepared as an **Object-Oriented Programming course project**. It demonstrates an encapsulated domain model, inheritance, polymorphism, persistent relationships and role-authorized workflows. M8/M9 stabilizes and documents the existing application for final submission without adding product modules.

## Features and user roles

- Patient registration, login and POST logout using ASP.NET Core Identity.
- Patient dashboard with summary counts and next upcoming appointment.
- Three-stage booking: clinic → branch → department → doctor → service, Istanbul date/time selection, then request review. Native forms also work without JavaScript.
- Patient appointment history, status filters, owned details and cancellation with a reason.
- Staff dashboard, department-scoped appointment list, status/date filters and details.
- Staff confirmation, rejection with reason and completion at or after the scheduled end.
- SQLite persistence, explicit EF migrations, overlap triggers and optimistic concurrency for decisions.

| Identity role | Access |
| --- | --- |
| Patient | Own dashboard, booking, history/details/cancellation; requires linked Patient. |
| Staff | Department dashboard/list/details and review; requires linked Staff. |
| ClinicAdmin | Department-scoped dashboard/list/details when linked to Staff. Does not implicitly receive Staff mutation permissions. The bootstrap creates an unlinked admin, which cannot read clinical data. |

Doctor is a domain class, not an Identity role. Identity permissions are separate from Person.Role. All requested patient/staff features were found implemented; see the [code-backed final audit](docs/final-audit.md).

## Technology and solution architecture

.NET 8 / C# 12, ASP.NET Core MVC and Razor, ASP.NET Core Identity 8.0.22, EF Core SQLite 8.0.22, xUnit 2.9.3 and WebApplicationFactory, with plain JavaScript/CSS. No frontend package installation or build is needed.

| Project | Responsibility |
| --- | --- |
| [Domain](src/MediClinic.Domain) | Entity/Person hierarchy, clinic catalog, appointment state machine and invariants; framework independent. |
| [Application](src/MediClinic.Application) | IAppointmentBooking, request/read DTOs and Istanbul scheduling conversion. |
| [Infrastructure](src/MediClinic.Infrastructure) | Booking/query implementation, Identity model, DbContext, SQLite mappings/migrations. |
| [Web](src/MediClinic.Web) | MVC, Identity configuration, policies, UI and explicit Development tooling. |
| [Tests](tests/MediClinic.Tests) | Domain, persistence and MVC integration tests using isolated SQLite databases. |

Web composes services and accesses Identity/DbContext directly for transactional registration. This is a pragmatic layered design. See the [architecture diagram](docs/uml/layered-architecture.md).

## OOP principles

Abstract Entity supplies identity; abstract Person supplies shared names and the Role contract. Patient and Staff inherit Person; Doctor inherits Staff. Overridden Role properties demonstrate runtime polymorphism. Private setters, guarded business methods and read-only collections protect encapsulation. Clinic/Branch/Department own catalog children; Appointment associates Patient, Doctor and MedicalService. See [OOP principles with actual methods/tests](docs/oop-principles.md) and [domain UML](docs/uml/domain-class-diagram.md).

## Setup requirements and development configuration

Run commands from the repository root. Install the **.NET 8 SDK** compatible with [global.json](global.json) (8.0.425, latestPatch), Git and a trusted HTTPS development certificate. First restore needs NuGet access. Linux needs time-zone data for Europe/Istanbul. Node.js 18+ is optional for the JavaScript regression tests.

```powershell
dotnet --version
dotnet tool restore
dotnet restore MediClinic.sln
dotnet build MediClinic.sln --no-restore
dotnet dev-certs https --trust
```

On Linux, trust an appropriate local certificate using your platform's process. Authentication cookies are always Secure; use HTTPS for demos. The HTTPS profile is in [launchSettings.json](src/MediClinic.Web/Properties/launchSettings.json).

Web's default `Data Source=mediclinic.db;Foreign Keys=True` resolves against the Web content root. Migration tooling defaults to `src/MediClinic.Web/mediclinic.db` relative to the repository root. Use an absolute override so both target the same database. In PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$databasePath = Join-Path (Get-Location) 'src/MediClinic.Web/mediclinic.db'
$env:ConnectionStrings__MediClinic = "Data Source=$databasePath;Foreign Keys=True"
```

Settings: [appsettings.json](src/MediClinic.Web/appsettings.json), [appsettings.Development.json](src/MediClinic.Web/appsettings.Development.json). Supply secrets locally through environment variables; Web does not declare UserSecretsId. Do not commit passwords/databases. Public registration creates only Patient accounts. See [privileged Development bootstrap](docs/auth-design.md).

## Database and migration instructions

For an existing database, stop writers and make a consistent SQLite backup first, handling WAL correctly. Preserve records; do not delete/reset the database or replace migrations with EnsureCreated.

```powershell
# Inspect without connecting to the application database.
dotnet ef migrations list --project src/MediClinic.Infrastructure --no-connect
dotnet ef migrations has-pending-model-changes --project src/MediClinic.Infrastructure
# Explicitly update the selected local database.
dotnet ef database update --project src/MediClinic.Infrastructure
```

Three migrations exist: InitialPersistence, IdentityAuthentication and AppointmentScheduling. The last adds overlap triggers and refuses legacy overlapping blocking appointments instead of deleting history. Investigate such data before migrating. Startup/seeding never migrate automatically. A fresh database has no bookable catalog/availability until provisioned; the optional demo adds them.

## Application startup

Keep the Development/connection variables in the same terminal:

```powershell
dotnet run --project src/MediClinic.Web --launch-profile https
```

Open `https://localhost:7222`. Alternatively:

```powershell
dotnet run --no-launch-profile --project src/MediClinic.Web -- --urls https://localhost:7043
```

The explicit-port command needs ASPNETCORE_ENVIRONMENT set as above; without a launch profile the environment defaults to Production. Neither command implicitly seeds data.

## Test commands

```powershell
dotnet build MediClinic.sln
dotnet test MediClinic.sln --no-restore --logger "trx;LogFileName=m8-m9.trx"
node --test tests/booking-cascade.test.cjs
```

Tests use temporary SQLite databases and real migrations, not the local demo database. TRX stays in the ignored test-project TestResults directory. See [final evidence and Windows fallback](docs/submission-checklist.md). Do not change security policy or count aborted discovery as passing.

## Demo verification

Follow [safe demo setup](docs/demo-verification.md) to supply locally chosen passwords and run `--seed-demo` explicitly against a migrated Development database. It preserves existing records/credentials and adds weekday availability over a rolling 14-day horizon. The `@example.test` accounts are synthetic; the repository supplies no password.

Use **Hasta paneli → Randevu oluştur**, select the demo catalog, then **Tarih ve saate devam et → Uygun saatleri göster → Talep özetini gör → Randevu talebini gönder**. Staff uses **Personel paneli → Randevu inceleme → Detayları görüntüle**. Verify confirmation, cancellation, rejection, isolation and completion after EndsAt using the [current walkthrough](docs/submission-checklist.md#manual-demo-walkthrough). Earlier milestone docs retain historical English labels/test totals; this README and the final checklist describe the submission.

## Documentation index

- [Final feature and quality audit](docs/final-audit.md)
- [Submission checklist, test evidence and manual demo](docs/submission-checklist.md)
- [OOP principles](docs/oop-principles.md)
- UML: [classes](docs/uml/domain-class-diagram.md), [use cases](docs/uml/use-cases.md), [lifecycle](docs/uml/appointment-lifecycle.md), [booking sequence](docs/uml/appointment-booking-sequence.md), [layers](docs/uml/layered-architecture.md)
- Design history: [domain](docs/domain-model.md), [persistence](docs/persistence-design.md), [authentication](docs/auth-design.md), [booking](docs/appointment-booking-design.md), [demo](docs/demo-verification.md)
- UI: [shared](docs/ui-design.md), [patient dashboard](docs/patient-dashboard-design.md), [patient appointments](docs/patient-appointments-ui-design.md), [patient booking](docs/patient-booking-ui-design.md), [staff dashboard](docs/staff-dashboard-design.md), [staff appointments](docs/staff-appointments-ui-design.md)

## Known limitations

- Course scope excludes email verification/recovery, MFA, public privileged registration, catalog/availability administration UI, payments, notifications and rescheduling.
- ClinicAdmin has no global clinical override; default bootstrap admin is unlinked. Clinical reads require a Staff link and remain department-scoped.
- Navigation/statuses/booking/staff feedback and patient cancellation are Turkish. Some default Identity/DataAnnotations/model-binding registration errors and synthetic demo labels remain English; full localization is incomplete.
- Completion is a staff action, not automatic. Pending/Confirmed cancellation and Pending review have no scheduled-start cutoff. Completed appointments still block their intervals.
- SQLite concurrent writes may require refresh/retry. Trusted provisioning must preserve role/person link semantics.
- Automated MVC tests do not establish visual accessibility or browser compatibility. Final keyboard, screen-reader, responsive and interactive demo checks remain in the checklist. This milestone did not seed, migrate or modify the existing application database.
