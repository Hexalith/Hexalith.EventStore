"""Check retained package integrity; this never grants publication/readiness."""
import hashlib,json,pathlib,sys
E=pathlib.Path(__file__).resolve().parent
manifest=json.loads((E/'delivery-manifest.json').read_text())
def sha(path):return hashlib.file_digest(path.open('rb'),'sha256').hexdigest()
errors=[]
for row in manifest['files']:
 p=E/row['path']
 if not p.is_file() or p.stat().st_size!=row['bytes'] or sha(p)!=row['sha256']:errors.append(row['path'])
expected={r['path'] for r in manifest['files']}
actual={p.relative_to(E).as_posix() for p in E.rglob('*') if p.is_file() and p.relative_to(E).as_posix() not in manifest['manifest_exclusions']}
errors.extend('unexpected:'+p for p in sorted(actual-expected))
owner=E.parents[3]
spec=owner/manifest['spec']['path']
if sha(spec)!=manifest['spec']['sha256']:errors.append('spec')
closure=json.loads((E/manifest['source_closure']).read_text());root=pathlib.Path(closure['primary_checkout'])
for row in closure['files']:
 if sha(root/row['path'])!=row['sha256']:errors.append('source:'+row['path'])
result={'integrity_passed':not errors,'retained_files':len(expected),'source_paths':len(closure['files']),'errors':errors,'source_only':True,'bulk_evidence_landing_ready':False,'p1r_usable':False,'story_6_1':'blocked'}
(E/'delivery-verification.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result));sys.exit(0 if not errors else 1)
