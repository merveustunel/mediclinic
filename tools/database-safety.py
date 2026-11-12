"""Operator tooling: read-only inspection, SQLite online backup, explicit scoped grant.

No migration, account creation, password changes or role assignment is performed.
Keep inventory output private: it includes stored account and clinic identifiers.
"""
import argparse
from contextlib import closing
import hashlib
import json
from pathlib import Path
import sqlite3


def connect(path, mode="ro"):
    db = sqlite3.connect(Path(path).resolve().as_uri() + "?mode=" + mode, uri=True)
    db.execute("PRAGMA foreign_keys=ON")
    db.execute("PRAGMA busy_timeout=5000")
    return db


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def quote(name):
    return '"' + name.replace('"', '""') + '"'


def inventory(db):
    db.execute("BEGIN")
    tables = [r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name")]
    result = {"counts": {}, "columns": {}, "digests": {}}
    for table in tables:
        columns = [r[1] for r in db.execute("PRAGMA table_info(" + quote(table) + ")")]
        result["columns"][table] = columns
        rows = db.execute("SELECT * FROM " + quote(table)).fetchall()
        result["counts"][table] = len(rows)
        result["digests"][table] = digest(rows)
    result["history"] = db.execute("SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId").fetchall()
    result["integrity"] = db.execute("PRAGMA integrity_check").fetchall()
    result["foreign_keys"] = db.execute("PRAGMA foreign_key_check").fetchall()
    result["journal"] = db.execute("PRAGMA journal_mode").fetchone()[0]
    result["administrators"] = db.execute("SELECT u.Id,u.Email,u.PatientId,u.StaffId FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId=u.Id JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.NormalizedName='CLINICADMIN'").fetchall()
    result["clinics"] = db.execute("SELECT Id,Name FROM Clinics ORDER BY Id").fetchall()
    result["grants"] = db.execute("SELECT UserId,ClinicId FROM ClinicAdministrators ORDER BY UserId,ClinicId").fetchall() if "ClinicAdministrators" in tables else []
    db.rollback()
    return result


def digest(rows):
    return hashlib.sha256(json.dumps(sorted(rows, key=repr), ensure_ascii=True, default=str).encode()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    inspect = sub.add_parser("inspect")
    inspect.add_argument("database")
    backup = sub.add_parser("backup")
    backup.add_argument("database")
    backup.add_argument("destination")
    verify = sub.add_parser("verify")
    verify.add_argument("before", help="Unmigrated backup")
    verify.add_argument("after", help="Migrated rehearsal copy")
    grant = sub.add_parser("grant")
    grant.add_argument("database")
    grant.add_argument("--user-id", required=True)
    grant.add_argument("--clinic-id", required=True)
    grant.add_argument("--apply-verified-grant", action="store_true", required=True)
    args = parser.parse_args()
    if args.command == "inspect":
        with closing(connect(args.database)) as db:
            print(json.dumps(inventory(db), indent=2))
    elif args.command == "backup":
        destination = Path(args.destination).resolve()
        # Exclusive creation prevents accidental overwrite, including the source.
        with destination.open("xb"):
            pass
        with closing(connect(args.database)) as source, closing(sqlite3.connect(destination)) as target:
            source.backup(target)
            require(target.execute("PRAGMA integrity_check").fetchall() == [("ok",)], "Backup integrity failure")
            require(not target.execute("PRAGMA foreign_key_check").fetchall(), "Backup foreign key failure")
        print("Consistent SQLite backup created:", destination)
    elif args.command == "verify":
        with closing(connect(args.before)) as before, closing(connect(args.after)) as after:
            original, migrated = inventory(before), inventory(after)
            require(migrated["integrity"] == [("ok",)] and not migrated["foreign_keys"], "Migrated database integrity failure")
            migration_directory = Path(__file__).resolve().parents[1] / "src/MediClinic.Infrastructure/Persistence/Migrations"
            expected_history = [[p.stem] for p in sorted(migration_directory.glob("[0-9]*_*.cs")) if not p.name.endswith(".Designer.cs")]
            require([list(row) for row in migrated["history"]] == expected_history, "Unexpected migration history")
            require([list(row) for row in original["history"]] == expected_history[:len(original["history"])], "Original history is not an ordered migration prefix")
            for table, columns in original["columns"].items():
                if table == "__EFMigrationsHistory":
                    continue
                rows = after.execute("SELECT " + ",".join(map(quote, columns)) + " FROM " + quote(table)).fetchall()
                require(digest(rows) == original["digests"][table], "Changed historical values: " + table)
            if "Price" not in original["columns"]["Appointments"]:
                require(after.execute("SELECT count(*) FROM Appointments WHERE Price IS NOT NULL").fetchone()[0] == 0, "Legacy prices must remain unknown")
            if "Price" not in original["columns"]["MedicalServices"]:
                require(after.execute("SELECT count(*) FROM MedicalServices WHERE CAST(Price AS NUMERIC) <> 0").fetchone()[0] == 0, "Legacy service default must be zero")
            if "DoctorServices" not in original["columns"]:
                expected = set(before.execute("SELECT p.Id,s.Id FROM People p JOIN MedicalServices s ON s.DepartmentId=p.DepartmentId WHERE p.PersonType='Doctor'"))
                require(set(after.execute("SELECT DoctorId,ServiceId FROM DoctorServices")) == expected, "Assignment backfill mismatch")
            print(json.dumps({"before": original["counts"], "after": migrated["counts"], "historical_values_preserved": True, "integrity": migrated["integrity"], "foreign_keys": migrated["foreign_keys"], "history": migrated["history"]}, indent=2))
    else:
        with closing(connect(args.database, "rw")) as db, db:
            db.execute("BEGIN IMMEDIATE")
            require(db.execute("SELECT count(*) FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId=u.Id JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE u.Id=? AND r.NormalizedName='CLINICADMIN'", (args.user_id,)).fetchone()[0] == 1, "Verified existing ClinicAdmin required")
            require(db.execute("SELECT count(*) FROM Clinics WHERE Id=?", (args.clinic_id,)).fetchone()[0] == 1, "Existing clinic required")
            cursor = db.execute("INSERT INTO ClinicAdministrators(UserId,ClinicId) SELECT ?,? WHERE NOT EXISTS(SELECT 1 FROM ClinicAdministrators WHERE UserId=? AND ClinicId=?)", (args.user_id, args.clinic_id, args.user_id, args.clinic_id))
            inserted = cursor.rowcount
            require(not db.execute("PRAGMA foreign_key_check").fetchall(), "Grant foreign key failure")
        print("Grants inserted:", inserted)


if __name__ == "__main__":
    main()
