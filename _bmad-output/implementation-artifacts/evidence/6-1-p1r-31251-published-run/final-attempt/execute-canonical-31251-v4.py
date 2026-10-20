#!/usr/bin/env python3
"""Fresh canonical V4 invocation; retain all exits and pending decisions."""
import argparse,datetime,hashlib,json,subprocess,sys,uuid
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
for name in ('source-root','freeze','smoke','prior-snapshot','original-snapshot'):
    p.add_argument('--'+name,type=Path,required=True)
a=p.parse_args()
ROOT=Path(__file__).resolve().parents[4]
BASE=Path(__file__).resolve().parent
INDEX=BASE/'canonical-v4-index.json'
assert not INDEX.exists()
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def stamp():return datetime.datetime.now(datetime.timezone.utc).isoformat()
result={'scope':'fresh canonical technical evidence; acceptance remains pending','qualified':False,'p1r_usable':False,
        'started_utc':stamp(),'finished_utc':None,'commands':[],'errors':[],
        'verifier_root':str(ROOT),'source_root':str(a.source_root),'driver_sha256':sha(Path(__file__))}
def save():INDEX.write_text(json.dumps(result,indent=2)+'\n')
def command(label,argv,identity=None):
    identity=identity or uuid.uuid4().hex
    path=BASE/(label+'-command-'+identity+'.json')
    log=path.with_suffix('.log')
    assert not path.exists()
    row={'argv':list(map(str,argv)),'cwd':str(ROOT),'started_utc':stamp(),'output_log':str(log)}
    path.write_text(json.dumps(row,indent=2)+'\n')
    result['commands'].append({'label':label,'record':str(path)})
    save()
    with log.open('wb') as handle:process=subprocess.run(row['argv'],cwd=ROOT,stdout=handle,stderr=subprocess.STDOUT)
    row.update(exit_code=process.returncode,finished_utc=stamp())
    path.write_text(json.dumps(row,indent=2)+'\n')
    result['commands'][-1]['exit_code']=process.returncode
    save()
    print(json.dumps({'label':label,'exit_code':process.returncode,'record':str(path)}),flush=True)
    return process.returncode
save()
try:
    sys.path.insert(0,str(ROOT/'tools'))
    import p1r_qualification as preparation
    freeze=json.loads(a.freeze.read_bytes())
    result['source_freeze']={'path':str(a.freeze),'sha256':sha(a.freeze)}
    assert preparation.digest(preparation.canonical(preparation.source_binding(ROOT,a.source_root)))==freeze['canonical_binding_sha256']
    smoke=json.loads((a.smoke/'smoke.json').read_bytes())
    smoke_wrapper=a.smoke.with_name(a.smoke.name+'-isolation')/'isolation.json'
    wrapper=json.loads(smoke_wrapper.read_bytes())
    assert smoke['errors']==smoke['executor_errors']==[] and smoke['finished_utc'] and smoke['case']['assertions']['failed']==0
    assert wrapper['passed'] is True and wrapper['errors']==[] and wrapper['command_exit_code']==0
    result['preceding_smoke']={'path':str(a.smoke/'smoke.json'),'sha256':sha(a.smoke/'smoke.json'),
                               'isolation_sha256':sha(smoke_wrapper)}
    save()
    capture=BASE/'capture-outer-identities.py'
    before=BASE/'outer-shared-before-canonical-v4.json'
    assert command('outer-before',['python3',capture,'--out',before,'--before',a.prior_snapshot,'--before',a.original_snapshot])==0
    identity=uuid.uuid4().hex
    observation=BASE/('observation-'+identity)
    result['observation']=str(observation)
    save()
    assert command('observe',['python3',ROOT/'tools/p1r-published-executor.py','observe','--out',observation,
                             '--planning-observations',BASE/'planning-observations.json',
                             '--selection-reference','approved spec-6-1-p1r-31251-published-qualification.md; 2026-10-18 approve-and-continue',
                             '--source-root',a.source_root],identity)==0
    inputs=observation/'owner-inputs.json'
    decisions=observation/'owner-decisions.json'
    assert json.loads(inputs.read_bytes())['candidate']['version']=='3.125.1'
    result['inputs']={'path':str(inputs),'sha256':sha(inputs)}
    result['decisions']={'path':str(decisions),'sha256':sha(decisions)}
    identity=uuid.uuid4().hex
    execution=BASE/('execution-'+identity)
    result['execution']=str(execution)
    save()
    run_code=command('run',['python3',ROOT/'tools/p1r-published-executor.py','run','--out',execution,
                            '--inputs',inputs,'--source-root',a.source_root],identity)
    identity=uuid.uuid4().hex
    audit=BASE/('audit-v2-'+identity)
    result['audit']=str(audit)
    save()
    audit_code=command('audit-v2',['python3',ROOT/'tools/p1r-isolated-runner.py','--out',audit.with_name(audit.name+'-isolation'),
                                  '--source-root',a.source_root,'--','python3',BASE/'audit-31251-v2.py',
                                  '--verifier-root',ROOT,'--source-root',a.source_root,'--execution',execution,
                                  '--inputs',inputs,'--decisions',decisions,'--out',audit],identity)
    assert command('outer-after',['python3',capture,'--out',BASE/'outer-shared-after-canonical-v4.json',
                                  '--before',before,'--before',a.original_snapshot])==0
    assert sha(Path(__file__))==result['driver_sha256']
    assert run_code==audit_code==0,'canonical execution or independent audit retained a nonzero exit'
except BaseException as error:
    result['errors'].append({'type':type(error).__name__,'message':str(error)})
finally:
    result['finished_utc']=stamp()
    save()
print(json.dumps({key:result.get(key) for key in ('finished_utc','errors','execution','audit','qualified','p1r_usable')}),flush=True)
raise SystemExit(2 if result['errors'] else 0)
