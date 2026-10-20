python3 - <<'PY'
from pathlib import Path
p=Path('tools/p1r-published-consumers/README.md');s=p.read_text();start=s.index('The released v3.125.1 source is\n')
s=s[:start]+'''The published 3.125.1 qualification selects source
`55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b` and tag Builds
`4629ff2d3bd66376b29a553663c241d3a7b87853`. Pass `--source-root` to both
`observe` and `run` to select a self-contained ordinary Git checkout at that
exact tag, and pass the same root to strict `prepare`. The verifier closure
records the current tools and their execution Builds independently. Its
`candidate_source` closure records the immutable production/build inputs;
independent validation reconstructs both roles and refuses dirty, missing,
substituted or drifting source. Linked worktrees and borrowed Git object stores
are refused. Legacy calls without this option retain their historical binding.

The private runner mounts that exact candidate root when it is outside the
verifier workspace. It rechecks source after readiness waits and before final
receipt attachment. Source consumers use Debug/project references into the
selected root, including its SDK and central package pins. Their assets, locks
and physically loaded assemblies are independently bound. Package consumers
continue to restore the signed immutable nuget.org bytes in Release. The finite
snapshot/evolution enrollment includes exactly 3.125.1 alongside the earlier
enrolled versions; 3.126.0 remains refused before state reset or writer startup.

Fresh technical measurements retain every failed check and execution error.
A measured successor support table remains pending until its architect supplies
a genuine disposition. Strict preparation refuses that missing decision; all
four owner decisions and same-baseline conformance remain pending and
`p1r_usable=false`. This tooling changes no production snapshot policy, pins,
rollback=null, Projects AD-17 drain/freeze/forward recovery, acceptance, sprint
status or readiness.
''';p.write_text(s)
p=Path('.gitattributes');s=p.read_text();s+='\n# New P1R receipts bind literal bytes, command output and archive identities.\n_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run/** -text -whitespace\n';p.write_text(s)
p=Path('tools/p1r_published_qualification.py');s=p.read_text().replace('    files = [{"path": str(root / "evidence.json"),', '''    sdk = root / "global.json"
    declared = next((row["sha256"] for row in source["main"]["files"] if row["path"] == "global.json"), None)
    require(digest(regular(sdk)) == declared, "consumer SDK pin differs from tagged source")
    files = [{"path": str(sdk), "sha256": declared}, {"path": str(root / "evidence.json"),''')
p.write_text(s)
PY
python3 -m unittest discover -s tools/tests -p 'test_p1r_isolation_hardening.py' > /tmp/p1r-31251-verifier-hardening-tests.log 2>&1