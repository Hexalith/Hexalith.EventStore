"""Capture exact source inputs and literal verification commands for this package."""
import datetime, hashlib, json, os, pathlib, subprocess, sys, time
ROOT=pathlib.Path(os.environ.get('P1R_VERIFICATION_ROOT','/home/administrator/projects/hexalith/p1r-canonical-reconciliation-20261016'))
OWNER=pathlib.Path('/home/administrator/projects/hexalith/projects/references/Hexalith.EventStore')
E=pathlib.Path(__file__).resolve().parent
PREFIXES=('src/','tests/','tools/','scripts/','.github/workflows/','docs/architecture/')
def sha(path):
 if path.is_symlink():return 'symlink:'+os.readlink(path)
 return hashlib.file_digest(path.open('rb'),'sha256').hexdigest()
def binding():
 paths=set(subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0'))
 paths.update(subprocess.check_output(['git','ls-files','--others','--exclude-standard','-z'],cwd=ROOT).decode().split('\0'))
 closure=json.loads((OWNER/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-tooling-hardening-20261016/reviewed-source-hardening.json').read_text())
 keep={f['path'] for f in closure['files']}
 keep.update(p for p in paths if p and ('/' not in p or p.startswith(PREFIXES)))
 result={p:sha(ROOT/p) for p in sorted(keep) if (ROOT/p).is_file() or (ROOT/p).is_symlink()}
 # Exact external source/build inputs used by narrow Debug/source builds.
 dep=OWNER.parent
 for name in ['Hexalith.Builds','Hexalith.Commons','Hexalith.Tenants','Hexalith.EventStore']:
  base=dep/name
  tracked=subprocess.check_output(['git','ls-files','-z'],cwd=base).decode().split('\0')
  for p in tracked:
   if p and (p.startswith(('src/','Props/')) or '/' not in p) and (base/p).is_file():result[str(base/p)]=sha(base/p)
 return result
if __name__=='__main__':
 if sys.argv[1]=='bind':
  data=binding();(E/sys.argv[2]).write_text(json.dumps({'captured_at_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'canonical_baseline':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT).decode().strip(),'source_files':data},indent=2)+'\n');print('Bound',len(data),'source files')
 elif sys.argv[1]=='run':
  name=sys.argv[2];argv=sys.argv[3:];start=datetime.datetime.now(datetime.timezone.utc).isoformat();clock=time.monotonic();env=os.environ.copy();env['PYTHONDONTWRITEBYTECODE']='1'
  env['HEXALITH_BUILDS_SOURCE']=str(OWNER.parent/'Hexalith.Builds');log=E/(name+'.log')
  if log.exists() or (E/(name+'.json')).exists():raise RuntimeError('Evidence collision: '+name)
  with log.open('xb') as output:r=subprocess.run(argv,cwd=ROOT,env=env,stdout=output,stderr=subprocess.STDOUT)
  data=log.read_bytes()
  row={'argv':argv,'cwd':str(ROOT),'started_at_utc':start,'duration_seconds':time.monotonic()-clock,'exit_code':r.returncode,'environment_overrides':{'PYTHONDONTWRITEBYTECODE':'1','HEXALITH_BUILDS_SOURCE':env['HEXALITH_BUILDS_SOURCE']},'log':log.name,'log_bytes':len(data),'log_sha256':sha(log)}
  (E/(name+'.json')).write_text(json.dumps(row,indent=2)+'\n');print(json.dumps(row));print(data.decode(errors='replace')[-1800:]);sys.exit(r.returncode)
