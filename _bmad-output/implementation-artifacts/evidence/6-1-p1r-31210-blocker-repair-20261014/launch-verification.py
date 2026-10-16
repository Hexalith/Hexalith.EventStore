#!/usr/bin/env python3
"""Run a fresh private repair attempt with separately owned outer Docker activity."""
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import sys
import time
import uuid

REPO=Path(__file__).resolve().parents[4]
EVIDENCE=Path(__file__).resolve().parent
sys.path.insert(0,str(REPO/'tools'))
import p1r_qualification as p

BASE=EVIDENCE/('verification-'+uuid.uuid4().hex)
BASE.mkdir(exist_ok=False)
ATTEMPT=BASE/'attempt'; ISOLATION=BASE/'isolation'
ENV={k:v for k,v in os.environ.items() if not k.startswith('DOCKER_')}
outer_host=subprocess.check_output(['docker','context','inspect','--format','{{.Endpoints.docker.Host}}']).decode().strip()
outer_id=subprocess.check_output(['docker','--host',outer_host,'info','--format','{{.ID}}'],env=ENV).decode().strip()
nonce=uuid.uuid4().hex
private_command=['python3',str(EVIDENCE/'verify-and-audit.py'),'--attempt',str(ATTEMPT),'--repo',str(REPO)]
argv=[sys.executable,str(REPO/'tools/p1r-isolated-runner.py'),'--out',str(ISOLATION),'--outer-host',outer_host,'--',*private_command]
launch={'scope':'existing-package-fixture-repair-verification','started_utc':p.stamp(),
        'repo':str(REPO),'attempt':str(ATTEMPT),'isolation':str(ISOLATION),'argv':argv,
        'private_command':private_command,'outer_host':outer_host,'outer_daemon_id':outer_id,
        'outer_control_invocation':nonce,'outer_activity':str(BASE/'outer-activity.json'),
        'script_sha256':{name:p.digest(p.regular(EVIDENCE/name)) for name in ('verify-and-audit.py','verify-repair.py','independent-repair-audit.py')},
        'final_audit_sha256':p.digest(p.regular(EVIDENCE/'final-controller-audit.py')),
        'launcher_sha256':p.digest(p.regular(Path(__file__))), 'errors':[], 'p1r_usable':False,'story_6_1':'blocked'}
p.write_json(BASE/'launch-start.json',launch)
p.write_json(BASE/'launch.json',launch)
print(json.dumps({'verification':str(BASE),'started':True}),flush=True)


def outer_activity():
    name='p1r-outer-control-'+nonce
    result={'started_utc':p.stamp(),'name':name,'label_key':'hexalith.p1r.outer-control','label_value':nonce,
            'outer_daemon_id':outer_id,'commands':[],'passed':False,'errors':[],'container':None}
    def command(arguments,check=True):
        argv=['docker','--host',outer_host,*arguments]
        started=p.stamp()
        completed=subprocess.run(argv,env=ENV,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,check=False,timeout=45)
        result['commands'].append({'argv':argv,'started_utc':started,'finished_utc':p.stamp(),
            'exit_code':completed.returncode,'output':completed.stdout.decode(errors='replace'),
            'output_sha256':p.digest(completed.stdout)})
        p.write_json(BASE/'outer-activity.json',result)
        p.require(not check or completed.returncode==0,'owned outer control command failed')
        return completed
    fmt='{"Id":{{json .Id}},"Image":{{json .Image}},"Name":{{json .Name}},"State":{"Running":{{json .State.Running}},"StartedAt":{{json .State.StartedAt}}},"Labels":{"hexalith.p1r.outer-control":{{json (index .Config.Labels "hexalith.p1r.outer-control")}}}}'
    try:
        created=command(['create','--cidfile',str(BASE/'outer-control.cid'),'--name',name,'--label','hexalith.p1r.outer-control='+nonce,'--network','none',
            'redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf'])
        identity=(BASE/'outer-control.cid').read_text().strip()
        p.require(re.fullmatch('[0-9a-f]{64}',identity) is not None,'owned outer control ID absent')
        result['container']=identity
        command(['start',identity])
        running=json.loads(command(['inspect','--format',fmt,identity]).stdout)
        p.require(running['Id']==identity and running['Name']=='/'+name and running['State']['Running'] is True
                  and running['Labels']['hexalith.p1r.outer-control']==nonce,'outer running state/ownership differs')
        command(['stop','-t','1',identity])
        stopped=json.loads(command(['inspect','--format',fmt,identity]).stdout)
        p.require(stopped['Id']==identity and stopped['State']['Running'] is False
                  and stopped['Labels']['hexalith.p1r.outer-control']==nonce,'outer stopped state/ownership differs')
        command(['rm','-f',identity])
        absent=command(['inspect','--format',fmt,identity],False)
        p.require(absent.returncode!=0 and b'no such' in absent.stdout.lower(),'owned outer control absence unavailable')
        result['passed']=True
    except (Exception,KeyboardInterrupt) as error:
        result['errors'].append(type(error).__name__+': '+str(error))
    finally:
        # Creation may finish before a client interruption. Recover only by our
        # registered exact name and nonce; never select arbitrary host resources.
        observed=command(['inspect','--format',fmt,name],False)
        if observed.returncode==0:
            row=json.loads(observed.stdout)
            if row['Name']=='/'+name and row['Labels']['hexalith.p1r.outer-control']==nonce:
                command(['rm','-f',row['Id']])
            else:
                result['errors'].append('outer control ownership mismatch; resource preserved')
        elif b'no such' not in observed.stdout.lower():
            result['errors'].append('outer control final absence unavailable')
        result['finished_utc']=p.stamp()
        result['passed']=result['passed'] and not result['errors']
        p.write_json(BASE/'outer-activity.json',result)
    print(json.dumps({'outer_activity':result['passed'],'container':result['container']}),flush=True)


