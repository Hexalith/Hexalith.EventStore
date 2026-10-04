from pathlib import Path
import importlib.util
import sys
sys.dont_write_bytecode=True
path=Path('_bmad-output/implementation-artifacts/6-5-integration/build-integration.py')
spec=importlib.util.spec_from_file_location('policy_generator',path)
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
doc=(b.ART/'spec-event-versioning-upcasting.md').read_text()
ledger=(b.ART/'deferred-work.md').read_text()
again,ledger_again=b.owner_policy(doc,ledger)
assert b.record_owner_approval(again)==doc
assert ledger_again==ledger
old=Path('/tmp/story-6-5-pragmatic-_yzds0jh')
reviewed=(old/'_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md').read_text()
old_ledger=(old/'_bmad-output/implementation-artifacts/deferred-work.md').read_text()
expected,expected_ledger=b.owner_policy(reviewed,old_ledger)
assert b.record_owner_approval(expected)==doc
assert expected_ledger==ledger
changes=[(a,c) for a,c in zip(old_ledger.splitlines(),ledger.splitlines()) if a!=c]
assert len(old_ledger.splitlines())==len(ledger.splitlines()) and len(changes)==47
assert all(a.startswith('  status: open — proposed') and c.startswith('  status: open — accepted spec disposition') for a,c in changes)
assert 'six-field UNAPPROVED receipt' not in doc
assert 'searches its `UNAPPROVED` receipt' in doc
assert 'All 20 obligations remain open implementation/evidence follow-ups;' in doc
print('passed: idempotent policy-only splice, 47 status-line changes only, historical prose preserved, all 20 obligations open')
