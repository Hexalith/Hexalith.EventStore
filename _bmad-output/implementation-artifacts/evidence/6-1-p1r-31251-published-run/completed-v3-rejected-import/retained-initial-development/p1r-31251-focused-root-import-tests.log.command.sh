python3 - <<'PY'
from pathlib import Path
p=Path('tools/tests/test_p1r_published_executor.py');s=p.read_text().replace("        executor.write_json(source_path,binding)\n        receipt['execution_evidence'].update", "        executor.write_json(source_path,binding)\n        execution_path = Path(self.temporary.name)/'execution.json'\n        execution = json.loads(execution_path.read_bytes())\n        execution['executor_source_sha256'] = executor.digest(source_path.read_bytes())\n        executor.write_json(execution_path,execution)\n        receipt['execution_evidence'].update")
p.write_text(s)
PY
PYTHONPATH=tools:tools/tests python3 -m unittest test_p1r_published_executor.Published31251SnapshotWitnessTests.testActualReceiptImportForwardsSelectedVersionAndRefusesMissingOrSubstitutedProof test_p1r_isolation_hardening.TaggedSourceLifecycleTests > /tmp/p1r-31251-focused-root-import-tests.log 2>&1
cat /tmp/p1r-31251-focused-root-import-tests.log