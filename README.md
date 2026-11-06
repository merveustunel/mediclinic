# mediclinic

Milestone 6 provides an explicitly opt-in, Development-only demo setup against the existing migrated SQLite database. See [Demo setup and verification](docs/demo-verification.md) for safe Windows commands, secret configuration, demo logins, and the complete patient/staff verification workflow. No database reset or automatic migration is performed.
Identity setup, roles, migration commands, and development bootstrap: [Authentication design](docs/auth-design.md).

Milestone 5 adds patient booking and appointment history, plus department-scoped staff review. See [Appointment booking design](docs/appointment-booking-design.md) for scheduling, UTC storage, SQLite concurrency protection, authorization, migration deployment and manual checks.

```sh
dotnet tool restore
dotnet build MediClinic.sln
dotnet test MediClinic.sln
dotnet ef database update --project src/MediClinic.Infrastructure
```

Run the MVC app over HTTPS and use **My appointments** (Patient) or **Review appointments** (linked Staff). Booking requires an existing compatible catalog and doctor availability. All local scheduling uses Europe/Istanbul; new bookings await staff confirmation. Apply migrations explicitly; startup never migrates automatically.
