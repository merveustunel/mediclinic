# Appointment booking and scheduling

## Architecture

`IAppointmentBooking` and its request/read models live in Application. The Infrastructure implementation uses the existing EF Core/Identity context and domain constructors and lifecycle methods. MVC controllers bind explicit input allowlists and call this boundary with the authenticated Identity subject identifier. Domain remains framework independent and unchanged. The implementation does not add repositories that duplicate EF tracking or alter existing domain relationships.

Patient pages: `/Appointments`, `/Appointments/Book`, `/Appointments/Details/{id}`. Staff pages: `/StaffAppointments` and `/StaffAppointments/Details/{id}`. Navigation is role-aware. All mutations are POST with antiforgery validation; validation and stale-slot failures produce messages. Cross-scope details return 404. No patient names, birth dates or account details are included in the appointment read model.

## Scheduling rules

- The booking page lists compatible clinic / branch / department / doctor / service combinations. Doctor eligibility uses the existing `Doctor.CanProvide` department rule; specialty is descriptive. All submitted relationship identifiers are checked again at booking.
- The current catalog has **no active/inactive property**. All existing catalog entries are available for discovery. No artificial activation flag, price, or clinical qualification model is introduced. Availability is required to book any entry.
- Scheduling uses **Europe/Istanbul** for every branch. Submitted wall times are interpreted in that named zone, independent of server/browser timezone. Ambiguous or invalid local times are rejected. This is an explicit single-zone convention, not a per-branch timezone model.
- Booking must start strictly after the trusted server time and within the next year. Slot discovery shows 15-minute start increments within the selected local day, excluding busy periods. The write path accepts any start inside a covering availability interval; the discovery grid is a convenience, not an extra clinical invariant.
- The entire positive service duration must fit inside one `StaffAvailability` interval. Adjacent availability intervals are not stitched together. End time uses the existing domain calculation. Exact availability boundaries are accepted.
- Stored starts/ends use the existing UTC-ticks INTEGER converter, including full tick precision. UI displays local time and explicitly identifies the zone. The existing stored start/end pair snapshots booked duration, independent of subsequent catalog changes. The model has no service price, so no historical price can be captured.
- New appointments are Pending. Only Pending can be confirmed/rejected. Rejection and patient cancellation require a nonblank reason; the MVC input limit is 2000 characters. Pending and Confirmed can be cancelled, including after their start because the existing domain permits it. Only Confirmed can complete, at/after its scheduled end using trusted server time. Terminal states remain terminal.

## SQLite overlap and concurrency boundary

Doctor reservations use half-open intervals `[start,end)`: overlap is `existing.StartsAt < new.EndsAt && new.StartsAt < existing.EndsAt`. Adjacent appointments are allowed. **Pending, Confirmed and Completed block**; Rejected and Cancelled do not. Completed remains blocking to preserve historical occupancy, even though new bookings must be in the future.

The application precheck improves feedback, but is not the concurrency guarantee. Migration `20261106022559_AppointmentScheduling` installs SQLite BEFORE INSERT and BEFORE UPDATE triggers on Appointments. They reject overlapping blocking rows for the same doctor using `RAISE(ABORT, 'appointment_overlap')`. SQLite serializes writers, so the overlap predicate and write execute within the same database write boundary, including across different contexts/processes. Both newly blocking statuses and changed intervals/doctors are checked. This is not a process-local lock or a uniqueness index that only prevents identical starts.

EF uses Status as an optimistic concurrency token: concurrent staff/patient decisions cannot silently overwrite each other. Conflicts and SQLite busy/locked write failures become retry/refresh messages. There is no automatic business-operation retry. Slot lists can become stale; booking revalidates and the trigger decides the final write.

The migration first refuses any legacy overlapping blocking appointments, preserving the database and its clinical history rather than deleting or changing rows. Back up before deploying; inspect and explicitly resolve legacy conflicts before retrying. Existing applied migrations are untouched. This migration adds triggers and updates concurrency metadata without rebuilding existing tables. Future migrations that rebuild Appointments must preserve/reinstall its custom triggers. `EnsureCreated` is not a supported deployment path because custom SQL triggers come from migrations.

