"""Run with: python tests/database-safety.test.py. Uses disposable synthetic SQLite only."""
from contextlib import closing
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import unittest

TOOL = Path(__file__).resolve().parents[1] / "tools/database-safety.py"


class DatabaseSafetyTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.source = Path(self.directory.name) / "source.db"
        self.db = sqlite3.connect(self.source)
        self.addCleanup(self.db.close)
        self.db.executescript("""
            PRAGMA journal_mode=WAL;
            CREATE TABLE AspNetUsers(Id TEXT PRIMARY KEY);
            CREATE TABLE AspNetRoles(Id TEXT PRIMARY KEY, NormalizedName TEXT);
            CREATE TABLE AspNetUserRoles(UserId TEXT,RoleId TEXT);
            CREATE TABLE Clinics(Id TEXT PRIMARY KEY);
            CREATE TABLE ClinicAdministrators(UserId TEXT REFERENCES AspNetUsers(Id),
                ClinicId TEXT REFERENCES Clinics(Id), PRIMARY KEY(UserId,ClinicId));
            INSERT INTO AspNetUsers VALUES('admin'),('patient');
            INSERT INTO AspNetRoles VALUES('r','CLINICADMIN');
            INSERT INTO AspNetUserRoles VALUES('admin','r');
            INSERT INTO Clinics VALUES('clinic');
        """)

    def run_tool(self, *arguments):
        return subprocess.run([sys.executable, "-O", str(TOOL), *map(str, arguments)], capture_output=True, text=True)

    def grant(self, user="admin", clinic="clinic"):
        return self.run_tool("grant", self.source, "--user-id", user, "--clinic-id", clinic, "--apply-verified-grant")

    def test_backup_includes_committed_wal_and_preserves_source(self):
        self.db.execute("INSERT INTO Clinics VALUES('wal-row')")
        self.db.commit()
        original = self.source.read_bytes()
        destination = Path(self.directory.name) / "backup.db"
        self.assertEqual(0, self.run_tool("backup", self.source, destination).returncode)
        with closing(sqlite3.connect(destination)) as copied:
            self.assertEqual(2, copied.execute("SELECT count(*) FROM Clinics").fetchone()[0])
        self.assertEqual(original, self.source.read_bytes())

    def test_backup_refuses_overwrite_and_self_copy(self):
        self.assertNotEqual(0, self.run_tool("backup", self.source, self.source).returncode)
        self.assertEqual(2, self.db.execute("SELECT count(*) FROM AspNetUsers").fetchone()[0])

    def test_grant_is_repeatable(self):
        self.assertEqual(0, self.grant().returncode)
        self.assertIn("Grants inserted: 0", self.grant().stdout)
        self.assertEqual(1, self.db.execute("SELECT count(*) FROM ClinicAdministrators").fetchone()[0])

    def test_non_admin_and_unknown_account_are_denied(self):
        for user in ("patient", "unknown", "admin' OR 1=1 --"):
            self.assertNotEqual(0, self.grant(user).returncode)
        self.assertEqual(0, self.db.execute("SELECT count(*) FROM ClinicAdministrators").fetchone()[0])

    def test_unknown_clinic_is_denied(self):
        self.assertNotEqual(0, self.grant(clinic="unknown").returncode)
        self.assertEqual(0, self.db.execute("SELECT count(*) FROM ClinicAdministrators").fetchone()[0])


if __name__ == "__main__":
    unittest.main()
