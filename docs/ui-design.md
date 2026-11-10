# MediClinic Modern Purple — shared UI foundation (M7.1)

## Architecture and boundaries

The application stays ASP.NET Core 8 MVC. `Views/Shared/_Layout.cshtml` owns the Turkish document language, skip link, desktop sidebar, top bar, authenticated user context, antiforgery-protected POST logout, main landmark and footer. Pages can set `ViewData["Title"]`; otherwise the shell derives a Turkish title from the existing controller/action. Page headings remain in the workflow views. No counts or new workflow destinations are invented.

`wwwroot/css/site.css` defines the design system with CSS custom properties: primary `#6953C8`, accent `#8B5CF6`, soft surface `#F2EEFF`, page background `#F7F6FC`, cards `#FFFFFF`, text `#24213D`, plus border, muted text, radius, spacing and shadow tokens. Accent is decorative; primary is used for links and white-on-purple buttons. Semantic element defaults keep existing forms, labels, tables, headings and definition lists usable without rebuilding individual workflows.

Reusable classes: `.card`, `.button`, `.button-secondary`, `.button-danger`, `.alert`, `.alert-danger`, `.empty-state`, `.table-scroll` and `.status-badge`. MVC validation summary and input error classes receive matching styling. Wrap wide tables in a labeled, focusable `.table-scroll` region so scrolling stays inside the card. Do not apply overflow hiding to the document. Inputs and grid columns can shrink; identity text wraps.

`Program.cs` adds `UseStaticFiles` before routing. The layout references only local CSS/JS through Razor URL tag helpers with cache-busting versions. There is no frontend build, CDN, framework or external font dependency. An optional `Scripts` section supports later screens.

## Navigation and authorization

Navigation uses the existing Identity roles. Patient sees `/Dashboard/Patient`, `/Appointments` and `/Appointments/Book`. Staff sees `/Dashboard/Staff` and `/StaffAppointments`. ClinicAdmin sees `/Dashboard/ClinicAdmin` only; administrative membership does not grant clinical access. Multiple roles produce the union of their existing links. Active destinations use `aria-current="page"`; appointment detail pages keep their list destination active. Anonymous navigation exposes Home, Login and Register. These links are presentation only: all existing controller policies and service scope checks remain authoritative and unchanged.

The logout form uses `/Account/Logout`, POST and the MVC form tag helper's antiforgery token. The user context uses the existing authenticated identity name and a Turkish role description, without querying or disclosing clinical data.

## Responsive navigation and accessibility

Above 760px the shell uses a 248px sidebar and a flexible content column. At 760px and below it stacks into a single column with a native `details`/`summary` collapsible navigation. `site.js` closes it initially on mobile and synchronizes it across the breakpoint. Escape closes the menu and returns keyboard focus to its summary. This is an inline disclosure, not a modal: focus trapping and backdrop behavior are unnecessary. Without JavaScript the menu remains expanded and natively operable.

The layout includes semantic landmarks, one workflow h1, a visible-on-focus skip link, visible keyboard focus and 44px control minimum heights. Turkish status text accompanies distinct colors, so color alone never carries meaning. Reduced-motion preferences suppress optional transitions. Tables have column headers and a keyboard-focusable scrolling region. Check both keyboard and touch behavior when extending screens.

## Status badge contract

`Views/Shared/_StatusBadge.cshtml` accepts the existing `MediClinic.Domain.AppointmentStatus` enum, including `AppointmentSummary.Status`; no view model or persistence change is needed:

```cshtml
<partial name="_StatusBadge" model="Model.Status" />
```

| Value | Turkish label | Style |
| --- | --- | --- |
| Pending | Beklemede | Amber |
| Confirmed | Onaylandı | Purple |
| Completed | Tamamlandı | Green |
| Cancelled | İptal edildi | Neutral |
| Rejected | Reddedildi | Rose |

Unknown enum values get a neutral “Bilinmeyen durum” fallback. The `data-status` attribute retains the enum value for diagnostics and existing integration assertions; visible text is Turkish. Both patient and staff lists/details share the partial.

## Scope for later milestones

Existing workflow markup receives shared styling, Turkish static labels, table overflow containment and status partial integration. Booking field names, action values, form methods, routes, validation, scheduling, authorization, schema, migrations and demo data remain unchanged. Existing business/Identity validation and TempData messages remain in their original language; their complete localization is deferred. Full workflow layouts and richer dashboards belong to M7.2–M7.5.

## Verification (2026-11-11)

- `dotnet restore` completed. Windows NuGet audit reported NU1900 (nuget.org vulnerability metadata unavailable), including a retry outside the sandbox.
- Requested Windows build/test commands were attempted. The default output was locked by an already running application. Separate output via `--artifacts-path .dist/m7-win` built successfully. Windows tests: 105 passed, 22 failed with denied Windows Event Log access. This is not a passing Windows test run; no security settings were changed.
- Existing Ubuntu WSL SDK, independent `/tmp` source copy excluding databases and Windows outputs: restore succeeded; `dotnet build MediClinic.sln` succeeded with zero warnings/errors; `dotnet test MediClinic.sln` passed 127/127 with zero skips. Existing real-cookie/antiforgery MVC tests cover login/logout, scoped patient and staff appointment routes, booking, cancellation and review. Role tests additionally verify the Turkish shell, active navigation, role-specific links and POST logout form/token.
- Integrated browser: Home and Login render; versioned local CSS/JS load (background and breakpoint behavior confirmed). Login has no document overflow at 320, 390, 760, 768 and 1440px. Mobile disclosure opens with Enter and closes with Escape, returning focus to summary.
- Browser limitations: interactive authenticated booking/review/logout and populated appointment tables were not exercised in a browser; their routes and lifecycle are covered by the integration suite. No screen reader or physical mobile device check was performed. The integrated browser request API could not inspect asset response codes (`Storage.getCookies` protocol method unavailable); rendered CSS and running JS confirm asset loading.
