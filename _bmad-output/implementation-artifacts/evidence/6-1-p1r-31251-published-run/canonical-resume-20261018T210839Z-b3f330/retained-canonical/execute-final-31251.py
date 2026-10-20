#!/usr/bin/env python3
"""Coordinate the already-approved fresh canonical calls, retaining every exit."""
import datetime,hashlib,json,subprocess,sys,uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
BASE=Path(__file__).resolve().parent
SOURCE=Path('/tmp/p1r-31251-tagged-source-final-60e77a9db287420b8a913e014956015f')
INDEX=BASE/'canonical-final-index.json'
assert not INDEX.exists()
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def stamp():return datetime.datetime.now(datetime.timezone.utc).isoformat()
result={'scope':'fresh canonical technical evidence; acceptance remains pending','p1r_usable':False,'started_utc':stamp(),'finished_utc':None,'commands':[],'errors':[],'verifier_root':str(ROOT),'source_root':str(SOURCE),'driver_sha256':sha(Path(__file__))}
def save():INDEX.write_text(json.dumps(result,indent=2)+'\n')
def command(label,argv,identity=None):
 identity=identity or uuid.uuid4().hex
 path=BASE/(label+'-command-'+identity+'.json');log=path.with_suffix('.log');assert not path.exists()
 row={'argv':list(map(str,argv)),'cwd':str(ROOT),'started_utc':stamp(),'output_log':str(log)}
 path.write_text(json.dumps(row,indent=2)+'\n');result['commands'].append({'label':label,'record':str(path)});save()
 with log.open('wb') as handle:p=subprocess.run(row['argv'],cwd=ROOT,stdout=handle,stderr=subprocess.STDOUT)
 row.update(exit_code=p.returncode,finished_utc=stamp());path.write_text(json.dumps(row,indent=2)+'\n');result['commands'][-1]['exit_code']=p.returncode;save();print(json.dumps({'label':label,'exit_code':p.returncode,'record':str(path)}),flush=True)
 return p.returncode
save()
try:
 sys.path.insert(0,str(ROOT/'tools'));import p1r_qualification as preparation
 freeze=BASE/'verification-successor/final-source-freeze-v3.json';f=json.loads(freeze.read_bytes());result['source_freeze']={'path':str(freeze),'sha256':sha(freeze)}
 assert preparation.digest(preparation.canonical(preparation.source_binding(ROOT,SOURCE)))==f['canonical_binding_sha256']
 smoke=BASE/'reminder-smoke-v3-d158cf87ed27489d9025b04c8e13f4f7';s=json.loads((smoke/'smoke.json').read_bytes());w=json.loads((smoke.with_name(smoke.name+'-isolation')/'isolation.json').read_bytes())
 assert s['errors']==[] and s['executor_errors']==[] and s['finished_utc'] and len(s['cases'])==3 and all(r['assertions']['failed']==0 for r in s['cases'])
 assert w['passed'] is True and w['errors']==[] and w['command_exit_code']==0
 result['preceding_smoke']={'path':str(smoke/'smoke.json'),'sha256':sha(smoke/'smoke.json'),'isolation_sha256':sha(smoke.with_name(smoke.name+'-isolation')/'isolation.json')};save()
 capture=BASE/'capture-outer-identities.py';original=Path('/tmp/p1r-31251-verifier-38d60cf41afc4f2d99b4b377c016f95c')/BASE.relative_to(ROOT)/'outer-shared-before.json';before=BASE/'outer-shared-before-canonical-final.json'
 assert command('outer-before',['python3',capture,'--out',before,'--before',BASE/'outer-shared-before-v3-smoke.json','--before',original])==0
 identity=uuid.uuid4().hex;observation=BASE/('observation-'+identity);result['observation']=str(observation);save()
 argv=['python3',ROOT/'tools/p1r-published-executor.py','observe','--out',observation,'--planning-observations',BASE/'planning-observations.json','--selection-reference','approved spec-6-1-p1r-31251-published-qualification.md; 2026-10-18 approve-and-continue','--source-root',SOURCE]
 assert command('observe',argv,identity)==0
 inputs=observation/'owner-inputs.json';decisions=observation/'owner-decisions.json'
 assert json.loads(inputs.read_bytes())['candidate']['version']=='3.125.1'
 result['inputs']={'path':str(inputs),'sha256':sha(inputs)};result['decisions']={'path':str(decisions),'sha256':sha(decisions)}
 identity=uuid.uuid4().hex;execution=BASE/('execution-'+identity);result['execution']=str(execution);save()
 run_code=command('run',['python3',ROOT/'tools/p1r-published-executor.py','run','--out',execution,'--inputs',inputs,'--source-root',SOURCE],identity)
 identity=uuid.uuid4().hex;audit=BASE/('audit-v2-'+identity);wrapper=audit.with_name(audit.name+'-isolation');result['audit']=str(audit);save()
 audit_code=command('audit-v2',['python3',ROOT/'tools/p1r-isolated-runner.py','--out',wrapper,'--source-root',SOURCE,'--','python3',BASE/'audit-31251-v2.py','--verifier-root',ROOT,'--source-root',SOURCE,'--execution',execution,'--inputs',inputs,'--decisions',decisions,'--out',audit],identity)
 assert command('outer-after',['python3',capture,'--out',BASE/'outer-shared-after-canonical-final.json','--before',before,'--before',original])==0
 assert sha(Path(__file__))==result['driver_sha256']
 assert run_code==audit_code==0,'canonical execution or independent audit retained a nonzero exit'
except BaseException as error:
 result['errors'].append({'type':type(error).__name__,'message':str(error)})
finally:
 result['finished_utc']=stamp();save()
print(json.dumps(result),flush=True)
raise SystemExit(2 if result['errors'] else 0)
