# Staff appointment management — M7.4.2 / M7.4.3

## Page structure and filters

The Turkish staff list at `/StaffAppointments` reuses the purple/white shell and responsive appointment cards. Each entry shows patient name, doctor, service, branch, Istanbul start time, status and a detail link. The result count describes the selected filters, not an unfiltered total. All, Pending, Confirmed, Completed, Rejected and Cancelled links retain the selected date. A native GET date form retains status; clear filters resets both. Empty departments and empty filtered results show a useful recovery link.

The application service applies status and date predicates in SQL after the existing authenticated department scope. Date filtering uses the appointment start in the half-open Istanbul day `[midnight, next midnight)`, including terminal statuses. Invalid statuses, numeric/composite enum input, malformed dates and the maximum date (which cannot produce a following midnight) return 400. Stable ordering is descending start followed by appointment ID.

Details use a separate staff view and read model, preserving patient views and summaries. Patient names are loaded only on authorized staff read routes. The information grid wraps with available width, uses semantic time elements, and formats both start and end with explicit `tr-TR` and `Scheduling.Local` (Europe/Istanbul).

## Status actions and validation

Pending appointments have separate confirm and reject POST forms: purple confirmation and red rejection. Rejection requires a non-whitespace reason on the server and a native required field, limited to 2000 characters. The textarea explains that patients can see the reason. Validation returns the current details and submitted reason. Errors use an alert summary; successful actions redirect with Turkish status feedback.

Confirmed appointments expose green completion only when injected TimeProvider is at or beyond scheduled end. Before then an explanation appears. Completed, Rejected and Cancelled appointments have no action forms. The existing domain remains authoritative for every transition, including crafted requests, early completion and concurrent changes. Business failures display Turkish recovery guidance without exposing exception internals.

## Authorization and data safety

Existing Staff/ClinicAdmin read authorization and service checks remain in force. Every list, details lookup and mutation is scoped to the linked staff member's department, which also isolates branches and clinics. User-supplied ownership or department parameters do not change scope. Out-of-scope detail/action targets return 404; unauthorized roles are denied. Patients and anonymous visitors cannot access staff routes. An unlinked account fails closed.

Review still requires the Staff policy and the service's persisted Staff role. A ClinicAdmin without Staff remains read-only and sees no forms; the existing dual-role Staff grant remains supported. Forms preserve antiforgery validation and bind only ID, action and reason. Patient summaries do not acquire patient names. No domain rules, migrations, dependencies, JavaScript, demo records or application database files are changed.

## Manual verification checklist

- Sign in as linked Staff and compare all six status filters and a selected Istanbul date with department records; clear both filters.
- Check an empty department and an empty status/date combination. Verify recovery navigation and dashboard/detail/list links.
- Confirm a pending appointment, reject another with a meaningful reason, try blank/whitespace/overlong reasons, and verify Turkish feedback and patient-visible outcome.
- Confirmed appointments before scheduled end must explain completion timing; at/after the end allow completion. Try crafted terminal and early-completion POSTs; verify the domain rejects them.
- Check another department, branch and clinic through list and detail requests; check Patient, anonymous, unlinked Staff and linked ClinicAdmin accounts. Admin-only accounts must not mutate even with a valid antiforgery token. Missing tokens return 400.
- At 320, 390, 768 and 1440 px, check no horizontal overflow, readable cards/grid, visible status badges, wrapping buttons, associated labels and keyboard focus. Confirm and reject must work independently without scripts.
- Run the full solution build and suite. Use the existing Ubuntu WSL SDK in an isolated source copy excluding databases when Windows Application Control blocks execution; never weaken security policies.

## Verification results (2026-11-12)

- Latest main was fetched and matched local main at `904ec03` (merged PR #13); the working tree was clean before the feature branch was created.
- Final Windows `dotnet build MediClinic.sln --no-restore`: succeeded, zero warnings/errors.
- Windows full suite attempted: 73 passed, 100 failed, zero skipped; blocked assemblies reported Application Control `0x800711C7`. No security settings were changed.
- Existing Ubuntu WSL SDK, independent `/tmp/mediclinic-m742` source copy excluding database files and Windows build outputs: full `dotnet test MediClinic.sln` passed **173/173**, zero failed/skipped. This includes existing booking/patient tests and new staff filtering, Istanbul day boundaries, isolation, access control, read-only admin, rejection validation, completion timing, terminal transitions and empty-state checks. An initial new MVC test expired its authentication cookie when advancing the fake clock; correcting its initial clock kept the cookie valid and the complete rerun passed.
- Headless Chromium rendered actual MVC integration-test HTML with repository CSS at **320, 390, 768 and 1440 px**, for both list and actionable details. All eight checks had document width equal to viewport width. Inputs/buttons were at least 44 px tall; the rejection label, required validation, reason entry and confirmation keyboard focus passed. Screenshots were reviewed for the purple/white layout. These are rendered snapshot checks; authenticated navigation and mutations were verified by real-cookie MVC tests rather than a live browser against the application database.
- Existing SQLite and demo records were untouched. No remaining milestone implementation gaps identified. Lists remain unpaginated, consistent with the existing architecture.
