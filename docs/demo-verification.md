# Development demo setup and verification

Run all commands from the repository root in PowerShell. Use only a local development database. Stop the MVC server and other database writers before seeding; run one seeder at a time. The command adds data in one transaction, exits without starting a server, and never resets, deletes, or migrates the database. The three existing migrations remain unchanged; no new migration is needed.

## Seed the existing Windows database safely

The existing default database is `C:\Projects\mediclinic\src\MediClinic.Web\mediclinic.db`. If your migrated database lives elsewhere, replace the absolute path below with that exact path. Check the file first so a typo cannot silently seed a new database. With the server stopped, make a backup before the first run; if WAL files exist, use SQLite's backup facility or checkpoint/close all connections before copying.

```powershell
Set-Location C:\Projects\mediclinic
$dbPath = 'C:\Projects\mediclinic\src\MediClinic.Web\mediclinic.db'
if (-not (Test-Path -LiteralPath $dbPath -PathType Leaf)) { throw 'Existing database not found; check the path.' }
Copy-Item -LiteralPath $dbPath -Destination ($dbPath + '.before-demo-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.bak')
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__MediClinic = "Data Source=$dbPath;Foreign Keys=True"
# Enter locally chosen passwords without placing their values in shell history.
$env:Demo__PatientPassword = [System.Net.NetworkCredential]::new('', (Read-Host 'Demo patient password' -AsSecureString)).Password
$env:Demo__StaffPassword = [System.Net.NetworkCredential]::new('', (Read-Host 'Demo staff password' -AsSecureString)).Password
try {
    dotnet run --no-launch-profile --project src/MediClinic.Web -- --seed-demo
    if ($LASTEXITCODE -ne 0) { throw 'Demo setup failed; inspect the error before retrying.' }
} finally {
    Remove-Item Env:Demo__PatientPassword, Env:Demo__StaffPassword -ErrorAction SilentlyContinue
}
```

Both secret values are required on every invocation. Identity requires at least 12 characters, uppercase and lowercase letters, a digit and a symbol. Never put values in committed settings, scripts, CLI arguments, or documentation. No credentials are supplied by this repository. Environment variables exist in process memory; use a trusted local terminal. An alternative is the .NET user-secrets provider configured locally for the Web project; its keys are `Demo:PatientPassword` and `Demo:StaffPassword` (the project does not currently declare a UserSecretsId). Environment variables above work without modifying the project.

If any migrations are missing, the seeder refuses to proceed. Review and explicitly apply the existing migrations using the same connection string if needed:

```powershell
dotnet tool restore
dotnet ef database update --project src/MediClinic.Infrastructure
```

Seeding is not normal startup configuration: only `--seed-demo` invokes it. Production, Staging, and Testing environments reject it before accessing the database. Demo identities also cannot sign in outside Development. Never deploy this development database or these secrets to production.

## Demo catalog and accounts

| Login | Password source | Identity role | Domain link / review scope |
| --- | --- | --- | --- |
| demo.patient@example.test | Demo__PatientPassword | Patient | Demo Patient |
| demo.staff.central@example.test | Demo__StaffPassword | Staff | Central reviewer / General Medicine |
| demo.staff.riverside@example.test | Demo__StaffPassword | Staff | Riverside reviewer / Cardiology |

The clinic is **MediClinic Demo**. Central has General Medicine, doctors **Deniz Demo** and **Ece Demo**, and a 30-minute General Medicine consultation. Riverside has Cardiology, doctor **Can Demo**, and a 30-minute Cardiology consultation. Each department has a linked non-doctor reviewer. There is no administrative override or ClinicAdmin demo account.

Every doctor receives 09:00–17:00 Europe/Istanbul availability on weekdays in the next 14 calendar days, starting tomorrow. Re-run to extend that rolling horizon. Stable, namespaced IDs identify demo records and each doctor/date availability; the same day's repeated run adds nothing. Existing passwords, users, appointments, and availability remain untouched. Changed secrets on a later run do not rotate existing passwords. Email/role/link collisions fail and roll back rather than taking over an account. Invalid passwords and other failures roll back the entire seed transaction. Do not manually reuse demo IDs or alter demo relationships. Old availability is retained to preserve history. No appointments are pre-created.

## Start and verify manually

