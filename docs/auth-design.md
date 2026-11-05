# Identity authentication

## Setup and migrations

The .NET 8 MVC application uses ASP.NET Core Identity EF stores 8.0.22 in the existing SQLite `MediClinicDbContext`. Domain remains framework independent. The original InitialPersistence migration is unchanged. IdentityAuthentication only adds Identity tables and references to People; it does not rebuild or drop domain tables. No environment migrates automatically at startup.

From the repository root:

```powershell
dotnet tool restore
dotnet restore MediClinic.sln
dotnet build MediClinic.sln
dotnet test MediClinic.sln
dotnet ef migrations has-pending-model-changes --project src/MediClinic.Infrastructure
dotnet ef migrations script InitialPersistence IdentityAuthentication --project src/MediClinic.Infrastructure
# Explicitly update the local development database:
dotnet ef database update --project src/MediClinic.Infrastructure
# Run MVC over HTTPS (trust the development certificate if needed):
dotnet dev-certs https --trust
dotnet run --project src/MediClinic.Web -- --urls https://localhost:7044
```

ConnectionStrings__MediClinic overrides the database location. Prefer absolute paths. Review migration SQL and back up production data before applying migrations separately during deployment. Never run development bootstrap against a production database.

## Roles and policies

| Identity role / policy | Authorized landing |
| --- | --- |
| Patient | /Dashboard/Patient |
| Staff | /Dashboard/Staff |
| ClinicAdmin | /Dashboard/ClinicAdmin |

Each policy requires authentication and its exact role; ClinicAdmin does not implicitly inherit Staff or Patient access. /Dashboard routes to the highest relevant landing. Home and Account registration/login remain public. Logout requires authentication, POST and anti-forgery. AccessDenied returns 403. Cookie challenges redirect anonymous visitors to login and authenticated visitors with the wrong role to access denied. Views show only navigation for granted roles. Landings contain no business operations.

Domain Person.Role and Doctor.Specialty never supply authorization claims. They retain their existing clinical meanings.

## Domain association and invariants

ApplicationUser has optional, privately set PatientId/Patient and StaffId/Staff associations. Typed factory methods accept an already valid domain object. Registration uses the existing Patient constructor, preserving name and birth-date rules, and creates a fresh Patient rather than letting a visitor claim an existing clinical record. Account creation, Patient insertion and Patient role assignment share one SQLite transaction. Failure rolls back all three. A duplicate registration never leaves an orphan Patient.

Unique FK indexes enforce at most one account per Patient or Staff record. Restrict prevents deleting a linked Person. A check constraint prevents linking both kinds of Person to one account. Domain records may exist without accounts. There is no public relinking API. Staff bootstrap queries an existing Staff (including Doctor); it cannot manufacture a department or bypass domain constructors.

Limitation: ClinicAdmin initially has no Person association and cannot participate in clinical operations that require a Person. Identity roles are independent of these nullable links. Future privileged provisioning must preserve matching role/link semantics. As with the existing TPH clinical FKs, SQLite FKs target People and cannot enforce its discriminator; typed application factories enforce subtype selection. Direct SQL and arbitrary role assignment outside these flows are trusted administrative operations, not supported public interfaces.

## Development-only privileged bootstrap

Use an explicit one-shot command; the server does not start afterward. It fails outside Development, requires a new email, validates passwords through Identity, and never overwrites an existing account. It creates role definitions as needed. No password is stored in source or logged. Supply a password through a locally controlled environment or secret provider, never a command-line argument or committed settings file.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Bootstrap__Email = 'admin@example.test'
$env:Bootstrap__Role = 'ClinicAdmin'
# Set Bootstrap__Password using your local secret mechanism before running.
dotnet run --no-launch-profile --project src/MediClinic.Web -- --bootstrap-identity
# For Staff instead, set Bootstrap__Role=Staff and Bootstrap__StaffId to an existing Staff/Doctor GUID.
Remove-Item Env:Bootstrap__Password
```

Bootstrap does not migrate. Apply migrations explicitly first. Missing secrets, invalid role, missing Staff, duplicate link/email, and Identity validation failures stop bootstrap and roll back. Public registration exposes no role or link fields and explicitly rejects submitted Role/Roles/PatientId/StaffId values.

## Security decisions and boundaries

Identity supplies salted password hashing, password validation (12 characters, uppercase/lowercase, number and symbol), security stamps and lockout (5 failures, 15 minutes). Cookies are HttpOnly, Secure always, SameSite=Lax, use a __Host- name and have a 30-minute sliding expiry. HTTPS is required locally as well as in production; HSTS is enabled outside Development. Login does not accept a return URL, preventing open redirects. All unsafe account actions validate anti-forgery tokens via MVC and Razor form tag helpers.

Successful and duplicate registrations render the same confirmation without auto-login; login failures use the same message for unknown, locked, and incorrect-password accounts. Identity credential checks are not a constant-time endpoint; generic messages do not eliminate timing side channels. No email confirmation is required because email delivery is excluded; email ownership is unverified and must not authorize clinical record claims. Production rate limiting, MFA, recovery/delivery, shared data-protection key provisioning, secret provisioning and audited administration remain deployment/future work. No patient data or passwords are logged by the account controllers.

Integration tests exercise the real MVC pipeline over HTTPS using migrated temporary SQLite databases: registration and persisted links, login/logout, every role landing and cross-role denial, generic invalid credentials and duplicate registration, rejected privilege injection, anti-forgery enforcement, secure cookie flags, lockout, invalid registration rollback, production bootstrap rejection, and upgrading an existing clinical database. Existing domain/persistence tests remain in place.
