"""Apply one guard-weakening mutation at a time to a scratch copy and require the focused suite to fail.

Usage: run_mutation_checks.py TOOLS_DIR. Entry 0 is an unmutated control that must pass; every other
entry must be killed. Exit 1 when the control fails or any mutation survives.
"""
import shutil, subprocess, sys, tempfile
from pathlib import Path

SOURCE = Path(sys.argv[1])
MUTATIONS = [
    ("p1r_published_qualification.py", 'return "tooling-synthetic" if synthetic else "published-package"', 'return "published-package"'),
    ("p1r_published_qualification.py", 'technical = owner_selected and selection["assertion_instrumentation"] is not None and not unsatisfied', 'technical = not unsatisfied'),
    ("p1r_published_qualification.py", 'require(case["disposition"] == "incompatible", "unsupported operation claimed compatibility")', 'pass'),
    ("p1r_published_qualification.py", 'compatibility = "incompatible" if unsupported or incompatible else "compatible"', 'compatibility = "compatible"'),
    ("p1r_published_qualification.py", 'require(packet["evaluation"] == evaluation, "invented or stale qualification evaluation")', 'pass'),
    ("p1r_published_qualification.py", '"dependent execution refused: " + R_INPUTS)', '"x") if False else None'),
    ("p1r_published_qualification.py", 'and (case["outcome"] != "refusal" or unchanged))', ')'),
    ("p1r_published_qualification.py", 'require(lanes["families"] == [by_id[name] for name in FAMILIES], "family dispositions differ from their scenarios")', 'pass'),
    ("p1r_published_qualification.py", 'require(row == unavailable_row(group, row["id"], selection), "required qualification lane claims unbound evidence")', 'pass'),
    ("p1r_published_qualification.py", 'and row["scope"] in ACCEPTED_SCOPES[group] and bool(row["receipts"])', 'and bool(row["receipts"])'),
    ("p1r_published_qualification.py", 'complete = decisions_complete(inputs, decisions)', 'complete = decisions is not None'),
    ("p1r_published_qualification.py", '(prefix + "no-eventstore-source-dependency", safely(lambda: all(library["type"] == "package" for _, _, library in eventstore()))),', '(prefix + "no-eventstore-source-dependency", True),'),
    ("p1r_published_qualification.py", 'and any(value == assembly["sha256"] for _, value in provided(assembly))', ''),
    ("p1r_published_qualification.py", 'require(receipt["checks"] == checks, "claimed package observations differ from bound evidence")', 'pass'),
    ("p1r_published_qualification.py", '"package observations differ from the retained evidence manifest")', '"x") if False else None'),
    ("p1r_published_qualification.py", 'require(identities["configuration"] == "Release" and identities["packages"]', 'require(True or identities["packages"]'),
    ("p1r_published_qualification.py", '"p1r_usable": False,\n            "usability_authority"', '"p1r_usable": complete,\n            "usability_authority"'),
    ("p1r_qualification_runtime.py", 'return execution, "unverified"', 'return execution, "compatible" if passed else "unverified"'),
    ("p1r_qualification_runtime.py", 'checks_from((fresh, restored == source,', 'checks_from((True, restored == source,'),
    ("p1r_qualification_runtime.py", '                        floor, prior, snapshot, second, fresh_restart, reconstructed,', '                        True, prior, snapshot, True, fresh_restart, reconstructed,'),
    ("p1r_qualification_runtime.py", 'and not writers & restarted_processes)', ')'),
    ("p1r_qualification_runtime.py", '        all(not attempt["errors"] for attempt in attempts),', '        True,'),
    ("p1r_qualification_runtime.py", '        receipt["shared"]["before"] == receipt["shared"]["after"],', '        True,'),
    ("p1r_qualification_runtime.py", 'require(resource["label"] == label, "cleanup inventory includes an unowned resource")', 'pass'),
    ("p1r_qualification_runtime.py", 'and backup["output_sha256"] == receipt["backup"]["sha256"]', ''),
    ("p1r_qualification_runtime.py", 'require(value["synthetic"] == (scope == "tooling-synthetic"), "fixture marker differs from evidence scope")', 'pass'),
    ("p1r_qualification_runtime.py", 'require(section["scope"] == "local-process-control", "process controls claim a wider scope")', 'pass'),
]
survivors = []
MUTATIONS.insert(0, ("p1r_published_qualification.py", "SYNTHETIC = ", "SYNTHETIC = "))  # control: unmutated copy must pass
for index, (name, old, new) in enumerate(MUTATIONS):
    with tempfile.TemporaryDirectory(prefix="p1r-mutation-") as scratch:
        tools = Path(scratch) / "tools"
        shutil.copytree(SOURCE, tools, ignore=shutil.ignore_patterns("__pycache__"))
        verifier = "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/run_verification.py"
        (Path(scratch) / verifier).parent.mkdir(parents=True)
        shutil.copyfile(SOURCE.parent / verifier, Path(scratch) / verifier)
        path = tools / name
        text = path.read_text()
        assert text.count(old) == 1, (index, old, text.count(old))
        path.write_text(text.replace(old, new))
        result = subprocess.run([sys.executable, "-m", "unittest", "tests.test_p1r_published_qualification"], cwd=tools,
                                capture_output=True, text=True, timeout=600)
        killed = result.returncode != 0
        if index == 0:
            assert not killed, result.stderr[-3000:]
        tail = [l for l in result.stderr.splitlines() if l.startswith(("FAIL:", "ERROR:"))][:3]
        print(f"{index:02d} {'KILLED ' if killed else 'SURVIVED'} {name}: {old[:70]!r} {tail}", flush=True)
        if not killed:
            survivors.append(index)
print("survivors:", survivors)
sys.exit(0 if survivors == [0] else 1)
