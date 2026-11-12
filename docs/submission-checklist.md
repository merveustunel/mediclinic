# M8/M9 submission checklist

Date: 2026-11-12. Course: Object-Oriented Programming. Branch: `feat/m8-m9-final-submission`, based on fetched main including PR #14. One PR targets main; merging is left to the maintainer.

## Requirements and documentation

- [x] Authentication/authorization, patient registration/login and dashboard.
- [x] Patient booking, owned list/details/status filters and reasoned cancellation.
- [x] Staff dashboard, department list/details, status/date filters, confirmation, rejection and timed completion.
- [x] Clinic, Branch, Department, MedicalService, Patient, Staff, Doctor, StaffAvailability and Appointment persisted with EF Core SQLite.
- [x] [Feature/quality audit](final-audit.md) and [OOP assessment](oop-principles.md); no artificial inheritance.
- [x] Mermaid: [domain](uml/domain-class-diagram.md), [use cases](uml/use-cases.md), [lifecycle](uml/appointment-lifecycle.md), [sequence](uml/appointment-booking-sequence.md), [layers](uml/layered-architecture.md).
- [x] [README](../README.md) setup, migrations, startup, tests, roles, documentation index and limitations.

## Automated verification

Windows checks ran from repository root with .NET SDK 8.0.425. Final Linux build/tests used the existing Ubuntu WSL SDK 8.0.428 in an isolated source copy. Tests use independent temporary SQLite databases; the JavaScript suite uses Node's built-in runner.

| Command | Result |
| --- | --- |
| `dotnet tool restore` | Passed; dotnet-ef 8.0.22. |
| `dotnet build MediClinic.sln --no-restore` | Final Windows and isolated WSL builds both passed, 0 warnings / 0 errors. |
| `dotnet ef migrations has-pending-model-changes --project src/MediClinic.Infrastructure` | Passed; no model changes since last migration. |
| `dotnet ef migrations list --project src/MediClinic.Infrastructure --no-connect` | Listed the three existing migrations; no application database connection/mutation requested. |
| Final Windows `dotnet test MediClinic.sln --no-build --no-restore --logger "trx;LogFileName=m8-m9-final.trx"` | Blocked by Application Control `0x800711C7` during assembly discovery; no tests executed. Not a passing run despite exit code 0. |
| Final isolated WSL `dotnet test MediClinic.sln --no-build --no-restore --logger 'trx;LogFileName=m8-m9-wsl.trx'` | Passed 177 / failed 0 / skipped 0, duration 39 seconds. |
| `node --test tests/booking-cascade.test.cjs` | Passed 5 / failed 0 / skipped 0. |
| Relative Markdown file links / Mermaid source / staged diff hygiene | All relative file targets resolved; five Mermaid code blocks present; diff whitespace check passed. Diagram visual rendering still requires manual preview. |

The initial restricted Windows run on the original source reported 116 passed / 57 failed / 0 skipped, caused by denied `.NET Runtime` Event Log access. An approved normal intermediate run passed 177 cases. After rebuilding the final test source, Application Control blocked Windows discovery. The final authoritative run therefore used Ubuntu WSL with a fresh source copy excluding databases, backups, Git metadata and Windows build outputs, and passed all 177 cases including four added cancellation regressions. No policy changed. Linux TRX is retained in the isolated copy's TestResults directory; Windows reports remain in the ignored test-project TestResults directory. This table preserves the summary without committing generated files.

Final unique automated cases: **182 passed, 0 failed, 0 skipped** (177 .NET + 5 JavaScript). Windows retries and the WSL run are not added together as separate tests.

