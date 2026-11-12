# M10 clinic management

> Historical M10 report. M12 now integrates branch working hours; missing weekday rows are unrestricted. Current operational instructions and readiness are in the [M13 runbook](database-migration-runbook.md) and [audit](final-requirements-audit.md). Counts/results below describe M10 only.

Based on main at `1b4f6b7`, including merged PR #15. The existing Domain entities, Application booking contracts, Infrastructure Identity/EF context, MVC shell and migrated SQLite test fixtures were audited and reused. Catalog operations use the existing Web-to-DbContext composition; no duplicate organization entities or service abstraction was introduced. Staff/clinical permissions and booking contracts are unchanged.

## Scope and trusted provisioning

`ClinicManagementController` requires the authenticated `ClinicAdmin` role. Every read and mutation also checks `ClinicAdministrators(UserId, ClinicId)`. A grant covers all branches, departments, services and working hours of that clinic. IDs outside that scope return 404; an unlinked admin sees an empty state and cannot create a clinic. Having a Staff link does not imply a catalog grant. The bootstrap and demo seed do not create grants.

An already scoped administrator can create another clinic; its grant is inserted in the same transaction. No user can grant access to an existing clinic or change parent associations through the management forms. Public registration remains Patient-only. All POST actions validate antiforgery tokens.

For the first authorized administrator, a trusted database operator must explicitly provision a grant after verifying both the Identity account and the clinic. On a completely empty installation, the operator must first explicitly provision the initial Clinic (ID and nonblank name) as well. There is deliberately no automatic first-admin/global-owner fallback. Stop application writers, take a consistent SQLite backup and use a parameterized command through trusted tooling:

```sql
INSERT INTO ClinicAdministrators (UserId, ClinicId)
SELECT u.Id, c.Id
FROM AspNetUsers u CROSS JOIN Clinics c
WHERE u.Id = @verifiedUserId AND c.Id = @verifiedClinicId
  AND EXISTS (
    SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId
    WHERE ur.UserId = u.Id AND r.NormalizedName = 'CLINICADMIN'
  )
  AND NOT EXISTS (
    SELECT 1 FROM ClinicAdministrators a
    WHERE a.UserId = u.Id AND a.ClinicId = c.Id
  );
```

Bind the existing stored identifiers, check the affected-row count and audit the assignment. No account, role, grant or real demo record was provisioned as part of this change. Revoking the grant takes effect on the next management request. Administrative appointment access continues to follow the separate existing Staff-linked model.

## Persistence and existing data

Additive migration `20261112154350_ClinicManagement`:

- Adds non-null `MedicalServices.Price`, .NET `decimal`, precision 11/scale 2. SQLite stores decimal as TEXT, avoiding floating-point money conversion.
- Existing services receive **0 TRY**, meaning an unconfigured/free catalog price, not a historical billing value. Administrators must review prices before billing is introduced. No historical appointment prices are invented.
- Adds `ClinicAdministrators` with a composite key and restrictive user/clinic foreign keys. Starts empty.
- Adds branch-level `WorkingHours` with one row per branch/day, restrictive branch FK and a check for valid day and open/closed time combinations. Starts empty; missing rows mean unconfigured, not an implicit open schedule.
- Does not rebuild/delete existing tables, seed records, reset databases or alter appointment intervals/triggers.

Apply explicitly with the existing README migration procedure, after stopping writers and making a consistent backup. Startup still does not migrate automatically. The application's existing SQLite database has not been updated by this milestone. Rollback drops the new data, so prefer a forward correction after deployment.

## Behavior and validation

Turkish management pages support clinic creation/name editing, branch creation/name/address editing, department creation/name editing, and service creation/name/duration/TRY price editing. The hierarchy can be filtered by clinic, branch and department. Sibling duplicate names (trimmed, ordinal case-insensitive) are rejected; clinic names are checked globally without disclosing other clinics. Existing duplicate records are preserved. Creation and duplicate checks share a SQLite write transaction. Existing parent relationships are derived from the stored entity on edit.

Names and addresses must be nonblank; forms limit names to 200 and addresses to 500 characters. Service editing accepts whole minutes from 1 to 1440 and prices from 0 to 999999999.99 TRY with at most two decimal places. Legacy positive durations remain readable. Changing a service duration does not rewrite existing appointment EndsAt snapshots.

Working hours use Turkey local wall-clock time, one non-overnight interval per weekday. Open days require opening before closing; closed days clear both times. Different branches can have independent schedules. Forms use the existing shell and flexible wrapping for small screens. Physical browser/accessibility review remains manual.

## M11/M12 limitations

- M11 staff/doctor administration and availability provisioning are separate; existing staff appointment workflows remain intact.
- M12 now intersects configured branch hours with availability and conflicts; missing weekdays remain unrestricted. See [booking integration](booking-integration.md).
- No holidays, split shifts, overnight intervals, price history/payment collection, deletion or grant-management UI. Trusted provisioning is required for initial/existing-clinic grants.
- Concurrent edits use the existing SQLite behavior; the last successful edit wins and a busy database can require retry.

## Verification

Tests include real migrated SQLite controller operations, cross-clinic isolation, unlinked-admin denial, real Identity/MVC role checks and antiforgery, invalid service inputs, working-hours validation, a legacy migration upgrade and existing appointment regression suites. Legacy scheduling migration tests now seed historical columns directly rather than writing the new Price property into an old schema.

Final Windows .NET suite: **184 passed, 0 failed, 0 skipped** (58 seconds), including 7 new M10 cases. JavaScript booking suite: **5 passed, 0 failed**. EF reports no pending model changes. The initial sandboxed suite encountered denied Windows Event Log access; approved normal Windows execution completed successfully, so WSL was not needed. Tests exclusively use independent temporary databases. No security policy was changed.

The application database SHA-256 remains `92046304EE79DA7BAF971F3B8FF73A59519DA15C365415F765CC7A9F88088883`, matching the M8/M9 baseline. No database, secret, build output or test report is included in the commit.
