# Admin filters, bilingual medical data and navigation UX

Branch: `feat/admin-filters-localization-ux`. Base: fetched `origin/main`, `0a15aee`.

## Implemented changes

- All appointments combines authorized branch and department with existing status and inclusive Istanbul start/end dates. Status links retain the other filters. Clear resets the GET query. Review, confirmation, rejection, concurrency and patient ownership rules remain unchanged.
- Employee management combines full-name search, branch, department and domain role (Doctor/Staff). Existing edit, availability and account behavior remains intact. Edit links retain a validated local return URL through save/cancel; availability offers a link back to the filtered list.
- A shared filter partial and responsive grid keep the purple theme and existing light/dark tokens. Labels remain explicitly associated with inputs. Long appointment text wraps; employee tables scroll inside their container.
- Sidebar selection uses the routed controller/action, with `aria-current="page"`. Employee edit/availability/interval/weekly pages keep Employee management active; appointment details keep All appointments active; department/service edits keep Organisation management active. Dashboard is active only on its own routed dashboard action.
- Department and service translations can be maintained in Organisation management. Existing names, IDs, relationships, price/duration values and appointment snapshots are preserved.

## Filtering and authorization

Dropdown records are loaded only through the current user's ClinicAdministrators grants. A supplied branch/department outside that scope, an unknown ID, or a department from a different selected branch returns 404. Invalid status/date/role or malformed identifiers return 400. Filtering never grants access to appointment mutation endpoints.

Appointment branch/department/status/date predicates run in the EF query after clinic authorization. Employee branch/department/role predicates also run in SQL. Name search runs on the server after materializing this authorized subset, using Turkish `CompareInfo` with IgnoreCase/IgnoreNonSpace: `İPEK` matches `ipek`, `IŞIK` matches `ışık`. This avoids SQLite's ASCII-only default case handling. No employee/account records are created or removed by GET filters.

Department options are constrained by branch in the UI, and the same relationship is independently validated on the server. With JavaScript disabled, choose All departments before switching to an unrelated branch and submit the GET form. Existing empty states and a new localized employee no-match message explain when nothing matches.

## Translation storage and fallback

Departments and MedicalServices each gain nullable `NameTr` and `NameEn` columns (maximum 200 characters in the domain/form/EF model). The original `Name` remains the canonical administrative name. The selected culture is the existing secure TR/EN culture cookie; supported cultures remain tr-TR/en-GB.

Display order:

1. A nonempty translation for the selected language on that entity.
2. An explicit reviewed pair in [MedicalNames](../src/MediClinic.Domain/MedicalNames.cs), matching either its Turkish or English name.
3. The original name unchanged, including unknown/custom content.

There is no machine translation of arbitrary content. Personal names, clinic names, branch names, addresses and free text are not passed through medical-name translation. Doctor specialty/job-title free text is also preserved.

The booking service emits translated department/service labels for initial HTML and dynamically fetched choices. Patient lists/details/dashboard, staff review/dashboard, admin appointments/dashboard, employee forms/lists and organisation management use the same entity-aware selection. Existing TRY number formatting and Istanbul scheduling are preserved.

After the secure language POST, only explicitly marked medical labels/options are refreshed from an authorized GET of the current page. Form nodes, entered values, passwords, selected identifiers and keyboard focus remain intact. Booking refresh includes live selection values, aborts older requests and ignores stale responses. A failed booking-language refresh restores the manual GET fallback; other labels update on the next successful navigation.

## Migration and safe application

Added [20261115142639_BilingualMedicalNames](../src/MediClinic.Infrastructure/Persistence/Migrations/20261115142639_BilingualMedicalNames.cs), its generated designer and the updated snapshot. Up adds four nullable columns only. No startup migration or seed behavior was added. The migration has **not** been applied to any existing user database. Updated application binaries require this schema upgrade before serving database-backed pages.

Follow the [database migration runbook](database-migration-runbook.md): inspect the actual configured connection, stop writers, make and verify a consistent backup, rehearse on a disposable copy, review results, and obtain explicit approval before touching the original. Never infer the runtime database path from the default configuration alone.