Keep the Development and connection-string variables from above in your terminal. Configure/trust the local HTTPS certificate if necessary:

```powershell
dotnet dev-certs https --trust
dotnet run --no-launch-profile --project src/MediClinic.Web -- --urls https://localhost:7043
```

Open `https://localhost:7043`. Use separate browser profiles/private sessions for patient and staff so their cookies do not replace each other. All displayed dates/times are Europe/Istanbul.

1. Log in as `demo.patient@example.test` using the patient password you supplied. Open **My appointments**, then book. Select the combined clinic / branch / department / doctor / service option for MediClinic Demo / Central / General Medicine / Deniz Demo / General Medicine consultation. Select an upcoming weekday within the next 14 days and click **Show available times**. Choose a time and **Request appointment**. Verify **Pending**, the correct doctor/branch/service and 30-minute duration. Save the details URL and appointment ID.
2. In another profile, log in as `demo.staff.central@example.test`. Open **Review appointments**, find the request, open its details and **Confirm**. Refresh the patient's details/history and verify **Confirmed**.
3. As the patient, cancel that Confirmed appointment with reason `Plans changed`. Verify **Cancelled** and the reason. Pending appointments are also eligible; terminal appointments are not. Reload available times and verify the released time can be selected again.
4. Request another appointment at the released time. As Central staff, attempt **Reject** without a reason (must remain Pending), then reject with `Doctor unavailable`. Refresh the patient details and verify **Rejected** and the reason. Verify the slot is released.
5. Book a new Pending appointment. Keep an already loaded booking form for the same doctor/time in another tab. Submit it after the first succeeds: a reservation/refresh error must appear, with only one blocking appointment. Reload slots: overlapping starts must be absent, while adjacent 30-minute appointments may be booked. For two different patients racing, register a second patient through the normal registration form and submit both preloaded forms for the same time. Exactly one may succeed. SQLite triggers protect the database even if both forms passed discovery before either submitted.
6. While logged out, open the saved patient URL and `/StaffAppointments`: expect a login redirect. As the patient, open `/StaffAppointments`: expect AccessDenied (403 after following the redirect). As Riverside staff, open the Central staff details URL: expect 404, and the Central request must be absent from the list. A second registered patient must get 404 for the original patient's details and cannot cancel it by submitting its ID. Staff cannot book through the patient endpoint. Mutations without an antiforgery token must return 400.

If there are no times, verify the selected date is a seeded future weekday, all 30 minutes fit the availability, and previous Pending/Confirmed bookings do not occupy it. Rerun the seeder for a fresh 14-day horizon; do not reset the database. Repeated manual runs intentionally accumulate appointment history.

## Automated verification

```powershell
dotnet build MediClinic.sln
dotnet test MediClinic.sln
# Optional focused rerun:
dotnet test MediClinic.sln --filter FullyQualifiedName~DemoVerificationTests
```

Tests use isolated temporary SQLite files and real migrations, Identity password hashing, HTTPS cookies, MVC routes and antiforgery forms. They do not seed or delete your local database. The new suite covers idempotency, relationships, roles, secrets, rollback, environment restrictions, preserved records, and a complete seeded patient/staff booking lifecycle. Existing suites cover patient isolation, privilege injection, database overlap races and concurrency.

Windows policy can block assemblies with `0x800711C7` or deny Event Log access under a restricted runner. Report actual failures, retry with approved normal execution when appropriate, and do not disable Smart App Control or change security policy to run tests.

### Milestone 6 results (2026-11-06)

- Windows .NET SDK 8.0.425: `dotnet build MediClinic.sln` succeeded, zero warnings/errors. `dotnet test MediClinic.sln` could not load the test assembly due to Application Control error `0x800711C7`, both inside and outside the restricted runner. No tests executed, even though VSTest returned exit code 0; this is not a passing test run.
- Ubuntu WSL, installed .NET 8 SDK: `dotnet build MediClinic.sln` succeeded, zero warnings/errors; `dotnet test MediClinic.sln` passed **127/127**, zero failures/skips (117 existing plus 10 new cases). Commands ran in an independent temporary source copy, excluding Windows build outputs and database files, with the installed SDK executable at `$HOME/.dotnet/dotnet`.
- No Windows security policies were changed. The existing Windows application database was not seeded or modified during implementation; the opt-in command above is ready for local use with your chosen passwords.
