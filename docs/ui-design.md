# MediClinic Modern Purple — shared UI foundation (M7.1)

## Authentication screens (M7.2)

The four [account views](../src/MediClinic.Web/Views/Account/) keep the shared shell. Each sets a Turkish page title and has one h1 in a labeled section. Login and Register use a centered, 32rem maximum-width `.auth-panel`, shared tokens, a full-width submit button and reciprocal account links. Register's name fields use two columns above 480px and stack below it. The password control wraps its button underneath the input when space is limited; controls retain the shared 44px minimum height.

Keep `asp-for`, the existing POST action and the form tag helper, which supplies the antiforgery token. Field errors use `asp-validation-for` with a stable `{Property}-error` ID, referenced by the input's `aria-describedby`. Password inputs also reference `Password-hint`. Set `aria-invalid="true"` only when that field has ModelState errors. The `All` summary has an alert role and Turkish introduction; it includes field errors and model-level Identity errors. Validation remains server-side and works without JavaScript. Preserve original validation messages, including their language, rather than translating or suppressing server responses in a view.

Password visibility is progressive enhancement in [site.js](../src/MediClinic.Web/wwwroot/js/site.js). The native button is initially hidden, then enabled only for an existing password input. Its `aria-controls` identifies the input, `aria-pressed` reflects visibility and its Turkish text explains the next action. Enter and Space work natively. The script changes only the input type and button state; it never reads, copies, stores or logs the password value. It masks again on form submission and page exit. Without JavaScript the input remains masked and the form submits normally. Do not add a password value attribute or disable autocomplete/paste.

RegistrationComplete keeps the same neutral response for successful and duplicate registrations and offers login without automatic sign-in. AccessDenied keeps HTTP 403 and offers Dashboard only to authenticated visitors, with Home available to everyone. Dashboard remains responsible for choosing the role landing. No account controller, model validation, Identity settings, cookies, lockout, authorization, database or clinical workflow changes are part of this milestone.

### M7.2 verification (2026-11-11)

- Windows `dotnet restore`: passed. Requested default `dotnet build MediClinic.sln` and `dotnet test MediClinic.sln` were attempted but the running application's output files were locked. Building with `--artifacts-path .dist/m7-2-win` succeeded with zero errors; NU1900 warnings indicated unavailable NuGet vulnerability metadata, including outside the sandbox.
- Windows isolated-output tests: restricted run 106 passed / 26 failed due to denied Windows Event Log access; rerun outside the sandbox passed 132/132, zero skipped. No application security or logging configuration was relaxed.
- Existing Ubuntu WSL SDK, separate temporary source copy excluding databases and generated outputs: `dotnet restore` passed, `dotnet build MediClinic.sln` passed with zero warnings/errors, `dotnet test MediClinic.sln` passed 132/132, zero skipped.
- [AuthenticationTests](../tests/MediClinic.Tests/AuthenticationTests.cs) now also cover rendered form bindings, antiforgery tokens, label/error/hint associations, password-control semantics, field invalid state, no password echo after invalid submission, anonymous AccessDenied status/navigation and the redirect chain for all three roles. Existing tests cover registration, logout, invalid credentials, generic duplicate responses, privilege injection, lockout, cookies and authorization.
- Integrated browser could not open pages. Using already cached Playwright with installed Chrome instead, Login, Register and anonymous AccessDenied passed at 320, 390, 768 and 1440px: no horizontal document overflow, one h1, expected response codes and control sizes. Password toggles passed Enter/Space and pressed/type-state checks at every width. Empty Login/Register submissions displayed server summaries and associated field errors and returned to masked inputs. Login also passed server validation with JavaScript disabled at 320px. No browser page errors occurred. Desktop Login and 320px Register screenshots were visually inspected.
- Limitations: successful registration/RegistrationComplete, authenticated login/logout and role redirects were verified through the real-cookie MVC integration suite, not interactively in Chrome. Browser form checks used empty submissions and created no accounts. No physical device, screen reader, Safari or Firefox verification was performed.


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