For a newly allocated, absolute temporary rehearsal path only:

```powershell
dotnet ef database update 20261115142639_BilingualMedicalNames --project src/MediClinic.Infrastructure --connection 'Data Source=C:/absolute/disposable/rehearsal.db;Foreign Keys=True'
```

Replace the example with a verified disposable path. To rehearse preservation of existing data, use a verified backup copy as described in the runbook. Do not execute that command against either protected application database as part of automated delivery.

Backfilling is optional because display fallback already handles the reviewed vocabulary. [backfill-medical-names.sql](../scripts/backfill-medical-names.sql) is an explicit transactional SQL script to run manually only after the migration and successful rehearsal. It matches exact reviewed names, fills NULL fields only, and leaves every manually populated translation and canonical Name unchanged. Running it twice has the same result. Extend the reviewed vocabulary and script together when adding supported legacy names. Custom translations should normally be entered through the scoped administration forms. Neither migration nor backfill was run against an original database.

## Verification

- `dotnet build` / final `dotnet build --no-restore`: passed, zero warnings/errors.
- `dotnet test --no-build --no-restore`: **275 passed, 0 failed, 0 skipped** (`admin-filters-localization-final.trx`). The full suite uses temporary SQLite or in-memory connections and includes booking → staff confirmation → patient readback, authorization, concurrency, filtering, bilingual options, fallback, backfill repeatability and navigation regression tests.
- `node --test tests/booking-cascade.test.cjs tests/preferences.test.cjs tests/weekly-hours.test.cjs tests/admin-filters.test.cjs`: **18 passed, 0 failed**.
- `python tests/database-safety.test.py`: **5 passed**; synthetic temporary databases only.
- EF `migrations has-pending-model-changes` with an in-memory design-time connection: no pending model changes.
- `git diff --check`: passed.

An initial restricted .NET run hit Windows Event Log permission errors. An authorized execution outside that sandbox exposed a missing Patient include in the translated dashboard path; it was fixed before successful verification. Security/logging configuration was not relaxed.

## Manual browser acceptance checklist

Use a disposable migrated test database with synthetic users and explicit clinic grants, never either protected database. These visual/device checks remain manual; HTTP integration and JavaScript VM tests do not replace a real browser acceptance pass.

- [ ] In TR and EN, combine Riverside → Dermatology → Pending → date range; change status/date and verify branch/department persist. Clear all; test no matches and invalid/foreign IDs.
- [ ] Search İpek/İPEK and Işık/IŞIK; combine branch/department/Doctor/Staff. Verify Edit and Availability, including returning to the filtered list.
- [ ] Enter custom TR/EN names in department/service forms; verify patient booking choices and details, staff review/dashboard, admin dashboard/appointments and employee/organisation views. Leave translations empty and verify known vocabulary and unknown-name fallback.
- [ ] Change language during cascading booking fetches; confirm current selections and typed form data survive, choices use the new language and no stale response replaces them. Test a failed network request and the manual fallback.
- [ ] Check exactly one sidebar item on each main route and Employee Edit/Availability/Weekly/Interval, Appointment Details and Department/Service Edit, in both cultures and themes.
- [ ] At 320/375/768px and 200% zoom, check filter stacking, long names, local table scrolling, focus visibility, Tab/Enter behavior, touch targets and readable light/dark contrast. Confirm the page itself has no horizontal overflow.
- [ ] Verify TR `1.234,50 TL`, EN `1,234.50 TRY`, localized dates and unchanged Istanbul appointment times.
- [ ] Complete a fresh patient booking, staff confirmation, and patient confirmation readback. Exercise rejection and concurrent review as well.

## Limitations and safety evidence

