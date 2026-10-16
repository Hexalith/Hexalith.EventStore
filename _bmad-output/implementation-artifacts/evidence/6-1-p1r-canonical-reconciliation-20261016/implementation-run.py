from pathlib import Path
import hashlib,json,os,subprocess,sys,time
root=Path('/home/administrator/projects/hexalith/p1r-canonical-reconciliation-20261016')
e=Path(__file__).resolve().parent
label=sys.argv[1]
argv=sys.argv[2:]
out=e/'implementation-verification'
out.mkdir(exist_ok=True)
log=out/(label+'.log')
receipt=out/(label+'.json')
if log.exists() or receipt.exists(): raise RuntimeError('Evidence collision: '+label)
env=os.environ.copy()
env['HEXALITH_BUILDS_SOURCE']='/home/administrator/projects/hexalith/projects/references/Hexalith.Builds'
started=time.time()
p=subprocess.run(argv,cwd=root,env=env,capture_output=True)
log.write_bytes(p.stdout+p.stderr)
receipt.write_text(json.dumps({'argv':argv,'cwd':str(root),'exit_code':p.returncode,'elapsed_seconds':time.time()-started,'log':str(log.relative_to(e)),'sha256':hashlib.sha256(log.read_bytes()).hexdigest()},indent=2)+'\n')
print(label,p.returncode,round(time.time()-started,2),flush=True)
print((p.stdout+p.stderr).decode(errors='replace')[-9000:])
sys.exit(p.returncode)
