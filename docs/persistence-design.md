# Persistence foundation

Infrastructure uses EF Core SQLite and Design **8.0.22**, with the same version of the repository-local `dotnet-ef` tool. Domain remains dependent only on the base class library. Web registers a scoped `MediClinicDbContext` through `AddInfrastructure(connectionString)`.

## Materialization and encapsulation

EF constructor binding supports scalar properties, including private constructors, but cannot inject navigation objects into constructors. Existing Branch, Department, MedicalService, Staff, Doctor, StaffAvailability and Appointment constructors require navigation objects, so each now has an identity-only nonpublic materialization constructor. Person has a `private protected` identity-only constructor to support Staff. The original Entity constructor still validates identity. Clinic and Patient can use their existing scalar constructors.

These are the only Domain changes (plus null-forgiving initializers for properties populated during materialization). Public constructors, factories, guards, read-only properties and lifecycle methods remain unchanged. EF writes getter-only auto-property backing fields, including immutable scalar properties and navigations; collection navigations explicitly use `_branches`, `_departments` and `_services`. No public setters, framework attributes or EF dependencies were added. Persisted rows must be trusted: materialization bypasses navigation-dependent constructor checks.

## Tables and relationships

| Domain types | Storage |
| --- | --- |
| Clinic, Branch, Department, MedicalService | Separate tables with required parent FKs |
| Person → Patient, Staff → Doctor | One `People` table, explicit `PersonType` discriminator (TPH) |
| StaffAvailability | Required Staff FK and interval |
| Appointment | Required Patient, Doctor and MedicalService FKs, interval, status and decision reason |

TPH preserves polymorphic Person and Staff queries without joins across subtype tables. Subtype columns are nullable physically, with discriminator-aware checks requiring patient birth date, staff department/job title and doctor specialty/title. All IDs are caller-supplied GUID primary keys with `ValueGeneratedNever`; Entity itself is not an entity/table.

Computed FK getters dereference navigations and cannot be independently hydrated. They are ignored, with shadow properties (`ClinicKey`, `BranchKey`, `DepartmentKey`, `StaffKey`, `PatientKey`, `DoctorKey`, `ServiceKey`) stored in conventional `*Id` columns instead. Appointment.Branch and BranchId remain derived from Service.Department.Branch and are not duplicated in storage. Queries can filter a shadow key with `EF.Property<Guid>(entity, "DoctorKey")`, for example, or traverse mapped navigations.

Loading is explicit; there are no lazy-loading proxies. Include the graph needed by domain operations: an appointment's Branch requires `Include(a => a.Service).ThenInclude(s => s.Department).ThenInclude(d => d.Branch)`. A bare appointment query can update status without loading participants, but accessing participant-derived IDs requires those navigations. Use tracked graphs for updates: detached objects do not carry shadow FKs, so blindly calling Update on partially loaded/detached graphs is unsafe.

All relationships use `Restrict` on delete, including ownership. This preserves clinical references and avoids cascade cycles; future removal workflows must explicitly remove dependents in order. EF indexes FK columns by convention. Composite indexes support doctor/start, patient/start and staff/start queries. They are deliberately nonunique and provide no overlap or concurrency protection. Names are not unique business identifiers.

Required text has NOT NULL and nonblank checks; service duration must be positive; interval ends must follow starts. Appointment status is stored as an explicit enum string with an allowed-values check and required nonblank reasons for rejection/cancellation. Domain methods still enforce lifecycle transitions, same-department eligibility, trimmed text and future-birth-date rules. SQL checks do not replicate all domain logic or validate lifecycle history. TPH foreign keys reference People identifiers, so SQLite cannot enforce the referenced subtype with these simple FKs; use domain construction for typed associations. Direct SQL is not a supported booking interface.

## SQLite representations and limitations

- DateOnly uses EF Core 8's native SQLite mapping to ISO `TEXT` (`yyyy-MM-dd`), with no timezone conversion.
- DateTimeOffset uses UTC ticks in an `INTEGER`, preserving instants and 100 ns precision and allowing server-side comparisons/order/indexes. Original UTC offsets are intentionally lost; loaded values have offset zero. Appointment duration/end snapshots remain stored. Timezone presentation and recurring schedules need a separate future policy.
- TimeSpan duration is an integer count of ticks; GUIDs are SQLite TEXT.
- SQLite has dynamic typing and does not enforce declared string maximum lengths. No arbitrary length limits are introduced here.
- SQLite has limited concurrent writers and no native database-generated rowversion. There is no booking concurrency or overlap prevention in this foundation.
- Some schema changes require table rebuilds. Review generated migrations and back up the database before deployment. SQLite lacks EF idempotent migration scripts; apply migrations against a known schema or use a reviewed migration bundle.
- Not every DateTimeOffset member translates after conversion. Compare complete timestamp values for SQL range predicates; timezone/date-part calculations may need explicit application handling.

## Configuration and migration operations

Web reads `ConnectionStrings:MediClinic` from [appsettings.json](../src/MediClinic.Web/appsettings.json). Relative database paths are resolved against the Web content root. The default is `src/MediClinic.Web/mediclinic.db`; database, WAL and journal files are Git-ignored. Set `ConnectionStrings__MediClinic` to override, preferably with an absolute path for deployments. Foreign keys are explicitly enabled in the configured connection string.

The design-time factory avoids starting Web and never opens a database merely to construct the context. It accepts a connection string after `--`, then the environment override, then a repository-root-relative development default. Run these commands **from the repository root**:

```sh
dotnet tool restore
dotnet restore MediClinic.sln
dotnet ef migrations add MigrationName --project src/MediClinic.Infrastructure --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/MediClinic.Infrastructure
dotnet ef migrations script 0 InitialPersistence --project src/MediClinic.Infrastructure
# Explicitly create/update the local development database:
dotnet ef database update --project src/MediClinic.Infrastructure
# Explicit target override (create its parent directory first):
dotnet ef database update --project src/MediClinic.Infrastructure -- "Data Source=/absolute/path/mediclinic.db;Foreign Keys=True"
```

The initial migration and snapshot are versioned. Web does **not** call EnsureCreated or Migrate at startup in any environment. Production migration execution is a separate, deliberate deployment operation; a reviewed migration bundle can also be used. Integration tests keep a SQLite in-memory connection open, apply the real migrations, then exercise separate contexts for round trips, graph fixup, status updates, time queries and database constraint enforcement. They do not use EF's nonrelational InMemory provider.

Identity authentication and user-domain associations are documented in [auth-design.md](auth-design.md). Booking serialization and overlap rules remain out of scope.