- Name search intentionally materializes only the already-authorized employee subset for Turkish-aware comparison. Very large installations may need an indexed normalized search strategy and pagination; neither was introduced here.
- Free-text specialty/job-title data and unknown medical names retain their original spelling unless explicit translations are maintained. Empty translations do not synthesize arbitrary text.
- Returning from deeper availability edits uses existing navigation/browser history; the direct availability return link retains the original employee filters.
- No real browser/device acceptance was claimed. Responsive rules and navigation/rendering are covered by source/HTTP/JavaScript checks; the checklist above remains for visual acceptance.
- The pre-existing modification to [MediClinic.Web.csproj](../src/MediClinic.Web/MediClinic.Web.csproj) was preserved and excluded from this feature commit.
- No application server, migration, reset or demo seed was run against the protected databases. All database-writing tests used isolated temporary connections. Protected-file SHA-256 values were unchanged across verification:

| Protected file | SHA-256 |
| --- | --- |
| `.dist/m13/browser.db` | `3A3F98DD8723C83FF134AE14A75682CAB21EFC6F8825056D343EEA7DDA1782CA` |
| `src/MediClinic.Web/mediclinic.db` | `37F6D0FCB1CE1570393F7A3F035C63321DA170A404861C49BDB2B59072C18F2F` |
| `.dist/backups/mediclinic-before-seed-20261113-070015.db` | `02FD40D1B24BA49A5D53466214FC7E435B586339968CC084A994063B13A7FAB2` |

No protected backup was modified, moved or removed. No automatic merge is requested.

## Files changed

47 feature files; the pre-existing project-file edit is excluded.

