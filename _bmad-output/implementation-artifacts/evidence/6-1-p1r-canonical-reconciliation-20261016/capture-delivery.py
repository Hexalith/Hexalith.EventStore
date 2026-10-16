"""Retain the complete unstaged source diff and a fresh canonical reconciliation closure."""
import datetime,hashlib,json,pathlib,subprocess
ROOT=pathlib.Path('/home/administrator/projects/hexalith/p1r-canonical-reconciliation-20261016')
OWNER=pathlib.Path('/home/administrator/projects/hexalith/projects/references/Hexalith.EventStore')
E=pathlib.Path(__file__).resolve().parent
BASE='a2016e88f4d0da9b953fe6f4394f8b8fd4e27f72'
def sha(b):return hashlib.sha256(b).hexdigest()
def git(*args):return subprocess.check_output(['git',*args],cwd=ROOT)
original=json.loads((E/'implementation-final-source-manifest.json').read_text())
paths=[f['path'] for f in original['files']]
reviewed=json.loads((OWNER/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-tooling-hardening-20261016/reviewed-source-hardening.json').read_text())
reviewed_rows={r['path']:r for r in reviewed['files']}
parts=[f'Review working tree: {ROOT}\nCanonical baseline: {BASE}\n\n'.encode(),git('diff','--binary',BASE,'--',*paths)]
rows=[]
for name in sorted(paths):
 data=(ROOT/name).read_bytes();r=subprocess.run(['git','show',BASE+':'+name],cwd=ROOT,capture_output=True);canonical=r.stdout if r.returncode==0 else None
 row={'path':name,'bytes':len(data),'sha256':sha(data),'canonical_sha256':sha(canonical) if canonical is not None else None}
 if name in reviewed_rows:row.update(hardening_sha256=reviewed_rows[name]['sha256'],exact_hardening_bytes=sha(data)==reviewed_rows[name]['sha256'])
 if canonical is None:
  d=subprocess.run(['git','diff','--no-index','--binary','--','/dev/null',name],cwd=ROOT,capture_output=True)
  if d.returncode not in (0,1):raise RuntimeError(d.stderr.decode())
  parts.append(d.stdout)
 rows.append(row)
b=b''.join(parts);(E/'reviewed-reconciliation.diff').write_bytes(b)
protected={}
for name in git('ls-files','-z','--','src/','tests/Hexalith.EventStore.Server.Tests/Security/','Directory.Packages.props','Directory.Build.props','Directory.Build.targets','global.json','.gitmodules','.releaserc.json','tools/tests/test_p1r_published_qualification.py').decode().split('\0'):
 if not name or name in paths:continue
 data=(ROOT/name).read_bytes();base=git('show',BASE+':'+name)
 if data!=base and name!='global.json':raise RuntimeError('Canonical protected source changed: '+name)
 # global.json is explicitly CRLF on checkout; compare its canonical Git-normalized bytes.
 if name=='global.json' and data.replace(b'\r\n',b'\n')!=base.replace(b'\r\n',b'\n'):raise RuntimeError('SDK pin changed')
 protected[name]={'sha256':sha(data),'canonical_git_blob':git('rev-parse',BASE+':'+name).decode().strip()}
helpers=json.loads((OWNER/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-successor-enrollment-20261014/required-ci-test-dependencies.json').read_text())['files']
assert all(sha((ROOT/r['path']).read_bytes())==r['sha256'] for r in helpers)
value={'captured_at_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':'uncommitted source-only reconciliation; no landed/frozen publication source or qualification','canonical_baseline':BASE,'canonical_tree':git('rev-parse',BASE+'^{tree}').decode().strip(),'primary_checkout':str(ROOT),'files':rows,'complete_hardening_closure_paths':len(reviewed_rows),'complete_delivery_paths':len(paths),'required_ci_helpers':helpers,'canonical_production_security_and_pins':protected,'approved_publisher':original['approved_publisher'],'diff':{'path':'reviewed-reconciliation.diff','bytes':len(b),'sha256':sha(b)},'future_frozen_source':None,'reserved_version':None,'selected_successor':None,'p1r_usable':False,'story_6_1':'blocked'}
(E/'reviewed-source-reconciliation.json').write_text(json.dumps(value,indent=2)+'\n')
print(json.dumps({'paths':len(rows),'closure':len(reviewed_rows),'exact_hardening':sum(r.get('exact_hardening_bytes',False) for r in rows),'protected_canonical_files':len(protected),'diff_bytes':len(b),'diff_sha256':sha(b)}))
