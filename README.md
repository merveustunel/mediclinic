# MediClinic

ASP.NET Core MVC / .NET 8 OOP course project for clinic appointments. M13 audits main including merged PR #18. **Submission remains conditional:** the original database needs an explicitly approved upgrade and initial administrator provisioning; live browser workflows remain unverified because the local HTTPS certificate is untrusted. See the [final requirements audit](docs/final-requirements-audit.md) and [submission checklist](docs/submission-checklist.md).

## Features and roles

- Clinic, multiple branches, departments, medical services with duration and decimal TRY prices.
- Scoped clinic/employee administration, doctor specialties, explicit service assignments, availability and branch working hours.
- Patient registration, login, dashboard, three-stage booking, owned details/history and reasoned cancellation.
- Department-scoped staff dashboard, confirmation/rejection and completion after the appointment ends.
- Appointment duration/price snapshots, overlap triggers and optimistic concurrency for decisions.

| Identity role | Access |
| --- | --- |
| Patient | Own appointments; requires linked Patient. Public registration grants only this role. |
| Staff | Department-scoped appointments and decisions; requires linked Staff. Doctors use this Identity role. |
| ClinicAdmin | Clinic/employee management requires explicit ClinicAdministrators grants. Clinical reads separately require a Staff link; clinical mutations additionally require Staff role. |

Doctor is a domain subtype, not a separate Identity role. A role or job title alone does not confer clinic ownership. The inspected local database has Patient/Staff accounts only, with no ClinicAdmin account. Bootstrap and demo seed never create clinic grants.

## Architecture and OOP

| Project | Responsibility |
| --- | --- |
| [Domain](src/MediClinic.Domain) | Entity/Person abstraction, inheritance and polymorphism; guarded private state and read-only collections. |
| [Application](src/MediClinic.Application) | Booking contracts, DTOs, Istanbul scheduling. |
| [Infrastructure](src/MediClinic.Infrastructure) | Booking, scoped clinical queries, Identity, EF Core SQLite and six migrations. |
| [Web](src/MediClinic.Web) | MVC/Razor, authentication/policies and scoped management transactions. |
| [Tests](tests/MediClinic.Tests) | Domain, database and Identity/MVC integration tests with temporary SQLite. |

See [OOP evidence](docs/oop-principles.md), [class UML](docs/uml/domain-class-diagram.md), [use cases](docs/uml/use-cases.md), [booking sequence](docs/uml/appointment-booking-sequence.md), [lifecycle](docs/uml/appointment-lifecycle.md) and [architecture](docs/uml/layered-architecture.md). Web directly uses DbContext for management/registration; this is a pragmatic layered design.

## Setup and verification

Run from the repository root with .NET SDK compatible with [global.json](global.json), Git and NuGet access. Node 18+ runs JavaScript tests; Python 3 runs optional database-safety tooling/tests. Linux needs Europe/Istanbul time-zone data. No frontend install/build is needed.

```powershell
dotnet tool restore
dotnet restore MediClinic.sln
dotnet build MediClinic.sln --no-restore
dotnet test MediClinic.sln --no-build --no-restore --logger 'trx;LogFileName=m13.trx'
node --test tests/booking-cascade.test.cjs
python tests/database-safety.test.py
dotnet ef migrations has-pending-model-changes --project src/MediClinic.Infrastructure --no-build
dotnet ef migrations list --project src/MediClinic.Infrastructure --no-build --no-connect
```

M13 results: build 0 warnings/errors; .NET **208 passed / 0 failed / 0 skipped** in approved Windows execution; JavaScript **5/0/0**; Python safety checks **5/0/0**. Restricted Windows Event Log failures and the successful retry are recorded in the [audit](docs/final-requirements-audit.md). Never disable security controls or treat failed/empty discovery as passing.

## Database safety and startup

The default runtime database is [src/MediClinic.Web/mediclinic.db](src/MediClinic.Web/mediclinic.db), resolved against the Web content root. The design-time factory resolves its default path from the repository root. Always specify an absolute connection path. Inspect process/deployment overrides before treating this as the active deployed database.

Existing databases must follow the [safe backup, rehearsal and approval-gated original migration runbook](docs/database-migration-runbook.md). Six migrations exist, through BookingIntegration. Startup, bootstrap and demo seed do not automatically migrate. Do not reset an existing database or use EnsureCreated.

For verification, use only the isolated migrated copy:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__MediClinic = 'Data Source=C:/Projects/mediclinic/.dist/m13/browser.db;Foreign Keys=True'
dotnet run --no-launch-profile --project src/MediClinic.Web -- --urls https://localhost:7443
```

Use a trusted local development HTTPS certificate (standard setup: `dotnet dev-certs https --trust`). Cookies remain Secure, HttpOnly and SameSite=Lax; do not downgrade to HTTP. Certificate trust was not changed by M13; browser navigation returned ERR_CERT_AUTHORITY_INVALID. Default HTTPS launch profile uses port 7222.

Supply credentials locally through environment configuration; never commit them. [Demo setup](docs/demo-verification.md) is explicit and Development-only; use synthetic accounts/data on a disposable copy. Existing demo passwords are preserved, so choosing a different seed password does not rotate them. Initial administrator identity and grant steps are in the [runbook](docs/database-migration-runbook.md#initial-administrator-provisioning).

## Scheduling and limitations

- Missing branch weekday hours are unrestricted for migration compatibility; explicitly configure all seven days to restrict the full week. Configured hours intersect doctor availability and conflicts. No clinic-level inherited schedule, holidays, split shifts or overnight branch hours.
- New appointments snapshot price and duration; legacy prices remain NULL/unknown, distinct from zero. No payments or historical billing reconstruction.
- One department per employee; transfers with history/availability are restricted. No global admin override, grant-management UI, deletion, rescheduling, notifications, recovery, MFA or forced initial-password rotation.
- Turkish UI includes some English framework errors/demo labels. Keyboard, screen-reader, responsive and live browser checks remain pending.
- SQLite contention may require retry. Completion is a staff action after EndsAt. Completed appointments continue to block their intervals.

Current design detail: [clinic management](docs/clinic-management.md), [staff management](docs/staff-management.md), [booking integration](docs/booking-integration.md), [authentication](docs/auth-design.md). Earlier milestone reports are historical; M13 audit/checklist supersede their totals, migration counts and readiness claims.
