# SQLite migration and initial provisioning runbook

M13 rehearsed M10–M12 against an online backup, never the original. Results and counts are in the [final audit](final-requirements-audit.md). Commands assume the repository root `C:/Projects/mediclinic`. Python 3 is required for the optional [operator tool](../tools/database-safety.py); it uses only the standard library.

## Locate and inspect without changing data

[Program](../src/MediClinic.Web/Program.cs) resolves relative runtime Data Source against the Web content root; the [design-time factory](../src/MediClinic.Infrastructure/Persistence/MediClinicDbContextFactory.cs) defaults relative to the repository root. Configuration in [appsettings](../src/MediClinic.Web/appsettings.json) selects `mediclinic.db`. No connection/environment override was present in the audit shell. The inspected original is **C:/Projects/mediclinic/src/MediClinic.Web/mediclinic.db**. Other running/deployed processes can have different overrides; inspect their launch configuration separately.

```powershell
python tools/database-safety.py inspect C:/Projects/mediclinic/src/MediClinic.Web/mediclinic.db
dotnet ef migrations list --project src/MediClinic.Infrastructure --no-build --no-connect
```

Inspection opens SQLite with URI `mode=ro` and reads history, counts, integrity, foreign keys, account identifiers and grants in a snapshot transaction. Keep output private. It does not print passwords or hashes from Identity rows. Do not use `immutable=1` on a live WAL database: committed data can still be in WAL.

## Consistent backup and isolated rehearsal

The original uses WAL. **Copying the main file alone while writers are active is unsafe.** The tool opens the source read-only and uses SQLite's online backup API, incorporating committed WAL frames into a consistent destination. Destination creation is exclusive: an existing file or the source itself cannot be overwritten. No source checkpoint, VACUUM, migration or seed is issued. Backup integrity/FK failures stop the command; retain and investigate any failed destination, and use a new name for a retry.

Prefer stopping all application writers for the whole backup/rehearsal/migration window. If an online backup is needed, it represents the committed snapshot captured by SQLite, not later transactions. Keep backup files under the same restricted permissions as the source. Do not publish inventory or backup files. A raw filesystem alternative requires all SQLite connections closed and a verified consistent set of the main file and any remaining `-wal`/`-shm` sidecars; never discard WAL to make a backup work. The online backup needs no copied sidecars.

The following is safe rehearsal only. Use a new timestamp for each attempt:

```powershell
$original = 'C:/Projects/mediclinic/src/MediClinic.Web/mediclinic.db'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = "C:/Projects/mediclinic/.dist/m13/before-$stamp.db"
$rehearsal = "C:/Projects/mediclinic/.dist/m13/rehearsal-$stamp.db"
New-Item -ItemType Directory -Force C:/Projects/mediclinic/.dist/m13 | Out-Null
python tools/database-safety.py backup $original $backup
if ($LASTEXITCODE -ne 0) { throw 'Backup failed; stop.' }
python tools/database-safety.py backup $backup $rehearsal
if ($LASTEXITCODE -ne 0) { throw 'Rehearsal copy failed; stop.' }
dotnet ef migrations script 20261106022559_AppointmentScheduling 20261112164410_BookingIntegration --project src/MediClinic.Infrastructure --no-build --output "C:/Projects/mediclinic/.dist/m13/upgrade-$stamp.sql"
if ($LASTEXITCODE -ne 0) { throw 'SQL generation failed; stop.' }
# Review this SQL and the current history. This baseline applies to the audited DB.
dotnet ef database update 20261112164410_BookingIntegration --project src/MediClinic.Infrastructure --no-build --connection "Data Source=$rehearsal;Foreign Keys=True"
if ($LASTEXITCODE -ne 0) { throw 'Rehearsal migration failed; stop.' }
python tools/database-safety.py verify $backup $rehearsal
if ($LASTEXITCODE -ne 0) { throw 'Preservation or integrity verification failed; stop.' }
```

If history differs, generate SQL from its actual last migration and inspect it. The verification tool requires an ordered history prefix and the exact current six-migration target. It compares every original table/column digest except migration history (including Identity hashes/links without outputting those values), checks all row counts implicitly through full row comparison, NULL legacy appointment prices, zero-default legacy service prices, exact compatible assignment backfill, `integrity_check` and `foreign_key_check`. It must run **before provisioning or workflow writes**, which intentionally alter data. Keep an untouched backup/rehearsal and a separate browser copy.

Migration order:

