# Patient appointments UI — M7.3.2

## Design

The patient journey is: open Randevularım, choose a status, inspect the owned appointments, then follow a detail or booking link. The patient-only [view](../src/MediClinic.Web/Views/Appointments/PatientList.cshtml) keeps the shared Modern Purple tokens, 44px actions, visible focus styles and the dashboard status badge partial. The existing shared list remains the staff view and is unchanged.

A Turkish heading, description and prominent booking action precede wrapping filter links. A single semantic table contains doctor, service, branch, start date/time, status and detail action. Container queries turn its rows into cards below 780px of available content width, including when the sidebar reduces tablet content space. Cards use two columns, or one below 400px. There is no duplicate hidden appointment list or horizontal table scrolling. Column headers remain available to assistive technology. Each detail link includes the doctor and date in visually hidden text to distinguish repeated actions.

Dates use `Scheduling.Local` (Europe/Istanbul) and explicit `tr-TR` formatting: `dd MMMM yyyy, HH:mm`. The semantic time element retains the original instant in invariant ISO 8601 format. The reusable badge supplies all labels and colors.

## Filtering and scope

[Controller](../src/MediClinic.Web/Controllers/AppointmentsController.cs) first calls the existing `booking.List(UserId, false)`. Authentication, patient linkage validation and ownership scope therefore precede filtering, including invalid filter handling. The [view model](../src/MediClinic.Web/Models/PatientAppointmentsModel.cs) filters that scoped snapshot without changing the service or database. Existing descending start-time ordering is preserved, including relative order of equal timestamps. This intentionally uses the already materialized list; future pagination would require moving filtering into a scoped service query.

| Turkish filter | GET parameter |
| --- | --- |
| Tümü | absent or empty `status` |
| Beklemede | `status=Pending` |
| Onaylandı | `status=Confirmed` |
| Tamamlandı | `status=Completed` |
| İptal edildi | `status=Cancelled` |
| Reddedildi | `status=Rejected` |

Only these exact status names are accepted. Unknown, numeric, composite or differently cased values return HTTP 400 with a Turkish message. Filter links use GET, work without JavaScript, are bookmarkable and mark the active selection with `aria-current`. Ownership cannot be overridden by query parameters. Appointment reasons are not exposed on the list.

The unfiltered owned count distinguishes a patient with no appointments from an empty selected status. The first state offers booking; the second offers a reset to all appointments. Neither state suggests that another patient's appointments exist.

No lifecycle, booking, cancellation, staff authorization, database schema or application data changes are included.

## Verification

- `dotnet restore`: passed.
- `dotnet build --no-restore`: passed, zero warnings/errors.
- `dotnet test --no-build`: initial sandbox run encountered Windows Event Log access restrictions. Repeating outside the sandbox passed all 155 tests, zero failures/skips. No logging or security policies were changed.
- The 14 new MVC regression cases also passed separately. They cover all filters including absent/empty values, invalid values, patient query tampering and ownership isolation, detail access, ordering, Turkish date/time, both empty states, anonymous/staff restrictions and unlinked patient denial. Existing booking/lifecycle and staff suites remain green.
- Browser layout checks used actual MVC-rendered test HTML and the shared stylesheet, served from a temporary directory. Populated, no-appointments and no-filter-matches states had no page overflow at 320, 390, 768 and 1440px. Rows rendered as cards at 320/390/768 and table rows at 1440. Active filters and distinct empty headings were verified in the browser.
- Browser previews are static snapshots: live filter/detail navigation and authentication were verified by MVC HTTP integration tests, not by clicking through an authenticated browser session. An initially missing shared script in the snapshot server produced a 404; filtering and responsive behavior require no script. No existing application database was reset, reseeded or modified.