- [docs/admin-filters-localization-ux.md](../docs/admin-filters-localization-ux.md)
- [scripts/backfill-medical-names.sql](../scripts/backfill-medical-names.sql)
- [src/MediClinic.Application/AppointmentBooking.cs](../src/MediClinic.Application/AppointmentBooking.cs)
- [src/MediClinic.Domain/Department.cs](../src/MediClinic.Domain/Department.cs)
- [src/MediClinic.Domain/MedicalNames.cs](../src/MediClinic.Domain/MedicalNames.cs)
- [src/MediClinic.Domain/MedicalService.cs](../src/MediClinic.Domain/MedicalService.cs)
- [src/MediClinic.Infrastructure/AppointmentBooking.cs](../src/MediClinic.Infrastructure/AppointmentBooking.cs)
- [src/MediClinic.Infrastructure/Persistence/MediClinicDbContext.cs](../src/MediClinic.Infrastructure/Persistence/MediClinicDbContext.cs)
- [src/MediClinic.Infrastructure/Persistence/Migrations/20261115142639_BilingualMedicalNames.Designer.cs](../src/MediClinic.Infrastructure/Persistence/Migrations/20261115142639_BilingualMedicalNames.Designer.cs)
- [src/MediClinic.Infrastructure/Persistence/Migrations/20261115142639_BilingualMedicalNames.cs](../src/MediClinic.Infrastructure/Persistence/Migrations/20261115142639_BilingualMedicalNames.cs)
- [src/MediClinic.Infrastructure/Persistence/Migrations/MediClinicDbContextModelSnapshot.cs](../src/MediClinic.Infrastructure/Persistence/Migrations/MediClinicDbContextModelSnapshot.cs)
- [src/MediClinic.Infrastructure/StaffDashboard.cs](../src/MediClinic.Infrastructure/StaffDashboard.cs)
- [src/MediClinic.Web/Controllers/AdminAppointmentsController.cs](../src/MediClinic.Web/Controllers/AdminAppointmentsController.cs)
- [src/MediClinic.Web/Controllers/ClinicManagementController.cs](../src/MediClinic.Web/Controllers/ClinicManagementController.cs)
- [src/MediClinic.Web/Controllers/EmployeeManagementController.cs](../src/MediClinic.Web/Controllers/EmployeeManagementController.cs)
- [src/MediClinic.Web/Localization/Ui.cs](../src/MediClinic.Web/Localization/Ui.cs)
- [src/MediClinic.Web/Models/AdminFilters.cs](../src/MediClinic.Web/Models/AdminFilters.cs)
- [src/MediClinic.Web/Models/ClinicManagementModel.cs](../src/MediClinic.Web/Models/ClinicManagementModel.cs)
- [src/MediClinic.Web/Models/EmployeeManagementModels.cs](../src/MediClinic.Web/Models/EmployeeManagementModels.cs)
- [src/MediClinic.Web/Resources/SharedResource.en-GB.resx](../src/MediClinic.Web/Resources/SharedResource.en-GB.resx)
- [src/MediClinic.Web/Resources/SharedResource.resx](../src/MediClinic.Web/Resources/SharedResource.resx)
- [src/MediClinic.Web/Resources/SharedResource.tr-TR.resx](../src/MediClinic.Web/Resources/SharedResource.tr-TR.resx)
- [src/MediClinic.Web/Views/AdminAppointments/Details.cshtml](../src/MediClinic.Web/Views/AdminAppointments/Details.cshtml)
- [src/MediClinic.Web/Views/AdminAppointments/Index.cshtml](../src/MediClinic.Web/Views/AdminAppointments/Index.cshtml)
- [src/MediClinic.Web/Views/Appointments/Book.cshtml](../src/MediClinic.Web/Views/Appointments/Book.cshtml)
- [src/MediClinic.Web/Views/Appointments/Details.cshtml](../src/MediClinic.Web/Views/Appointments/Details.cshtml)
- [src/MediClinic.Web/Views/Appointments/List.cshtml](../src/MediClinic.Web/Views/Appointments/List.cshtml)
- [src/MediClinic.Web/Views/Appointments/PatientList.cshtml](../src/MediClinic.Web/Views/Appointments/PatientList.cshtml)
- [src/MediClinic.Web/Views/ClinicManagement/Edit.cshtml](../src/MediClinic.Web/Views/ClinicManagement/Edit.cshtml)
- [src/MediClinic.Web/Views/ClinicManagement/Index.cshtml](../src/MediClinic.Web/Views/ClinicManagement/Index.cshtml)
- [src/MediClinic.Web/Views/Dashboard/Patient.cshtml](../src/MediClinic.Web/Views/Dashboard/Patient.cshtml)
- [src/MediClinic.Web/Views/EmployeeManagement/Availability.cshtml](../src/MediClinic.Web/Views/EmployeeManagement/Availability.cshtml)
- [src/MediClinic.Web/Views/EmployeeManagement/Edit.cshtml](../src/MediClinic.Web/Views/EmployeeManagement/Edit.cshtml)
- [src/MediClinic.Web/Views/EmployeeManagement/Index.cshtml](../src/MediClinic.Web/Views/EmployeeManagement/Index.cshtml)
- [src/MediClinic.Web/Views/Shared/_AdminFilters.cshtml](../src/MediClinic.Web/Views/Shared/_AdminFilters.cshtml)
- [src/MediClinic.Web/Views/Shared/_Layout.cshtml](../src/MediClinic.Web/Views/Shared/_Layout.cshtml)
- [src/MediClinic.Web/Views/Shared/_StaffAppointments.cshtml](../src/MediClinic.Web/Views/Shared/_StaffAppointments.cshtml)
- [src/MediClinic.Web/Views/StaffAppointments/Details.cshtml](../src/MediClinic.Web/Views/StaffAppointments/Details.cshtml)
- [src/MediClinic.Web/Views/StaffAppointments/Index.cshtml](../src/MediClinic.Web/Views/StaffAppointments/Index.cshtml)
- [src/MediClinic.Web/wwwroot/css/site.css](../src/MediClinic.Web/wwwroot/css/site.css)
- [src/MediClinic.Web/wwwroot/js/admin-filters.js](../src/MediClinic.Web/wwwroot/js/admin-filters.js)
- [src/MediClinic.Web/wwwroot/js/booking.js](../src/MediClinic.Web/wwwroot/js/booking.js)
- [src/MediClinic.Web/wwwroot/js/medical-names.js](../src/MediClinic.Web/wwwroot/js/medical-names.js)
- [tests/MediClinic.Tests/AdminFiltersLocalizationTests.cs](../tests/MediClinic.Tests/AdminFiltersLocalizationTests.cs)
- [tests/MediClinic.Tests/BookingTests.cs](../tests/MediClinic.Tests/BookingTests.cs)
- [tests/admin-filters.test.cjs](../tests/admin-filters.test.cjs)
- [tests/booking-cascade.test.cjs](../tests/booking-cascade.test.cjs)