1. `20261105220707_InitialPersistence`: clinical graph, checks and restrictive FKs.
2. `20261105223217_IdentityAuthentication`: Identity schema and person links.
3. `20261106022559_AppointmentScheduling`: refuses legacy blocking overlaps, then installs insert/update overlap triggers. Never delete history to force it through.
4. `20261112154350_ClinicManagement` (M10): service Price defaults to 0 TRY; empty grants and working hours.
5. `20261112161952_StaffManagement` (M11): explicit DoctorServices, compatible department backfill and assignment guards.
6. `20261112164410_BookingIntegration` (M12): nullable appointment price; legacy charges remain unknown. No invented billing backfill.

## Original upgrade: only after explicit user approval

**This section was not executed during M13.** Request explicit approval for the verified original path, reviewed SQL and maintenance window. Keep writers stopped from the new backup until final validation; repeat the rehearsal if data or code changed. Do not copy the rehearsal/browser database over the original.

After approval, in the same terminal holding the freshly verified `$original` and `$backup` values above:

```powershell
dotnet ef database update 20261112164410_BookingIntegration --project src/MediClinic.Infrastructure --no-build --connection "Data Source=$original;Foreign Keys=True"
if ($LASTEXITCODE -ne 0) { throw 'Original migration failed; keep writers stopped and investigate.' }
python tools/database-safety.py verify $backup $original
if ($LASTEXITCODE -ne 0) { throw 'Validation failed; keep writers stopped.' }
python tools/database-safety.py inspect $original
```

Review output before reopening the application. Preserve the backup; do not automatically run Down migrations, reset, reseed, restore over files or remove sidecars on failure. M12 Down discards collected snapshot prices; M11 Down drops assignments; M10 Down drops grants/hours/prices. Prefer a reviewed forward correction. A restore requires its own explicit decision, stopped connections, and coherent SQLite file handling.

## Initial administrator provisioning

ClinicManagement and EmployeeManagement require both the `ClinicAdmin` Identity role and a `ClinicAdministrators(UserId, ClinicId)` grant. Clinical appointment reads still require a Staff link; mutation requires Staff role. Initial bootstrap creates an unlinked administrator, with no global clinical privilege. Revoking a clinic grant takes effect on the next management request.

**Actual finding:** the original has 3 users and 2 roles (Patient/Staff), no ClinicAdmin account, and no grant table until M10. There is no existing administrator to silently repair. A trusted operator must identify the intended owner through an external verification process. Do not promote an arbitrary existing Patient/Staff account.

If a verified ClinicAdmin account already exists in the target, reuse its stored ID. Otherwise, after separate explicit provisioning authorization, create a **new verified administrator** using [IdentityBootstrap](../src/MediClinic.Web/IdentityBootstrap.cs). It requires Development, at least 12 characters with Identity's upper/lower/digit/symbol requirements, and an already migrated database. It is a one-time account creation command: repeating it for the same email fails safely rather than resetting credentials. Supply secrets locally, not in arguments/source:

```powershell
$env:ConnectionStrings__MediClinic = "Data Source=$original;Foreign Keys=True"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
# Set Bootstrap__Email and Bootstrap__Password privately to the verified owner's values.
$env:Bootstrap__Role = 'ClinicAdmin'
dotnet run --no-build --no-launch-profile --project src/MediClinic.Web -- --bootstrap-identity
if ($LASTEXITCODE -ne 0) { throw 'Bootstrap failed; investigate without resetting accounts.' }
Remove-Item Env:Bootstrap__Password -ErrorAction SilentlyContinue
python tools/database-safety.py inspect $original
# Assign these only after checking the exact stored user and clinic IDs from private inspection.
$verifiedUserId = Read-Host 'Verified existing ClinicAdmin user ID'
$verifiedClinicId = Read-Host 'Verified existing clinic ID'
python tools/database-safety.py grant $original --user-id $verifiedUserId --clinic-id $verifiedClinicId --apply-verified-grant
if ($LASTEXITCODE -ne 0) { throw 'Grant failed; no fallback privileges allowed.' }
```

The parameterized grant tool opens only an existing database (`mode=rw`), locks its transaction before checks, requires the existing normalized CLINICADMIN role and existing clinic, adds only the selected pair, validates FKs and commits atomically. Repeating the same verified grant inserts 0 rows. It never assigns roles, creates accounts or links an admin to a clinical person. Use the same verification and private audit trail for any later grant. A completely empty installation also needs an explicitly provisioned initial Clinic before grants; the management UI deliberately has no first-owner bypass.

M13 test: bootstrap of a clearly synthetic `@example.test` administrator on `.dist/m13/browser.db` succeeded; grants inserted **1 then 0**. No original account changed. Python tests reject non-admin, unknown/injection-shaped user IDs and unknown clinics, including under optimized Python (`-O`); WAL backup and overwrite refusal also pass. Public web grant management is intentionally absent. Before using the original, complete the explicit identity/ownership approval and migration steps above.
