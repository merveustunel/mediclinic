# M7.3.3 Patient booking UI

## Design and transitions

The existing `/Appointments/Book` GET and POST routes remain. Modern Purple tokens,
cards, buttons, focus outlines and responsive grids provide the visual language.
The ordered step list exposes the current stage with `aria-current="step"`.
All booking interface text is Turkish; database names are displayed unchanged.

1. **Hizmet seçimi:** five labelled native selects cascade through clinic, branch,
   department, doctor and service. Each list is derived from `IAppointmentBooking.Options`
   and filtered by every preceding identifier. Duplicate identifiers are collapsed;
   names are sorted using Turkish collation. Unavailable descendants are disabled.
   Select an option and submit **Seçenekleri güncelle** to load its descendants.
   Changing an ancestor clears incompatible descendants and reports the change.
2. **Tarih ve saat:** the selected service context remains visible. A native date
   field reloads available slots. Native radio cards support Tab, Space and arrow
   keys. Nothing is selected automatically. Empty availability explains how to
   select another date. A missing or stale selection returns to this stage.
3. **Kontrol ve talep:** the summary includes clinic, branch, department, doctor,
   service, duration, Turkish date and local time. Only the final submit performs
   a POST. The existing details route confirms successful creation. Every stage
   explains that requests begin as Pending (Beklemede) and require staff review.

All transitions are ordinary server-rendered form submissions. No booking JavaScript,
external dependencies or frontend framework is required. GETs never create or reserve
appointments. Existing populated links without a stage parameter still show slots.
The selected date is preserved when editing service selections. Browser-native date
picker appearance follows the browser locale; surrounding dates explicitly use `tr-TR`.

## Validation and security

Only allowlisted fields are bound. Complete option tuples are checked against current
backend data before discovering slots or submitting. Invalid identifiers and stale
descendants cannot silently become another appointment. Binding failures produce a
Turkish instruction rather than exposing framework messages or raw submitted values.
Missing selections keep the patient in the first stage.

Both review and final submission call `Slots` again and require the submitted local
start to belong to the selected date's current available slots. This also rejects
arbitrary starts outside the displayed 15-minute grid. The existing application
service then independently rechecks relationships, future/one-year boundaries,
duration, working availability and overlapping blocking appointments. Its SQLite
overlap trigger remains the final concurrency boundary. A displayed slot is never
a reservation; another patient or staff availability change can invalidate it.

Patient identity always comes from authenticated claims. The Patient policy,
patient linkage, ownership checks, antiforgery and privilege-field rejection remain.
No staff/admin workflow, Identity policy, database schema or migration changes were
made. Scheduling continues converting Europe/Istanbul local times to UTC through
the existing scheduling service. UI date formatting does not change UTC comparisons.

## Automated verification

`BookingMvcTests` covers progressive selection with HTTP requests and no script
execution, disabled descendants, complete review content, Turkish date/time display,
successful Pending creation, tampered hierarchy, stale slots, empty availability,
missing slots, malformed dates and an empty service catalog. The demo journey test
now supplies the selected date to the final submission. Existing service/domain
tests cover invalid identifiers, durations, boundaries, working hours, overlaps,
concurrent writes, ownership, authorization and state transitions.

Restore succeeded. Build succeeded with zero warnings/errors before browser checks.
The complete 158-test suite passed in permitted Windows execution. The sandboxed
run initially reported Windows Event Log access failures (46 failed / 109 passed);
those were environment restrictions, not asserted application failures. WSL discovery
was denied with `Wsl/EnumerateDistros/Service/E_ACCESSDENIED`, so permitted native
execution was used. Windows Application Control also blocked a temporary harness
apphost; invoking its managed DLL using the installed dotnet host succeeded.

## Browser evidence

A separate temporary SQLite database was created using the existing test fixture;
the application database was never reset, deleted or reseeded. A local HTTPS server
used that temporary database. Its untrusted development certificate was accepted only
in the browser test session. No certificate trust or Identity configuration changed.

With page JavaScript execution disabled, the browser completed all five cascading
selections, displayed empty availability for a doctor without working hours, then
selected an available doctor. Submitting without a slot showed a Turkish validation
alert. Space and ArrowRight selected 12:15 with a visible solid focus outline. The
review displayed 12 Kasım 2026, Perşembe, all service details and 30 dakika. Final
submission redirected to details showing Beklemede and the Turkish success notice.

Selection, slot and review layouts were measured at 320, 390, 768 and 1440 CSS pixels. None
overflowed horizontally; selects measured 47px and slot cards at least 48px high. Native form controls
and the existing global 44px button minimum are retained. Screen-reader testing and
cross-browser/device testing were not performed. A build attempted while the browser
server held the Web DLL hit a Windows file lock; stop the server before rebuilding.
