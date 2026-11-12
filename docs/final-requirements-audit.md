# M13 final requirements audit

Date: 2026-11-12. Base: fetched main `600ca95` (merged PR #18). Branch: `feat/m13-final-readiness`. No automatic merge. **Assessment: implementation and automated checks substantially cover the course requirements, but submission readiness is conditional.** Live browser workflows remain unverified, the original database is three migrations behind, and it has no ClinicAdmin identity/grant. Requirement 2 has a branch-level scheduling interpretation; clinic-wide hours are absent. Do not present this report as an unconditional submission pass.

## Official requirement traceability

Status **Implemented / automated** means source evidence and a passing automated test exist; it does not mean live browser acceptance passed. Tests use independent migrated SQLite fixtures. **Qualified** identifies a literal scope limitation needing evaluator acceptance or further work.

| Official requirement | Implementation files / evidence | Test coverage | Status | Remaining limitation |
| --- | --- | --- | --- | --- |
| 1. Clinic/hospital definitions and multi-branch support | [Clinic](../src/MediClinic.Domain/Clinic.cs), [Branch](../src/MediClinic.Domain/Branch.cs), [ClinicManagementController](../src/MediClinic.Web/Controllers/ClinicManagementController.cs): parent-owned branches, persisted graph, scoped create/edit. | [OrganizationTests](../tests/MediClinic.Tests/OrganizationTests.cs), [PersistenceTests](../tests/MediClinic.Tests/PersistenceTests.cs), [ClinicManagementTests](../tests/MediClinic.Tests/ClinicManagementTests.cs), especially ScopedCatalogCrudAndIsolationPreserveAppointments. | Implemented / automated | Clinic represents an institution; no separate Hospital subtype/classification. Live admin browser pending. |
| 2. Clinic/branch working hours and departments | [WorkingHours](../src/MediClinic.Domain/WorkingHours.cs), [Department](../src/MediClinic.Domain/Department.cs), [ClinicManagementController](../src/MediClinic.Web/Controllers/ClinicManagementController.cs), [booking implementation](../src/MediClinic.Infrastructure/AppointmentBooking.cs): branch weekday configuration, departments and shared slot/booking predicate. | [ClinicManagementTests](../tests/MediClinic.Tests/ClinicManagementTests.cs), [BookingIntegrationTests](../tests/MediClinic.Tests/BookingIntegrationTests.cs): closed/open boundaries, active booking preservation, administration race. | Qualified: branch implementation automated | No clinic-wide inherited hours. Missing weekday is unrestricted for legacy compatibility; configure all seven days. One non-overnight interval/day; no holiday/split hours. Obtain rubric acceptance if clinic-level hours are required literally. |
| 3. Medical services, duration and price | [MedicalService](../src/MediClinic.Domain/MedicalService.cs), [catalog models](../src/MediClinic.Web/Models/ClinicManagementModel.cs), [ClinicManagementController](../src/MediClinic.Web/Controllers/ClinicManagementController.cs): decimal TRY, guarded duration, validated edit. | [ClinicManagementTests](../tests/MediClinic.Tests/ClinicManagementTests.cs): invalid service inputs and MVC persisted edit; [BookingIntegrationTests](../tests/MediClinic.Tests/BookingIntegrationTests.cs). | Implemented / automated | Legacy services receive zero default, requiring review; no billing/payment system. SQLite TEXT storage does not itself enforce decimal scale. |
| 4. Doctor and healthcare staff registration | [Staff](../src/MediClinic.Domain/Staff.cs), [Doctor](../src/MediClinic.Domain/Doctor.cs), [EmployeeManagementController](../src/MediClinic.Web/Controllers/EmployeeManagementController.cs): scoped creation/edit, optional Staff Identity account in one transaction. | [EmployeeManagementTests](../tests/MediClinic.Tests/EmployeeManagementTests.cs): StaffCreationEditingAndProvisioningAreAtomicAndStaffOnly, role/scope/antiforgery. | Implemented / automated | Trusted admin required; one department/employee. No invitation or forced initial-password rotation. Live admin creation pending. |
| 5. Doctor specialties and service assignments | [Doctor](../src/MediClinic.Domain/Doctor.cs): UpdateSpecialty, AssignServices, Offers; [EmployeeManagementController](../src/MediClinic.Web/Controllers/EmployeeManagementController.cs); [M11](../src/MediClinic.Infrastructure/Persistence/Migrations/20261112161952_StaffManagement.cs): join/backfill/compatibility triggers. | [EmployeeManagementTests](../tests/MediClinic.Tests/EmployeeManagementTests.cs): explicit service restriction on every booking path, historical preservation and assignment guards. | Implemented / automated | Specialty is a validated string, not a taxonomy; assignments must share department. Existing appointments survive assignment changes. |
| 6. Doctor availability management | [StaffAvailability](../src/MediClinic.Domain/StaffAvailability.cs), [EmployeeManagementController](../src/MediClinic.Web/Controllers/EmployeeManagementController.cs): future add/edit/removal and active appointment coverage. | [StaffAvailabilityTests](../tests/MediClinic.Tests/StaffAvailabilityTests.cs), [EmployeeManagementTests](../tests/MediClinic.Tests/EmployeeManagementTests.cs): future/overlap/ownership/coverage, [BookingIntegrationTests](../tests/MediClinic.Tests/BookingIntegrationTests.cs): racing edits. | Implemented / automated | Started/past intervals immutable; UTC storage/Istanbul input. Live configuration pending. |
| 7. Patient appointment creation | [AppointmentsController](../src/MediClinic.Web/Controllers/AppointmentsController.cs), [booking view](../src/MediClinic.Web/Views/Appointments/Book.cshtml), [booking service](../src/MediClinic.Infrastructure/AppointmentBooking.cs): hierarchy, assigned doctor/service, slot and final validation. | [BookingMvcTests](../tests/MediClinic.Tests/BookingMvcTests.cs): PatientCanBrowseBookViewAndCancelThroughMvc, native cascade/review; [BookingTests](../tests/MediClinic.Tests/BookingTests.cs); [JavaScript tests](../tests/booking-cascade.test.cjs). | Implemented / automated | Actual browser cascade/submit on copied data pending; no rescheduling. Slots are not reservations. |
| 8. Appointment overlap prevention | [booking service](../src/MediClinic.Infrastructure/AppointmentBooking.cs), [AppointmentScheduling](../src/MediClinic.Infrastructure/Persistence/Migrations/20261106022559_AppointmentScheduling.cs): prechecks and insert/update database triggers; Pending/Confirmed/Completed block. | [BookingTests](../tests/MediClinic.Tests/BookingTests.cs): simultaneous bookings, direct insert/update, adjacency, legacy overlap refusal. | Implemented / automated | SQLite busy conflicts need retry. Independent browser race pending. |
| 9. Appointment details including duration and price | [Appointment](../src/MediClinic.Domain/Appointment.cs), [patient details](../src/MediClinic.Web/Views/Appointments/Details.cshtml), [staff details](../src/MediClinic.Web/Views/StaffAppointments/Details.cshtml), [BookingIntegration migration](../src/MediClinic.Infrastructure/Persistence/Migrations/20261112164410_BookingIntegration.cs). | [BookingIntegrationTests](../tests/MediClinic.Tests/BookingIntegrationTests.cs): price/duration survive catalog changes, patient/staff details and isolation, nullable legacy price upgrade. | Implemented / automated | Legacy prices remain unknown, not reconstructed. Live rendering pending. |
| 10. Appointment approval mechanism | [Appointment](../src/MediClinic.Domain/Appointment.cs) state machine, [StaffAppointmentsController](../src/MediClinic.Web/Controllers/StaffAppointmentsController.cs), [booking service](../src/MediClinic.Infrastructure/AppointmentBooking.cs): decisions, reasons, concurrency, completion after EndsAt. | [AppointmentTests](../tests/MediClinic.Tests/AppointmentTests.cs), [StaffAppointmentsTests](../tests/MediClinic.Tests/StaffAppointmentsTests.cs), [BookingTests](../tests/MediClinic.Tests/BookingTests.cs): transitions, early completion, concurrent decisions. | Implemented / automated | Explicit Staff action; no automatic completion or start-time approval cutoff. Live review pending. |
| 11. Patient, Staff/Doctor and ClinicAdmin roles | [Program](../src/MediClinic.Web/Program.cs), [ApplicationUser/AuthRoles](../src/MediClinic.Infrastructure/Identity/ApplicationUser.cs), [ClinicAdministrator](../src/MediClinic.Infrastructure/Identity/ClinicAdministrator.cs), [management controllers](../src/MediClinic.Web/Controllers): Identity roles plus links/grants. | [AuthenticationTests](../tests/MediClinic.Tests/AuthenticationTests.cs), [ClinicManagementTests](../tests/MediClinic.Tests/ClinicManagementTests.cs), [EmployeeManagementTests](../tests/MediClinic.Tests/EmployeeManagementTests.cs), [StaffAppointmentsTests](../tests/MediClinic.Tests/StaffAppointmentsTests.cs): role/owner/department/clinic denial, antiforgery, injection, lockout. | Implemented / automated; local provisioning pending | Doctor uses Staff Identity role. Original has no ClinicAdmin account/grant; role alone never grants clinics. Admin clinical access separately needs Staff link/role. |
| 12. Encapsulation, inheritance, polymorphism and abstraction | [Entity](../src/MediClinic.Domain/Entity.cs), [Person](../src/MediClinic.Domain/Person.cs), [Patient](../src/MediClinic.Domain/Patient.cs), [Staff](../src/MediClinic.Domain/Staff.cs), [Doctor](../src/MediClinic.Domain/Doctor.cs), [IAppointmentBooking](../src/MediClinic.Application/AppointmentBooking.cs): abstract bases/contract, private setters/read-only collections, subtype Role dispatch. | [PersonTests](../tests/MediClinic.Tests/PersonTests.cs): InheritanceSupportsPolymorphicRoles and atomic guards; [OrganizationTests](../tests/MediClinic.Tests/OrganizationTests.cs), [PersistenceTests](../tests/MediClinic.Tests/PersistenceTests.cs), [ScaffoldTests](../tests/MediClinic.Tests/ScaffoldTests.cs). | Implemented / automated | Domain Role is descriptive, never an authorization claim. See [OOP explanation](oop-principles.md) and [UML](uml/domain-class-diagram.md). |

## Build and tests

Windows .NET SDK 8.0.425, EF tooling 8.0.22. No security controls were disabled. Counts reflect executed tests, not inferred discovery totals.

| Verification | Exact result | Evidence / command |
| --- | --- | --- |
| Full solution build | Passed; 0 warnings, 0 errors | `dotnet build MediClinic.sln --no-restore` |
| Initial restricted Windows .NET run | 138 passed, 70 failed, 0 skipped; 208 total | `dotnet test MediClinic.sln --no-build --no-restore --logger 'trx;LogFileName=m13-windows.trx'`; Event Log access denied in failing HTTP tests. This run is not a pass. |
| Approved Windows full .NET rerun | **208 passed, 0 failed, 0 skipped**; duration 1m04s | Same command with `m13-approved.trx`, outside restricted sandbox. No code/policy changes for this retry. |
| JavaScript | **5 passed, 0 failed, 0 skipped**, 0 cancelled | `node --test tests/booking-cascade.test.cjs` |
| Python operator safety | **5 passed, 0 failed, 0 skipped** | `python tests/database-safety.test.py`; subprocesses use `-O`: WAL preservation, no overwrite/self-copy, repeat grant, denied non-admin/unknown/injection-shaped user, denied unknown clinic. Initial new test run had 4 passes/1 cleanup error from an unclosed test connection; fixed with explicit closing, then all five passed. |
| EF model consistency | Passed; no pending model changes | `dotnet ef migrations has-pending-model-changes --project src/MediClinic.Infrastructure --no-build` |
| EF order | Six migrations, ordered as below | `dotnet ef migrations list --project src/MediClinic.Infrastructure --no-build --no-connect`; its generic inability-to-show-applied-status footer is expected with no connection. Actual history read with read-only SQLite. |
| Migrated copied DB | integrity_check: `ok`; foreign_key_check: 0 violations | `python tools/database-safety.py verify .dist/m13/before.db .dist/m13/rehearsal.db` |

Final unique automated tests: **218 passed / 0 failed / 0 skipped** (208 .NET + 5 JS + 5 Python). Retries are not added as additional cases. Private TRX files are retained under the test project's ignored TestResults directory; private operational logs/copies are in ignored `.dist/m13`. No WSL tests were needed; restricted WSL discovery was denied and is not validation evidence.

## Original database and migration rehearsal

Actual inspected path: **C:/Projects/mediclinic/src/MediClinic.Web/mediclinic.db**. [Runtime configuration](../src/MediClinic.Web/Program.cs) resolves against Web content root; [factory](../src/MediClinic.Infrastructure/Persistence/MediClinicDbContextFactory.cs) defaults relative to repository root. No relevant environment override existed in the audit shell. This does not prove the configuration of another deployed/running process.

Read-only inspection found WAL mode, clean integrity/FKs and the first three migrations applied. All six non-designer migration implementations were reviewed; designer metadata/snapshot align with the passing EF consistency check:

| Order | Migration | Audited original | Rehearsal result |
| --- | --- | --- | --- |
| 1 | 20261105220707_InitialPersistence | Applied | Preserved clinical graph/checks/FKs. |
| 2 | 20261105223217_IdentityAuthentication | Applied | Preserved accounts/roles/links. |
| 3 | 20261106022559_AppointmentScheduling | Applied | Existing overlap guards retained. Refuses overlapping legacy blocking rows when first applied. |
| 4 | 20261112154350_ClinicManagement (M10) | Pending | Applied; two services have zero catalog price, grants/hours empty. |
| 5 | 20261112161952_StaffManagement (M11) | Pending | Applied; exactly three department-compatible doctor/service pairs backfilled; assignment guards installed. |
| 6 | 20261112164410_BookingIntegration (M12) | Pending | Applied; five existing appointment prices remain NULL. |

The [safety tool](../tools/database-safety.py) opened the source read-only and used SQLite online backup, incorporating committed WAL. It created `.dist/m13/before.db`, then a separate `.dist/m13/rehearsal.db`. Only the rehearsal was upgraded with:

```powershell
dotnet ef database update --project src/MediClinic.Infrastructure --no-build --connection 'Data Source=C:/Projects/mediclinic/.dist/m13/rehearsal.db;Foreign Keys=True'
python tools/database-safety.py verify .dist/m13/before.db .dist/m13/rehearsal.db
```

| Table | Before | After migration |
| --- | ---: | ---: |
| Appointments | 5 | 5 |
| AspNetUsers | 3 | 3 |
| AspNetRoles | 2 | 2 |
| AspNetUserRoles | 3 | 3 |
| AspNetRoleClaims / AspNetUserClaims / AspNetUserLogins / AspNetUserTokens | 0 each | 0 each |
| Clinics | 1 | 1 |
| Branches | 2 | 2 |
| Departments | 2 | 2 |
| MedicalServices | 2 | 2 |
| People | 6 | 6 |
| StaffAvailabilities | 30 | 30 |
| ClinicAdministrators | Absent | 0 |
| WorkingHours | Absent | 0 |
| DoctorServices | Absent | 3 |
| __EFMigrationsHistory | 3 | 6 |

Verification compares every original table's original columns, including IDs, credentials/roles/links, appointment times/status/reasons and catalog/person/availability values via sorted row digests. All matched, not just row counts. Existing appointments retain duration/status; every legacy Price is unknown (NULL), never invented from the zero-default catalog. Exact expected assignment pairs matched. No FK violations; integrity `ok`. Four original scheduling/added assignment INSERT/UPDATE triggers were inspected on the copy. The original main-file SHA-256 remained **92046304EE79DA7BAF971F3B8FF73A59519DA15C365415F765CC7A9F88088883**; read-only history/count checks establish its baseline separately from WAL file hashes.

The final read-only all-table digest comparison of the original against the unmigrated backup also matched, including migration history and all Identity/clinical values. **No original migration, overwrite, reset, deletion, reseed, checkpoint or privilege change was performed.** Future original upgrade requires explicit user approval, a fresh stopped-writer backup/rehearsal and the exact steps in the [migration runbook](database-migration-runbook.md). Never replace the original with the rehearsal/browser copy.

## Administrator provisioning status

Read-only Identity joins found **no ClinicAdmin account**, rather than an existing administrator missing only its initial grant. The original has only Patient and Staff roles. M10 correctly creates an empty grant table and never confers ownership implicitly.

Tested separately on `.dist/m13/browser.db`, backed up from the migrated rehearsal: the existing Development-only [IdentityBootstrap](../src/MediClinic.Web/IdentityBootstrap.cs) created a new clearly synthetic `@example.test` ClinicAdmin using a locally generated private password. The [parameterized grant tool](../tools/database-safety.py) verified that account's existing role and the stored demo clinic ID, inserted **1** grant, then **0** on repeat. It never promoted existing accounts. Python tests verify non-admin/unknown-user and unknown-clinic refusal and transaction rollback. Bootstrap is one-time account creation; the initial grant procedure is repeatable and never resets passwords. See [secure operator instructions](database-migration-runbook.md#initial-administrator-provisioning). A trusted operator must still identify/authorize the real owner; no arbitrary account should be selected.

## End-to-end boundaries and remaining manual checks

Story: an authenticated patient selects an assigned clinic service/slot, submits a request, and sees its snapshotted details; authorized staff decide it while a granted admin configures the underlying catalog/schedule.

| Boundary | Evidence / result |
| --- | --- |
| Server and copy configuration | Approved HTTPS Kestrel start succeeded at localhost:7443 with absolute `.dist/m13/browser.db` connection. Restricted start hit Event Log denial; approved retry changed no security policy. |
| Browser → HTTPS | **Blocked**: integrated browser GET `/Account/Login` returned `net::ERR_CERT_AUTHORITY_INVALID`; local developer certificate is untrusted. No bypass or HTTP downgrade attempted. |
| MVC → EF → response, independent fixtures | Passing full .NET suite exercises real login/cookies/antiforgery and temporary migrated SQLite. Named tests above cover patient browse/book/details/cancel, staff confirm/reject/eligible completion, catalog CRUD/hours, employee registration/service assignment/availability and isolation/invalid inputs. |
| Browser JS/rendering and full copied-data flow | **Unverified**: no authenticated live flow on the copied user's data passed. The copied data has unknown existing demo passwords; M13 did not reset them. Synthetic admin bootstrap/grant only was tested directly. |

Every requested live patient, staff and ClinicAdmin flow remains on the [manual walkthrough](submission-checklist.md#manual-demo-walkthrough), including selection, price/duration/slots, create/details/cancel, authorized staff decisions, all management forms, invalid inputs, cross-user/department/clinic isolation, concurrent browser writes, keyboard/screen reader and responsive layouts. Use known synthetic credentials on a disposable copy after the machine owner configures trusted HTTPS. Automated clock-based completion tests do not imply a live after-EndsAt browser check. Mermaid syntax/source review is not evidence of rendered diagram acceptance.

## Known limitations and submission decision

- Requirement 2 supports branch hours, not an inherited clinic schedule. Missing weekday rows are unrestricted; explicitly configure the full week. Obtain rubric acceptance or implement clinic-level semantics if required.
- Original schema and administrator onboarding remain pending operator approval; do not run the M12 app against the old schema and assume management/booking works.
- Legacy prices are unknown; zero-default service prices require review. No historical billing reconstruction, payments, holidays, overnight/split shifts, multi-department employment, rescheduling, notifications, MFA/recovery, account invitation/forced rotation or grant UI.
- Some framework validation/demo text is English. SQLite contention may require retry; admin edits use last-valid-write behavior. Completed appointments remain blocking; decisions/cancellation have no scheduled-start cutoff.
- Live browser, accessibility/responsiveness and diagram rendering remain unverified.

**Final readiness: conditional, with critical verification/operational steps outstanding.** The code-backed matrix and 218 passing automated cases support review, and the migration rehearsal preserves data, but they do not replace trusted HTTPS browser acceptance, verified administrator onboarding, an approved original upgrade, or clarification of clinic-level hours if the rubric requires it. The PR must remain unmerged until the maintainer decides; course submission should follow completion/acceptance of these steps.
