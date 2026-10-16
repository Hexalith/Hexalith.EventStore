from pathlib import Path
import hashlib,json,os,subprocess,time
root=Path('/home/administrator/projects/hexalith/p1r-canonical-reconciliation-20261016')
e=Path(__file__).resolve().parent
out=e/'implementation-verification'
out.mkdir(exist_ok=True)
env=os.environ.copy()
env['HEXALITH_BUILDS_SOURCE']='/home/administrator/projects/hexalith/projects/references/Hexalith.Builds'
commands=[]
for name in ['test_p1r_qualification.py','test_p1r_published_qualification.py','test_p1r_published_executor.py','test_p1r_isolation.py','test_p1r_isolation_hardening.py','test_release_publication_authority.py']:
 commands.append([name.removesuffix('.py'),['python3','-m','unittest','discover','-s','tools/tests','-p',name,'-v']])
commands.append(['optimized-hardening',['python3','-O','-m','unittest','discover','-s','tools/tests','-p','test_p1r_isolation_hardening.py','-v']])
records=[]
for label,argv in commands:
 started=time.time()
 p=subprocess.run(argv,cwd=root,env=env,capture_output=True)
 log=out/(label+'.log');log.write_bytes(p.stdout+p.stderr)
 records.append({'label':label,'argv':argv,'cwd':str(root),'exit_code':p.returncode,'elapsed_seconds':time.time()-started,'log':str(log.relative_to(e)),'sha256':hashlib.sha256(log.read_bytes()).hexdigest()})
 (out/'commands.json').write_text(json.dumps(records,indent=2)+'\n')
 print(label,p.returncode,round(time.time()-started,2),flush=True)
