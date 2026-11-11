# M7.3.1 Patient dashboard

## Data and authorization

`GET /Dashboard/Patient` retains the existing Patient policy. The controller takes the Identity name identifier from the authenticated principal, never a request patient ID, and calls `IAppointmentBooking.List(userId, false)`. The existing service verifies the database role and linked patient, and filters appointments by that patient's key before materializing them. Missing identifiers, missing patient links and service authorization failures are forbidden. Existing Staff/ClinicAdmin dashboards, roles, routes, lifecycle, schema and migrations are unchanged.

The patient-specific [view model](../src/MediClinic.Web/Models/PatientDashboardModel.cs) is computed from this scoped result. Only doctor, service, branch, appointment time/status and the existing detail link appear in the next appointment card. Decision reasons, birth dates and other patients' data are not rendered.

## Metric definitions

| Metric | Definition |
| --- | --- |
| Toplam randevu | All appointments owned by this patient, including cancelled/rejected history. |
| Bekleyen randevu | All Pending appointments, including past requests awaiting review. |
| Onaylı yaklaşan randevu | Confirmed appointments whose start instant is strictly later than now. |
| Tamamlanan randevu | All appointments with Completed status; never inferred from elapsed time. |
| Sıradaki randevunuz | Earliest future Pending or Confirmed appointment, ordered by start then appointment ID for stable ties. |

The injected `TimeProvider.GetUtcNow()` is sampled once per request after the scoped query. `DateTimeOffset` comparisons compare UTC instants; no local wall-clock/string comparisons are used. An appointment starting exactly now, an already-started appointment, and terminal statuses are excluded from upcoming selection. A pending appointment may precede a confirmed appointment and is explicitly labelled Beklemede; confirmation is never implied.

Display uses existing `Scheduling.Local` / `Europe/Istanbul`, explicit `tr-TR` date formatting, a visible İstanbul timezone label and a machine-readable ISO timestamp on `<time>`. No appointment state is changed by visiting the dashboard.

## UI decisions

The [patient view](../src/MediClinic.Web/Views/Dashboard/Patient.cshtml) uses the shared Modern Purple layout, card/button/empty-state styles and status badge partial. All new static UI copy is Turkish. Four semantic definition-list summary cards precede the next appointment section and booking/list quick actions. Separate empty states distinguish a patient with no appointments from one with history but no upcoming appointment. Counts stay visible at zero. Status labels convey meaning without relying on color.

Five small dashboard CSS rules reuse existing color/spacing tokens. An auto-fitting grid with shrinkable columns responds to available content width, accounting for the shared sidebar at 768px. Actions wrap, long values wrap, and there are no fixed-width dashboard elements, new scripts, fonts, libraries or external requests. Existing keyboard focus and 44px button targets are reused. The page works without JavaScript.

## Automated verification

[PatientDashboardTests](../tests/MediClinic.Tests/PatientDashboardTests.cs) covers all status counts, past/exact-now/future boundaries, UTC-offset equivalence, stable next-selection ties, empty/history-only results, rendered patient data and Turkish/local time, query-parameter ownership injection, independent patient sessions, anonymous/staff denial and a patient account whose domain link was removed. Existing authentication tests cover all three role-policy combinations. Tests use real migrated temporary SQLite databases and real Identity/antiforgery cookies; no demo appointments are added to the application database.

## Verification results (2026-11-11)

- Windows `dotnet restore`: passed. `dotnet build MediClinic.sln`: passed with zero warnings/errors. Restricted `dotnet test MediClinic.sln`: 109 passed / 32 failed due to Windows Event Log access denial. Approved normal-permission rerun: **141 passed, zero failures/skips**. Application logging/security configuration was not relaxed.
- Ubuntu WSL: an independent temporary source copy excluded Git metadata, local databases and generated Windows outputs. Restore during build passed, build completed with zero warnings/errors, and `dotnet test MediClinic.sln` passed **141/141**, zero skipped.
- Integrated browser could not open the page. Already cached Playwright and installed Chrome were used against the real HTTPS MVC server with a separate migrated temporary SQLite test fixture. Both empty and populated patients passed at **320, 390, 768 and 1440px**: one h1, four cards, correct counts, status and Istanbul date/time, no horizontal document overflow, and quick-action targets at least 44px high. Desktop and 320px populated screenshots were visually inspected.
- Browser checks also passed keyboard focus, navigation to the existing booking endpoint, populated rendering with JavaScript disabled, anonymous login redirect and staff denial (403 after AccessDenied redirect). Zero browser page errors occurred. Fixture credentials and screenshots remain ignored scratch output; no application data or credentials were changed.
- Limitations: Chrome only; no physical devices, screen reader, Safari or Firefox checks. Counts currently reuse the existing materialized patient appointment list, so memory/query cost grows with that patient's history; a database aggregate query can be introduced later if measured scale requires it. There is no polling; refresh recomputes the metrics.
