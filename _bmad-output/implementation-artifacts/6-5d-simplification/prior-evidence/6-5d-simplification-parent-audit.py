import hashlib
import json
import re
import subprocess
import tempfile
from pathlib import Path

ROOT = Path('/home/administrator/projects/hexalith/eventstore')
ART = ROOT / '_bmad-output/implementation-artifacts'
SUPPORT = ART / '6-5d-simplification'
expected_archives = {
    'previous-candidate.md': 'fb0c7bec739df1752bc4bc47bd8aa223f74f709fbb6e4926d4be1992c8bc6954',
    'previous-execution.md': 'a2556eea423ac546aca15e7405f2cf64012a544966fec80464de27e905f87386',
}
for name, sha in expected_archives.items():
    assert hashlib.sha256((SUPPORT/name).read_bytes()).hexdigest() == sha, name
execution = (ART/'spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md').read_bytes()
frozen = re.search(rb'<frozen-after-approval[^>]*>.*?</frozen-after-approval>', execution, re.S).group()
assert len(frozen) == 3569
assert hashlib.sha256(frozen).hexdigest() == '9234f13ebb6aef6634fff07a239be0b32c948d4fe9ddca2e49a6a77d8eec10d9'
old = (SUPPORT/'previous-candidate.md').read_text()
new = (SUPPORT/'obligations.md').read_text()
def disposition_ids(text):
    section = text.split('<!-- pass2-dispositions-start -->')[1].split('<!-- pass2-dispositions-end -->')[0]
    return re.findall(r'^\| ((?:VG2|BH2|E2)-[^ |]+) \|', section, re.M)
old_ids, new_ids = disposition_ids(old), disposition_ids(new)
assert len(new_ids) == len(set(new_ids)) == 54 and set(old_ids) == set(new_ids)
print('parent audit: immutable archives, exact frozen intent and all 54 original dispositions passed')

source = (SUPPORT/'verify.py').read_text()
mutations = {
    'framing': ("b'\\x00\\x01'+len(fields).to_bytes", "b'\\x00\\x02'+len(fields).to_bytes"),
    'bound': ("'execution': 768*1024", "'execution': 1"),
    'transition': ("{('claimed','draining'),", "{('claimed','completed'),('claimed','draining'),"),
    'queue invariant': ("need(0<integer(r['ticket'])<=d['lastTicket'],'ticket')", "need(0<integer(r['ticket']),'ticket')"),
    'model behavior': ("('Completed' if classification=='success' else 'Rejected'", "('Rejected' if classification=='success' else 'Rejected'"),
}
with tempfile.TemporaryDirectory(prefix='6-5d-parent-mutations-') as directory:
    tmp = Path(directory)
    for name in ('known-answers.json','obligations.md'):
        (tmp/name).write_bytes((SUPPORT/name).read_bytes())
    for label, (old_text, new_text) in mutations.items():
        assert source.count(old_text) == 1, label
        (tmp/'verify.py').write_text(source.replace(old_text,new_text,1))
        result = subprocess.run(['python3',str(tmp/'verify.py')],capture_output=True,text=True)
        assert result.returncode != 0, label
        print(f'parent mutation: {label} rejected: {result.stderr.splitlines()[-1]}')
    (tmp/'verify.py').write_text(source)
    answers = json.loads((SUPPORT/'known-answers.json').read_text())
    first = next(iter(answers['records'].values()))
    first['sha256'] = '00'*32
    (tmp/'known-answers.json').write_text(json.dumps(answers))
    result = subprocess.run(['python3',str(tmp/'verify.py')],capture_output=True,text=True)
    assert result.returncode != 0, 'known answer'
    print(f'parent mutation: known answer rejected: {result.stderr.splitlines()[-1]}')
