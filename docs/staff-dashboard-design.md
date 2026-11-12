# Staff dashboard — M7.4.1

## Sections and data sources

The server-rendered Turkish dashboard at `/Dashboard/Staff` provides four metric cards, a compact queue of the first six pending requests, and a separate list of all appointments starting today. `/Dashboard/ClinicAdmin` renders the same dashboard for eligible administrators. Entries show only patient name, start time, doctor, service, branch and the shared status badge. Links open the existing staff details page. No new state transitions, database schema changes or JavaScript dependencies are introduced.

`IAppointmentBooking.Dashboard` reuses the appointment service's server-side department scope. Counts and read-model projections query persisted appointments; patient contact details, birth dates and decision reasons are excluded. The existing staff list remains the route for reviewing the entire queue.

## Metric definitions

All metrics include only the authenticated account's authorized department:

- **Bugünkü randevular**: all statuses whose appointment start is within today's Istanbul calendar day, including rejected and cancelled appointments.
- **Bekleyen talepler**: all Pending appointments, regardless of date (including overdue requests). This count is not limited to the six visible entries.
- **Bugün onaylı**: today's appointments whose current status is Confirmed.
- **Bugün tamamlanan**: today's appointments whose current status is Completed. This is based on scheduled start date, not the date of the completion action.

Pending requests sort by upcoming/current starts first (`StartsAt >= now`), then ascending start instant, then appointment ID; overdue requests follow in ascending start order. Today's list sorts by start instant then ID. No overdue requests are silently discarded from the metric.

## Authorization and isolation

Staff or ClinicAdmin role is required at the dashboard and existing staff read routes. The service additionally verifies the current database role and requires an existing linked Staff entity with a department. Department equality is applied in SQL before counting, ordering, projection and materialization. Department ownership also enforces branch and clinic isolation. Request parameters cannot override this scope. Patients and anonymous users cannot access staff data.

The existing model has no administrator-to-clinic assignment. Consequently an unlinked ClinicAdmin cannot safely be granted clinic-wide visibility: this milestone fails closed for such accounts. A ClinicAdmin linked to Staff receives only that department's scope. This deliberately does not infer a global or clinic-wide grant from the role. Staff read routes now accept linked ClinicAdmin accounts so dashboard detail links work. Review POST and service state changes retain the existing Staff requirement. Patient appointment summaries do not gain patient names.

## Date and time

The existing injected TimeProvider supplies one UTC now per dashboard request. Scheduling.Zone (`Europe/Istanbul`) determines the local calendar date. Local midnight and the following local midnight are converted to UTC; queries use the half-open interval `[start, nextStart)`. Display uses `tr-TR` (`dd MMMM yyyy, HH:mm`) and semantic time elements with an ISO instant. Server local time does not affect calculations.

Counts and lists are separate read queries, so concurrent staff activity can briefly change results between queries; reloading refreshes them.

## Manual verification

1. Use an existing Staff account linked to a department; open `/Dashboard/Staff` and compare cards against that department's records.
2. Check pending, confirmed, completed, cancelled and rejected statuses. Verify pending requests on other dates remain in the pending count and upcoming requests appear before overdue ones.
3. Follow a detail link and return using the dashboard navigation. Log in as a linked ClinicAdmin and repeat through `/Dashboard/ClinicAdmin`; an unlinked administrator must receive access denied.
4. Verify another department, branch and clinic cannot be selected through URL parameters. Attempt staff dashboard and details routes as a Patient, then anonymously.
5. Check the two independent empty states using an existing empty department or isolated test database.
6. At 320, 390, 768 and 1440 px, verify cards wrap, list content remains readable, links are usable by keyboard and no horizontal overflow occurs. No existing database should be reset or reseeded.
7. Run `dotnet build MediClinic.sln` and `dotnet test MediClinic.sln`. Tests use independent temporary migrated databases and leave the application's database and demo records unchanged.

## Verification results (2026-11-11)

- Windows solution build succeeded with zero warnings/errors.
- Full Ubuntu WSL .NET suite: **170 passed, 0 failed, 0 skipped**, including the five new dashboard integration/query tests. Build/restore succeeded using the existing SDK in an independent temporary source copy excluding database files. One additional MVC test rerun passed to capture rendered HTML for visual verification.
- Windows restricted full suite: 101 passed and 69 failed because Application Control blocked the Web assembly (`0x800711C7`). A standard-permission isolated-artifacts attempt also hit Application Control and discovered no tests; it is not counted as a passing run. No Windows security policies were changed.
- Integrated Chromium checked the real MVC test's rendered HTML with the repository CSS/JS at 320, 390, 768 and 1440 px. No horizontal overflow occurred, the metric grid adapted, detail links remained visible, and Turkish headings and the keyboard skip link were present. This was a rendered snapshot check; authenticated route and data behavior were verified separately through the real-cookie MVC tests, rather than a live browser session against the application database.
- Existing database and demo records were not reset, migrated or reseeded by this work.
