"""Evaluate actual MSBuild enrollment without restoring an unpublished package."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path('/home/administrator/projects/hexalith/p1r-canonical-validation-20261016/references/Hexalith.EventStore')
OUT = Path(__file__).resolve().parent / "compiler-enrollment-disposable.json"
assert not OUT.exists(), "verification outputs must be new"
records = []
checks = []
profiles = (
    ("3.122.1", False, True, True, True),
    ("3.121.0", False, True, True, True),
    ("3.119.0", False, True, True, False),
    ("3.110.0", False, True, False, False),
    ("3.70.1", False, False, False, False),
    ("3.122.0", False, False, False, False),
    ("3.123.0", False, False, False, False),
    ("1.0.0", True, True, True, True),
)
for version, source, capabilities, candidate, folded in profiles:
    for name in ("Host", "Domain", "Probe"):
        project = "tools/p1r-published-consumers/" + name.lower() + "/" + name + ".csproj"
        argv = ["dotnet", "msbuild", project, "-nologo", "-p:EventStoreVersion=" + version,
                "-p:UseCurrentSource=" + str(source).lower(), "-p:EventStoreSourceRoot=" + str(ROOT),
                "-p:Configuration=" + ("Debug" if source else "Release"),
                "-getProperty:DefineConstants", "-getItem:Compile"]
        result = subprocess.run(argv, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        record = {"argv": argv, "cwd": str(ROOT), "exit_code": result.returncode,
                  "stdout": result.stdout, "stderr": result.stderr,
                  "stdout_sha256": hashlib.sha256(result.stdout.encode()).hexdigest()}
        records.append(record)
        check = {"version": version, "source": source, "project": name, "passed": False}
        try:
            assert result.returncode == 0, "MSBuild evaluation failed"
            value = json.loads(result.stdout)
            constants = set(value["Properties"]["DefineConstants"].split(";"))
            compile_files = {Path(item["Identity"]).name for item in value["Items"]["Compile"]}
            expected = {"P1R_CAPABILITIES": capabilities, "P1R_CANDIDATE": candidate,
                        "P1R_FOLDED_SNAPSHOT": folded, "P1R_SOURCE": source}
            assert all((key in constants) == enabled for key, enabled in expected.items()), "compiler defines differ"
            if name in ("Host", "Domain"):
                assert ("FixtureEvolutionManifest.cs" in compile_files) == candidate, "evolution inclusion differs"
                assert ("FixtureSnapshotQualification.cs" in compile_files) == folded, "shared fixture inclusion differs"
            if name == "Domain":
                assert ("CounterSnapshotParity.cs" in compile_files) == folded, "parity inclusion differs"
            check.update(passed=True, defines=sorted(constants), compile_files=sorted(compile_files))
        except (AssertionError, ValueError, KeyError) as error:
            check["error"] = str(error)
        checks.append(check)
value = {"scope": "source compiler evaluation only; no package restore or qualification",
         "proposed_version": "3.122.1", "commands": records, "checks": checks,
         "passed": all(check["passed"] for check in checks)}
OUT.write_text(json.dumps(value, indent=2) + "\n")
print(json.dumps({"output": str(OUT), "checks": len(checks), "passed": value["passed"]}))
sys.exit(0 if value["passed"] else 2)
