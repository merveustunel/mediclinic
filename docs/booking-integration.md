# M12 booking integration

## Prices and legacy data

New appointments persist the service price at creation as a decimal rounded to two places (TRY). Duration continues to be snapshotted by StartsAt/EndsAt. Subsequent catalog edits do not change either snapshot. Patient and staff details show the same clinic, branch, department, doctor, service, duration, charge, Istanbul time and status within existing access scopes.

`20261112164410_BookingIntegration` only adds nullable `Appointments.Price` (decimal precision 11, scale 2; SQLite TEXT). Existing rows receive NULL: the historical charge is unknown, displayed as “Bilinmiyor (eski kayýt)”. NULL does not mean free. No catalog-based historical backfill is performed. New zero-priced services snapshot 0.00 TRY. SQLite does not itself enforce decimal scale; public domain creation rounds the catalog decimal to two places.

## Working hours

An explicitly configured closed day has no slots. An open day requires the entire appointment interval to fit between opening and closing, inclusive at the boundaries. Working hours do not support overnight intervals. All comparisons use Europe/Istanbul local days and hours; appointments and availability remain UTC instants.

For migration compatibility, **each missing weekday record is unrestricted**. This includes branches without any working-hours records. Doctor availability, explicit doctor-service assignment, future start, one-year horizon and overlap checks still apply. Configure all seven days for a fully restricted branch week. Existing appointments are not rewritten or silently cancelled. A new or changed hours record must preserve every Pending/Confirmed appointment it affects, including past records in those states. Cancel/reject affected appointments through authorized workflows before making incompatible changes.

Slot discovery and final booking call the same hours predicate. Booking INSERT holds the SQLite write lock through fresh assignment, availability, hours, catalog price and duration validation. Administrative transactions acquire the write lock before reading active appointments. Thus either administration commits first and booking revalidates, or booking commits first and administration sees its active appointment. SQLite busy/overlap failures in booking produce a retry message. Existing availability edits/removal protect active coverage; service removal now protects active service assignments. Administrative concurrent writes serialize; last valid edit wins (no optimistic revision editor).

## Migration order and operator steps

1. Stop writers and back up the existing SQLite file and any WAL/SHM files using a SQLite-safe backup procedure.
2. Review existing migration history. Apply in order: InitialPersistence › IdentityAuthentication › AppointmentScheduling › ClinicManagement › StaffManagement › BookingIntegration. EF applies only pending migrations. Legacy overlaps can block AppointmentScheduling; resolve clinical history manually rather than deleting records.
3. Review the generated upgrade SQL on a copy before using `dotnet ef database update --project src/MediClinic.Infrastructure --startup-project src/MediClinic.Web` against the explicitly configured intended database.
4. Verify row counts, foreign keys, historical prices remaining unknown, and historical duration/status preservation on that copy.

No migration or seed was run against the user's database during this implementation. Application startup does not apply migrations or reseed. Do not downgrade this migration after collecting price snapshots: Down drops the snapshot column.

## Manual verification still required

- After operator-controlled migration, log in as patient, staff and scoped clinic administrator on a copy of production data.
- Verify all three booking steps, service duration/TRY prices, closed-day and empty-slot messages, exact opening/closing boundaries and both appointment detail pages.
- Change a catalog price after booking and verify the original charge remains. Verify legacy unknown prices remain distinct from zero.
- Attempt hours, availability and assignment edits with Pending and Confirmed bookings; check rejection feedback and allowed compatible edits.
- Exercise independent browser sessions booking/editing schedules concurrently, including SQLite busy retry behavior. Test keyboard navigation and mobile layout.