The [audit matrix](final-audit.md#required-feature-traceability) links coverage for domain invariants, real Identity/MVC policies/antiforgery, booking hierarchy/availability/concurrency, patient/staff/admin isolation, UTC/Istanbul date and exact-now/end boundaries, SQLite migrations/constraints/round-trips, dashboards and filters. Rendered markup assertions are not physical browser or assistive-technology checks.

## Setup for the evaluator

1. Install compatible .NET 8 SDK from [global.json](../global.json); optionally Node 18+.
2. Restore tools/packages, build, configure HTTPS with [README commands](../README.md#setup-requirements-and-development-configuration).
3. Set Development and an absolute ConnectionStrings__MediClinic. Back up existing SQLite consistently before explicitly applying migrations. Do not reset it for a demo.
4. Use [demo setup](demo-verification.md) for locally supplied secrets and explicit `--seed-demo` if catalog/accounts/availability are needed. Existing records/passwords remain; changed input secrets do not rotate passwords.
5. Start HTTPS and open `https://localhost:7222` or the documented explicit port. Use separate patient/staff browser profiles.
6. Run complete automated commands above. Historical milestone totals/English UI labels are superseded by this checklist and the root README.

## Manual demo walkthrough

These items are **pending human verification**, not a claim of a completed live demo. Use synthetic/local data. [Demo setup](demo-verification.md) lists synthetic accounts; obtain passwords from local secret configuration.

- [ ] Verify trusted HTTPS, Ana sayfa, Giriş yap, Hasta kaydı. Register a synthetic patient, test invalid fields, log in/out, verify generic failures and no password echo.
- [ ] Check empty patient dashboard/list. Open **Hasta paneli → Randevu oluştur**; select demo clinic/branch/department/doctor/service and a future seeded weekday within 14 days.
- [ ] **Tarih ve saate devam et → Uygun saatleri göster → Talep özetini gör → Randevu talebini gönder**. Verify Beklemede, correct Istanbul time, doctor/service/branch/duration; save URL. Repeat selection without JavaScript.
- [ ] As Central staff in another profile: **Personel paneli → Randevu inceleme**, filter status/date, **Detayları görüntüle → Randevuyu onayla**. Patient refresh shows Onaylandı.
- [ ] Cancel as patient with a reason; verify Turkish success, İptal edildi, reason and released slot. Blank/overlength reasons and repeated POST must not change state again.
- [ ] Book another request. Reject as staff with reason; blank/overlength rejection fails. Patient sees Reddedildi/reason; slot is released.
- [ ] Complete a Confirmed appointment after its actual EndsAt. Before EndsAt the control is absent and forged early completion fails. Do not change server time or edit history; the automatic suite verifies this boundary with a test clock.
- [ ] Check dashboard counts, upcoming/history, pending summary and all filters; clear filters and check no-result states.
- [ ] Verify anonymous redirect, wrong-role denial, second-patient 404 for another patient's details, Riverside staff 404 for Central details. Linked ClinicAdmin is department-scoped/read-only unless also Staff.
- [ ] At 320/375px mobile, 768px tablet, 1280px desktop and 200% zoom: check menu/forms/details/filters, wrapping, clipped actions and horizontal overflow.
- [ ] Keyboard: skip link, visible focus, menu toggle/Escape, fields/radios/buttons, password toggle, submit/error recovery. Screen reader: landmarks, labels/errors, statuses, tables/cards, live feedback. Check contrast and actual browser rendering.
- [ ] Review remaining English framework registration errors/demo labels against course rubric; accept the documented limitation or schedule separate localization work.
- [ ] Open documentation links and preview all five Mermaid diagrams in the submission platform. Review the one PR against main; leave it unmerged.

## Repository and data hygiene

- [x] Starting tree clean; latest fetched main includes PR #14.
- [x] No migration/reset/seeding/provisioning/application database write in this milestone.
- [x] Application database SHA-256 unchanged during QA: `92046304EE79DA7BAF971F3B8FF73A59519DA15C365415F765CC7A9F88088883`.
- [x] Existing backup unchanged; no tracked database, generated output, TRX, secret settings or unrelated files added.
- [ ] Maintainer completes manual verification and course submission after PR review.

## Known limitations

Required functionality and OOP principles are present. Full framework-error Turkish localization and interactive visual/accessibility verification are incomplete. Bootstrap ClinicAdmin is unlinked and grants no global clinical view. Catalog/availability UI, rescheduling, email/recovery, notifications, MFA and production hardening are outside this milestone. SQLite conflicts require refresh/retry. See [README](../README.md#known-limitations).

## Windows execution fallback

For restricted Event Log access, retry through approved normal execution. If Application Control blocks assemblies (for example `0x800711C7`), use the existing Ubuntu WSL SDK with an independent source copy. Exclude databases/backups, Windows build artifacts and Git metadata; do not change security policy or execute Windows build outputs from WSL.

Example Bash inside Ubuntu WSL using the installed SDK:

```bash
qa_copy=$(mktemp -d /tmp/mediclinic-m8-m9-XXXXXX)
tar -C /mnt/c/Projects/mediclinic \
  --exclude=.git --exclude=bin --exclude=obj --exclude=TestResults \
  --exclude='*.db*' --exclude='*.sqlite*' --exclude='*.bak' \
  --exclude='.env*' -cf - . | tar -C "$qa_copy" -xf -
cd "$qa_copy"
"$HOME/.dotnet/dotnet" restore MediClinic.sln
"$HOME/.dotnet/dotnet" build MediClinic.sln --no-restore
"$HOME/.dotnet/dotnet" test MediClinic.sln --no-build --no-restore \
  --logger 'trx;LogFileName=m8-m9-wsl.trx'
```

Record actual totals/runtime. Exit code alone without executed tests is not a pass. Keep the isolated directory as needed; never copy test databases over application data.
