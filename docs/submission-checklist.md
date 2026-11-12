# M13 submission checklist

2026-11-12. Branch `feat/m13-final-readiness`, based on fetched main `600ca95` including PR #18. See the [evidence and requirement matrix](final-requirements-audit.md). **Not yet unconditionally ready for submission.**

## Completed evidence

- [x] All 12 official requirements inspected against implementation and named test files.
- [x] Full solution build: 0 warnings, 0 errors.
- [x] Full .NET suite: 208 passed, 0 failed, 0 skipped (approved Windows execution).
- [x] JavaScript: 5 passed, 0 failed, 0 skipped.
- [x] Python database safety: 5 passed, 0 failed, 0 skipped.
- [x] EF model consistency: no pending model changes; six migrations listed without connecting.
- [x] SQLite online backup includes committed WAL; M10–M12 applied only to an isolated copy.
- [x] Every historical column preserved; row counts, assignment backfill, NULL legacy prices, integrity and foreign keys checked.
- [x] Synthetic isolated administrator provisioned; grant repeat returns 0; non-admin/unknown-user/unknown-clinic grants rejected by safety tests.
- [x] Original database SHA-256 unchanged; no original migration, reseed, account or grant changes.
- [x] README, OOP, Mermaid and architecture updated for M10–M12.

## Before original migration

- [ ] Obtain explicit approval to migrate the original and separately verify the intended administrator identity/clinic ownership.
- [ ] Follow [migration runbook](database-migration-runbook.md): stop writers, absolute path, new consistent backup, fresh isolated rehearsal, SQL review, original upgrade and comparison.
- [ ] Provision a verified initial ClinicAdmin and explicit clinic grant. The inspected database has no ClinicAdmin account; do not promote a convenient Patient/Staff account.
- [ ] Review zero-default catalog prices and all seven working-hour days per branch. Preserve active appointments when editing hours/availability/services.

## Manual demo walkthrough

All items below are pending live-browser checks. Kestrel started against the isolated copy, but navigation to `https://localhost:7443/Account/Login` failed with `net::ERR_CERT_AUTHORITY_INVALID`. No certificate validation, secure-cookie, antiforgery or authorization control was disabled. MVC integration tests passing does not check browser JavaScript/rendering.

- [ ] Configure trusted development HTTPS using the machine owner's normal process. Start the app with the absolute isolated connection from [README](../README.md). Use known synthetic credentials; never reset original passwords to facilitate a demo.
- [ ] Patient: login; choose clinic → branch → department → doctor → service; check price/duration; select available slot; review/submit; view details; cancel with reason. Repeat native forms with JavaScript disabled.
- [ ] Check closed-day/empty-slot feedback, opening/closing boundaries, changed catalog prices versus preserved appointment snapshots, and legacy unknown prices versus zero.
- [ ] Staff: login; authorized department list/details; confirm a Pending appointment; reject another with reason; complete an eligible Confirmed appointment after EndsAt. Verify early completion denied without editing server time/history.
- [ ] ClinicAdmin: login with verified clinic grant; create/edit clinic, branch, department and service; set price/duration; configure all weekday hours; create/edit Staff and Doctor; assign services; configure future availability.
- [ ] Invalid inputs: empty names/reasons, negative/overprecision prices, zero duration, incompatible doctor services, overlapping/uncovered intervals, malformed dates and stale slot submission.
- [ ] Isolation: anonymous/wrong-role denial, other-patient details, other-department staff, ungranted admin, cross-clinic employee/catalog IDs, privilege injection and missing antiforgery token.
- [ ] Concurrent browser bookings and schedule/hours/assignment edits: one valid outcome, preserved history, usable retry feedback.
- [ ] Keyboard focus, labels/errors, screen reader, mobile/tablet/desktop, 200% zoom and navigation. Preview all five Mermaid diagrams in the evaluator's renderer.
- [ ] Review the single PR against main. Maintainer decides merging; submit only after critical checks are resolved.

## Reproduce automated results

Use [README commands](../README.md#setup-and-verification). If sandbox Event Log access causes failures, use approved normal Windows execution. M13's restricted run was 138 passed / 70 failed / 0 skipped; the approved full rerun was 208 / 0 / 0. Keep both TRX reports private under ignored TestResults; do not combine retries into extra passing cases. If Windows Application Control blocks discovery, record it and use an approved isolated WSL source copy excluding databases, secrets and Windows build outputs; never weaken policy. M13 did not need WSL.

Keep `.dist/m13` private: backups contain credentials/historical records and the browser copy has synthetic provisioning. Do not commit databases, passwords, generated reports or backup files. The untouched migration rehearsal and the modified browser copy serve different purposes; never copy either over the original.
