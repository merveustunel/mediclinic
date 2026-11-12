# M11 employee, doctor services and availability management

> Historical M11 report. M12 integrates branch hours and price snapshots. The [M13 audit](final-requirements-audit.md) and [runbook](database-migration-runbook.md) supersede verification totals and operational instructions below. Bootstrap uses environment configuration; Web has no UserSecretsId.

Based on latest main `2b3fb1d`, including merged PR #16. Reuses the Domain staff/doctor/person/department/service/availability graph, Application scheduling conventions and booking contracts, Infrastructure Identity/EF context, MVC shell and migrated SQLite tests. Management follows M10's existing scoped Web-to-DbContext composition. No duplicate employee or organization models were introduced.

## Administrator access

**Çalışan yönetimi** requires both the Identity `ClinicAdmin` role and an explicit `ClinicAdministrators(UserId, ClinicId)` grant. Every employee, department, service and availability request derives scope from that grant. Submitted employee and interval IDs must match stored relationships. Out-of-scope requests return 404; an admin without grants has an empty list and cannot register employees. All mutations are POSTs protected by antiforgery validation. Public registration remains Patient-only.

Provision an initial verified administrator through the Development-only `--bootstrap-identity` command, supplying `Bootstrap:Email`, `Bootstrap:Password` and `Bootstrap:Role=ClinicAdmin` through local environment configuration (Web has no UserSecretsId). Never put credentials in command arguments or source control. Existing administrators need no new account. A trusted operator must then explicitly grant that verified account access to the intended clinic using the repeatable procedure in the [M13 runbook](database-migration-runbook.md#initial-administrator-provisioning). Neither startup, migration, employee creation nor demo seed grants administrator access.

## Employee behavior

Turkish forms list employees with roles, doctor specialties, clinic/branch/department and account emails. They support creation, editing and department selection within authorized clinics. The existing model has exactly one department per employee; branch and clinic derive from it. Existing staff IDs and account links are preserved. A person cannot switch between Doctor and Staff on edit. Reserved Identity roles cannot be selected as a general staff job title; a job title never grants an Identity role.

Names are required and length limited. Trimmed, case-insensitive duplicate names in the selected department are rejected inside the write transaction. This intentionally conservative course-project rule can reject distinct people sharing a name; employee identifiers would be a future extension. Email uniqueness and password policy are also enforced through Identity. Legacy duplicate records are not deleted or normalized.

Department transfers require authorization for both source and destination. Employees with any appointment history or availability are not transferred, preserving clinical history and department-based staff workflows. Unscheduled employees can transfer. Doctor assignments are replaced within the same transaction, with the new department persisted before inserting its services. Existing employees and appointments are never deleted by these screens.

Optional account creation accepts an email and an administrator-supplied initial password. Identity requires at least 12 characters with upper/lower case, a digit and a non-alphanumeric character. Only `Staff` is assigned, including for doctors; no Patient link, ClinicAdmin role or ownership grant is added. The Staff role is created if absent. Account, role link and employee changes are atomic; provisioning failure rolls everything back. Existing accounts and passwords are not changed. Passwords are never put in logs, TempData, HTML or validation summaries; failed forms clear the password. Share the initial password privately with the employee. Automated invitations, password recovery and forced first-login rotation are not included in this minimal course flow.

## Explicit doctor services and migration

Additive migration `20261112161952_StaffManagement` creates `DoctorServices(DoctorId, ServiceId)` with a composite primary key, service index and restrictive foreign keys. SQL backfills every existing Doctor/service pair in the same department, preserving previous booking eligibility. It adds compatibility triggers rejecting assignments to non-doctors or services in another department (and therefore another branch/clinic). No existing table is rebuilt; accounts, appointments, availability and demo rows are untouched.

New services and doctors have no automatic assignment. Administrators select zero or multiple compatible services. New booking options, slot discovery and booking submission all require an explicit assignment plus department compatibility. Removing an assignment stops new bookings while retaining existing appointments. The Domain's existing `CanProvide` remains a compatibility predicate for constructing and reading historical appointment graphs; booking uses the explicit `Offers` predicate.

**Manual migration only:** stop application writers and take a consistent SQLite backup, including WAL handling. Review the generated SQL against the selected database, then explicitly apply the migration:

```powershell
dotnet ef migrations script 20261112154350_ClinicManagement 20261112161952_StaffManagement --project src/MediClinic.Infrastructure
# After reviewing SQL and selecting the intended absolute database path:
dotnet ef database update --project src/MediClinic.Infrastructure --connection "Data Source=C:/absolute/path/mediclinic.db;Foreign Keys=True"
```

The path above is a placeholder; use the existing database's actual path. Do not delete/reset a database or use EnsureCreated. No migration was applied to the workspace database during development. Startup and seeds still do not apply migrations. A downgrade drops explicit assignments and restores old code's department-wide booking behavior; prefer a forward correction after deployment.

## Availability and concurrent booking

Administrators can view a doctor's intervals, filter by Istanbul date, add future intervals, edit future intervals and remove safe future intervals. Stored timestamps remain UTC ticks. Forms use `Scheduling.ToUtc` / `Scheduling.Local` and `Europe/Istanbul`; ambiguous or invalid local times are rejected. Start must be in the future, end after start, and intervals must not overlap. Adjacent intervals are allowed. Started/past intervals are immutable.

Changes and removals require every Pending or Confirmed appointment to remain fully covered by one remaining/replacement interval. Completed, Rejected and Cancelled appointments retain their history. Legacy uncovered active appointments conservatively prevent destructive changes until their coverage is resolved. All management checks and writes run within SQLite immediate write transactions.

Booking also holds its write lock from insertion through a final database check of assignment and coverage before commit. A schedule/service change between initial booking checks and insertion causes rollback rather than an invalid appointment. The deferred transaction retains the existing database overlap-trigger protection for simultaneous bookings. SQLite contention can still require refreshing/retrying; concurrent edits use the existing last-successful-write behavior.

## Verification and M12

Tests cover real Identity/MVC authorization and antiforgery, unlinked and cross-clinic administrators, employee/account creation and rollback, editing/duplicates, department transfers, multiple explicit services, all booking entry points, legacy backfill/history/account preservation, database assignment guards, overlap/future validation, Pending/Confirmed coverage, safe deletion, Istanbul date filters and schedule/assignment changes racing a booking. Existing booking/concurrency regression tests remain included. The empty-catalog test now removes its temporary join rows before temporary services; no application data is deleted.

Final verification: Windows and isolated WSL builds passed with **0 warnings / 0 errors**; EF reports **no pending model changes**. The full .NET suite passed **194/194, 0 failed, 0 skipped** in WSL (47 seconds). The JavaScript suite passed **5/5** on Windows. Windows Application Control blocked EF tooling and test discovery, so authoritative .NET execution uses the documented isolated Ubuntu WSL source copy, excluding database files, secrets and Windows build outputs. No security policy was changed. The workspace SQLite SHA-256 remained `92046304EE79DA7BAF971F3B8FF73A59519DA15C365415F765CC7A9F88088883`. Responsive forms use native labeled controls, fieldsets, table captions/headers and a horizontally scrollable table container. Physical browser and assistive-technology review remains manual.

M12 intersects configured branch hours with doctor availability and conflicts, and snapshots prices. Missing weekday hours remain unrestricted. Holidays, split branch hours, notifications, invitation/recovery flows, concurrency conflict UI and multi-department employment remain future extensions; no extra administrator privileges are implied.