def interrupt(signum,frame):raise KeyboardInterrupt('launcher interrupted')
signal.signal(signal.SIGTERM,interrupt)
wrapper=None;identity=None
try:
    with (BASE/'console.txt').open('wb') as console:
        wrapper=subprocess.Popen(argv,cwd=REPO,stdout=console,stderr=subprocess.STDOUT,env=ENV,start_new_session=True)
        identity=p.identity(p.process_state(wrapper.pid))
        deadline=time.monotonic()+7200;next_heartbeat=0;activity_done=False
        while wrapper.poll() is None:
            if (ATTEMPT/'private-live-ready.json').is_file() and not activity_done:
                outer_activity();activity_done=True
            if time.monotonic()>=deadline:raise TimeoutError('private repair verification timed out')
            if time.monotonic()>=next_heartbeat:
                phase=('measurement' if (ATTEMPT/'private-live-ready.json').is_file() else 'private consumers')
                if (ATTEMPT/'repair-measurement-summary.json').is_file():phase='independent audit'
                print(json.dumps({'verification':str(BASE),'phase':phase}),flush=True)
                next_heartbeat=time.monotonic()+30
            time.sleep(1)
        launch['wrapper_exit_code']=wrapper.returncode
        if not activity_done:launch['errors'].append('private operational readiness never reached; no outer activity')
except (Exception,KeyboardInterrupt) as error:
    launch['errors'].append(type(error).__name__+': '+str(error))
finally:
    if wrapper is not None and wrapper.poll() is None:
        if p.same_process(identity,p.process_state(wrapper.pid)):p.signal_owned(identity,signal.SIGINT)
        try:wrapper.wait(timeout=60)
        except subprocess.TimeoutExpired:launch['errors'].append('owned wrapper graceful shutdown still pending')
    launch['wrapper_exit_code']=wrapper.returncode if wrapper is not None else 127
    launch['finished_utc']=p.stamp()
    p.write_json(BASE/'launch.json',launch)
    print(json.dumps({'verification':str(BASE),'wrapper_exit_code':launch['wrapper_exit_code'],'errors':launch['errors']}),flush=True)

final_argv=[sys.executable,str(EVIDENCE/'final-controller-audit.py'),'--repo',str(REPO),'--launch',str(BASE/'launch.json'),
            '--out',str(BASE/'final-independent-validation.json')]
final=subprocess.run(final_argv,cwd=REPO,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,check=False)
(BASE/'final-audit-console.txt').write_bytes(final.stdout)
p.write_json(BASE/'final-audit-command.json',{'argv':final_argv,'exit_code':final.returncode,
             'output_sha256':p.digest(final.stdout),'output':final.stdout.decode(errors='replace')})
print(final.stdout.decode(errors='replace'),flush=True)
raise SystemExit(0 if launch['wrapper_exit_code']==0 and final.returncode==0 and not launch['errors'] else 2)