## Authorization boundaries

Every service operation looks up the Identity subject, its stored role and typed domain association. Public booking inputs contain no patient identity or role. The MVC booking endpoint also rejects submitted PatientId, StaffId, Role, Roles, Status and EndsAt fields. Read/write queries always filter by the linked patient for patient operations.

The narrow staff policy is **linked department only**. A Staff role must have a linked domain Staff (Doctor is a Staff); only appointments whose service belongs to that Staff's immutable Department can be listed, viewed, confirmed, rejected or completed. This also confines access to that department's branch and clinic. Staff role alone, an unlinked staff account, or ClinicAdmin does not authorize clinic-wide access. No broad administrative override is provided. There is no public mapping/provisioning endpoint; use the existing trusted bootstrap/provisioning boundary for staff links. Person.Role is never an authorization claim.

## Verification

Run from the repository root:

```sh
dotnet tool restore
dotnet build MediClinic.sln
dotnet test MediClinic.sln
dotnet ef migrations has-pending-model-changes --project src/MediClinic.Infrastructure
dotnet ef database update --project src/MediClinic.Infrastructure
```

Tests use real migrated SQLite databases, not EF InMemory. File-backed concurrency tests synchronize two separate requests at SaveChanges after both prechecks have passed; exactly one booking succeeds. Other coverage includes raw write trigger protection, concurrent staff decisions, old-database upgrade preservation, duration snapshots, compatible relationships, availability, adjacent slots, blocking/released statuses, dates, reasons, completion timing and scoped authorization. MVC tests use real Identity cookie login and antiforgery tokens.

Manual checks (use a separate development database):

1. Apply migrations explicitly. Provision a clinic/branch/department/service graph, a doctor in that department and a future `StaffAvailability` through trusted domain/persistence tooling. The application deliberately has no public catalog/availability editing or demo-data endpoint.
2. Register and log in a patient over HTTPS. Open My appointments, choose a compatible combination and local date, load times, then request a slot. Verify Pending status, local branch/doctor/service/time and booked duration.
3. Use two separate patient sessions to request the same slot; only one may reserve it. Confirm adjacent intervals remain bookable. Verify a second patient cannot read or cancel the first patient's appointment by changing its URL/form ID.
4. Provision a Staff account linked to that department using the existing development bootstrap. Review and confirm the request. Early completion must fail. Reject a separate Pending request with a reason, or cancel as its patient, and verify the released slot becomes available.
5. Log in a Staff member linked to another department/branch/clinic and verify the appointment is absent and direct details/actions do not grant access. Verify an unlinked Staff/ClinicAdmin cannot obtain broad clinical access.
6. Submit mutations without antiforgery tokens and verify rejection. Submit invalid dates, incompatible identifiers or missing reasons and verify useful validation without creating/changing an appointment.

Windows may block unsigned test assemblies with code-integrity error `0x800711C7`. Do not change Smart App Control or system policies. Use an available Linux/WSL SDK and an independent source copy to avoid mixing Windows/Linux build outputs; report each environment's actual results in the PR.

### Milestone 5 validation results (2026-11-06)

- Ubuntu WSL, .NET SDK 8.0.427: `dotnet build MediClinic.sln` succeeded with zero warnings/errors; `dotnet test MediClinic.sln` passed **117/117**, no skips (85 existing plus 32 new cases).
- Windows, .NET SDK 8.0.425: final build succeeded with zero warnings/errors; the final test run outside the sandbox passed **117/117**, no skips.
- Earlier Windows attempts failed: the baseline run had 72 passed/13 failed with assembly code-integrity error `0x800711C7`; a later sandbox run had 99 passed/17 failed with denied Windows Event Log access. These were not reported as successful runs. No security settings or system policies were modified.
- EF `migrations has-pending-model-changes` succeeded with no pending model changes. The relational tests apply all migrations to fresh databases and verify that upgrading an Identity-era database preserves appointments. A conflicting legacy database refuses the new migration without changing its appointment rows.
