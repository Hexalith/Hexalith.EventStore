#!/usr/bin/env python3
"""Bounded PD1 owner probes and capsule cleanup interruption; no workspace writes."""
from pathlib import Path
import shutil
import subprocess
import tempfile

HERE = Path('/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/6-5d-simplification')
SOURCE = (HERE / 'verify.py').read_text()
CHANGES = {
    'redrive': (' and f[3]==held_identity(d)', ''),
    'legacy': ("need(legacy['owner']==owner,'legacy-owner');old=legacy['state']", "old=legacy['state']"),
}

def load(source):
    ns = {'__file__': str(HERE / 'verify.py'), '__name__': 'review_probe'}
    exec(compile(source, str(HERE / 'verify.py'), 'exec'), ns)
    return ns

def legacy_fixture(ns, db):
    events = [{'sequence': 10, 'message': 'm0', 'digest': ns['hashbytes'](b'0')}]
    db.external('drain', ns['drain_fixture'](events, 'success-events'), 'legacy')
    return events

for family, (old, new) in CHANGES.items():
    assert SOURCE.count(old) == 1
    for altered in (False, True):
        ns = load(SOURCE.replace(old, new, 1) if altered else SOURCE)
        db = ns['newdb']()
        if family == 'redrive':
            ns['held_init'](db)
            ns['capture'](db, b'exact carrier')
            try: ns['redrive'](db, crash='commit')
            except ns['Crash']: pass
            d = ns['getcontrol'](db, 'held', 'held')
            fields = ns['read_record'](bytes.fromhex(d['request']['claim']), ns['ANSWERS']['records']['D36-redrive'])
            fields[3][1] = ns['ZERO']
            claim = ns['record']('HX-EV-REDRIVE-REQUEST-2', fields)
            d['request']['claim'] = claim.hex()
            d['request']['signature'] = ns['fixture_sign'](claim)
            d['attempt']['requestHash'] = ns['hashbytes'](claim)
            db.write('held', ns['canonical'](d), 'held', db.read('held', 'held'))
            fn = lambda: ns['send_held'](db)
        else:
            events = legacy_fixture(ns, db)
            ns['capsule_make'](db, events, cleanup=True)
            ns['begin_resume'](db)
            ns['finish_resume'](db)
            fn = lambda: ns['legacy_transition'](db, 'execution', 'claimed', 1, owner='dead-letter-admin')
        before = db.snapshot()
        try:
            fn()
            print(family, 'mutant' if altered else 'original', 'accepted; persisted state changed =', before != db.snapshot())
        except ns['Refusal'] as exc:
            print(family, 'mutant' if altered else 'original', 'refused:', exc, '; unchanged =', before == db.snapshot())
    with tempfile.TemporaryDirectory(prefix='6-5d-bounded-owner-') as directory:
        target = Path(directory)
        for name in ('known-answers.json', 'obligations.md'):
            shutil.copy2(HERE / name, target / name)
        path = target / 'verify.py'
        path.write_text(SOURCE.replace(old, new, 1))
        result = subprocess.run(['python3', str(path)], capture_output=True, text=True)
        print(family, 'mutant complete suite exit', result.returncode)
        print(result.stdout.strip() or result.stderr.strip())

ns = load(SOURCE)
db = ns['newdb']()
events = legacy_fixture(ns, db)
original_delete = db.delete

def crash_before_drain(key, owner):
    if key == 'drain': raise ns['Crash']('before drain deletion')
    return original_delete(key, owner)

db.delete = crash_before_drain
try: ns['capsule_make'](db, events, cleanup=True)
except ns['Crash']: pass
db = db.restart()
d = ns['getcontrol'](db, 'execution', 'execution')
print('cleanup interruption: persisted phase:', d['phase'], '; intent:', d['intent']['kind'], '; drain still present:', db.read('drain', 'legacy') is not None)
try: ns['capsule_make'](db, events, cleanup=True)
except ns['Refusal'] as exc:
    print('cleanup recovery refused:', exc, '; persisted phase:', ns['getcontrol'](db, 'execution', 'execution')['phase'])
