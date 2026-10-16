#!/usr/bin/env python3
"""Independently finalize a repair only after private and outer cleanup are complete."""
import argparse
import datetime
import json
import os
import signal
from pathlib import Path
import subprocess
import sys

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--repo',type=Path,required=True)
parser.add_argument('--launch',type=Path,required=True)
parser.add_argument('--out',type=Path,required=True)
args=parser.parse_args()
repo=args.repo.resolve()
sys.path.insert(0,str(repo/'tools'))
import p1r_qualification as p
import p1r_isolation as iso

p.require(not args.out.exists(),'final audit output already exists')
launch=json.loads(p.regular(args.launch))
attempt=Path(launch['attempt']); isolation=Path(launch['isolation'])
EXPECTED_CHECKS=(
    'final-audit-source-bound-before-launch','all-executed-scripts-bound-before-and-after-execution',
    'source-closure-and-actual-private-python-bound','actual-distinct-daemon-namespaces-toolchain-and-command',
    'exact-ownership-pinned-images-only-owned-targets-and-actual-absence',
    'strict-nonempty-equality-repeat-cleanup-and-restore-dependency',
    'independent-measurements-and-six-supported-cases-sixteen-assertions',
    'unrelated-owned-outer-activity-overlaps-and-is-removed',
    'roles-usability-story-and-pinned-profile-remain-pending')
report={'valid_evidence':False,'correction_verified':False,'schema':'hexalith.p1r.repair-final-audit.v1','scope':'verified-existing-package-fixture-repair',
        'started_utc':p.stamp(),'checks':[],'errors':[],'observations':[],
        'four_role_acceptance':'pending','conformance':'pending','support_disposition':'pending',
        'qualified':False,'p1r_usable':False,'story_6_1':'blocked'}

def read(path):return json.loads(p.regular(Path(path)))
def save():p.write_json(args.out,report)
def check(name,call):
    try:
        value=call();report['checks'].append({'id':name,'passed':True});save();return value
    except (Exception,KeyboardInterrupt) as error:
        message=type(error).__name__+': '+str(error)
        report['checks'].append({'id':name,'passed':False,'reason':message})
        report['errors'].append(name+': '+message);save()
        if isinstance(error,KeyboardInterrupt):raise
def require(value,message):p.require(value,message)
def docker(arguments,absence=False):
    environment={k:v for k,v in os.environ.items() if not k.startswith('DOCKER_')}
    argv=['docker','--host',launch['outer_host'],*arguments]
    result=subprocess.run(argv,env=environment,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,check=False,timeout=45)
    row={'argv':argv,'exit_code':result.returncode,'output':result.stdout.decode(errors='replace'),
         'output_sha256':p.digest(result.stdout)}
    report['observations'].append(row);save()
    if absence:require(result.returncode!=0 and b'no such' in result.stdout.lower(),'owned resource still exists or absence unavailable')
    else:require(result.returncode==0,'final read-only Docker observation failed')
    return result.stdout

def interrupted(signum,frame):
    report['interrupted_signal']=signum
    raise KeyboardInterrupt('final audit interrupted')
previous_signals={sig:signal.signal(sig,interrupted) for sig in (signal.SIGINT,signal.SIGTERM)}

try:
    outer=read(isolation/'isolation.json'); private=read(isolation/'private-execution.json')
    binding=read(attempt/'scripts-binding.json'); orchestration=read(attempt/'orchestration.json')
    measurement=read(attempt/'repair-measurement-summary.json')
    audit=read(attempt/'independent-verification/independent-validation.json')
    execution=read(Path(measurement['run'])/'execution.json')
    proof=private['proof']; measured_proof=read(attempt/'measurement-started.json')['private_proof']
    report.update(attempt=str(attempt),isolation=str(isolation),run=measurement['run'],
                  inputs_sha256=execution['inputs_sha256'],assertions=measurement['assertions'],
                  six_repaired_cases=measurement['repaired_required_cases'])
    check('final-audit-source-bound-before-launch',lambda:require(
        p.digest(p.regular(Path(__file__)))==launch['final_audit_sha256'],'final audit source differs from launch'))
    def scripts():
        require(set(binding['scripts'])==set(launch['script_sha256']),'script closure inventory differs')
        for name,row in binding['scripts'].items():
            require(row['sha256']==launch['script_sha256'][name],'executed source differs from pre-launch reviewed source')
            require(row['executed_path']==str(attempt/'scripts'/name),'frozen script coordinate differs')
            require(all(p.digest(p.regular(Path(row[key])))==row['sha256'] for key in ('source_path','executed_path')),
                    'script source changed before final audit')
        sha=p.digest(p.regular(attempt/'scripts-binding.json'))
        require(orchestration['scripts_binding_sha256']==sha and audit['scripts_binding_sha256']==sha,
                'script closure differs between stages')
        require(execution['repair_driver_sha256']==binding['scripts']['verify-repair.py']['sha256'],
                'executor omitted or substituted the frozen repair driver')
    check('all-executed-scripts-bound-before-and-after-execution',scripts)
    def source():
        expected=private['source_binding'];actual=p.source_binding(repo)
        require({k:v for k,v in expected.items() if k!='python'}=={k:v for k,v in actual.items() if k!='python'},
                'reviewed source/configuration closure changed')
        require(proof['executables']['python']['sha256']==expected['python']['sha256'],
                'private Python differs from source binding')
        require(read(Path(measurement['run'])/'executor-source.json')==expected,'measurement differs from private source closure')
    check('source-closure-and-actual-private-python-bound',source)
    def relationship():
        require(outer['passed'] is True and private['passed'] is True and outer['command_exit_code']==0
                and private['exit_code']==0 and launch['wrapper_exit_code']==0,'private or outer execution failed')
        require(outer['invocation']==private['invocation'],'private invocation differs from owned controller')
        require(private['command']==launch['private_command'],'private command was substituted')
        require(proof['outer_daemon_id']==outer['outer_daemon_id']==launch['outer_daemon_id'],
                'outer endpoint binding differs')
        require(proof['daemon']['ID'] and proof['daemon']['ID']!=proof['outer_daemon_id'],
                'private daemon is the shared outer daemon')
        require(proof['socket']==iso.SOCKET and proof['daemon']['DockerRootDir']=='/var/lib/docker/data',
                'private socket/root differs')
        for name in ('net','pid'):
            require(proof['namespaces'][name]==proof['daemon_namespaces'][name],'private daemon namespace differs')
        require(measured_proof['daemon']['ID']==proof['daemon']['ID']
                and measured_proof['executables']==proof['executables'],
                'measurement used a different daemon/toolchain')
        require(outer['images']=={'sdk':iso.SDK_IMAGE,'daemon':iso.DAEMON_IMAGE,'python':iso.PYTHON_IMAGE},
                'private tool images changed')
        require(proof['dotnet_version']=='10.0.401','SDK differs from global.json')
        observed=docker(['info','--format','{{.ID}}']).decode().strip()
        require(observed==outer['outer_daemon_id'],'final outer endpoint differs')
    check('actual-distinct-daemon-namespaces-toolchain-and-command',relationship)
    def registry():
        owned=outer['owned_containers'];invocation=outer['invocation']
        require({row['role'] for row in owned.values()}=={'sdk','daemon','python-tools'},'owned outer inventory differs')
        require(outer['owned_volume']=='p1r-isolation-'+invocation,'private volume ownership differs')
        creates={row['output'].strip():row for row in outer['commands'] if row['argv'][5:6]==['create'] and row['exit_code']==0}
        require(set(creates)==set(owned),'owned containers differ from literal creates')
        image_roles={'sdk':iso.SDK_IMAGE,'daemon':iso.DAEMON_IMAGE,'python-tools':iso.PYTHON_IMAGE}
        configs={role:docker(['image','inspect','--format','{{.Id}}',image]).decode().strip()
                 for role,image in image_roles.items()}
        seen=set()
        for row in outer['commands']:
            require(p.digest(row['output'].encode())==row['output_sha256'],'outer command output hash differs')
            argv=row['argv'];require(argv[:3]==['docker','--host',launch['outer_host']],'outer command endpoint differs')
            command=argv[5:]
            if command[:2]==['rm','-f']:
                require(len(command)==3 and command[2] in owned,'cleanup targeted an unowned container')
            if command[:2]==['volume','rm']:
                require(command==['volume','rm',outer['owned_volume']],'cleanup targeted an unowned volume')
            if command[:1]==['inspect'] and row['exit_code']==0:
                observed=json.loads(row['output'])
                if observed.get('Id') in owned:
                    identity=observed['Id'];role=owned[identity]['role']
                    require(observed['Labels'].get(iso.LABEL)==invocation,'observed ownership label differs')
                    require(observed['Name']=='/p1r-isolation-'+invocation+'-'+role,'owned outer name differs')
                    require(observed['Image']==configs[role],'loaded outer tool image differs')
                    seen.add(identity)
        require(seen==set(owned),'missing exact owned outer inspection')
        for identity,row in creates.items():
            argv=row['argv'];require(iso.LABEL+'='+invocation in argv and image_roles[owned[identity]['role']] in argv,
                                    'creation omitted ownership or digest pin')
        runner=outer['runner_proof'];daemon=next(k for k,v in owned.items() if v['role']=='daemon')
        require(runner['NetworkMode']==runner['PidMode']=='container:'+daemon and not runner['Ports']
                and not runner['Privileged'],'runner published ports or escaped private namespaces')
        require({v['Destination'] for v in runner['Mounts']}=={'/var/lib/docker',str(repo.parent.parent)},
                'runner mount inventory differs')
        for identity in owned:docker(['inspect','--format','{{.Id}}',identity],absence=True)
        docker(['volume','inspect',outer['owned_volume']],absence=True)
        scratch=Path(outer['owned_scratch'])
        require(scratch.name.startswith('p1r-isolation-'+invocation+'-') and not scratch.exists(),
                'owned scratch remains or differs')
    check('exact-ownership-pinned-images-only-owned-targets-and-actual-absence',registry)
    def cleanup():
        require(len(outer['cleanup'])>=2 and all(not v['remaining'] and not v['errors'] for v in outer['cleanup']),
                'outer cleanup failed')
        require(len(private['cleanup'])>=2 and all(not v['remaining'] and not v['errors'] for v in private['cleanup']),
                'private process cleanup failed')
        require(outer.get('nested_empty_before_teardown') is True,'private inventory absence not measured before parent teardown')
        require(measurement['cleanup']['assertions']=={'attempted':7,'passed':7,'failed':0}
                and measurement['cleanup']['shared']['complete']
                and bool(measurement['cleanup']['shared']['before'])
                and measurement['cleanup']['shared']['before']==measurement['cleanup']['shared']['after'],
                'original strict equality failed or was empty')
        require(measurement['restore']==[{'assertions':{'attempted':12,'passed':12,'failed':0}}],
                'restore cleanup dependency did not pass')
    check('strict-nonempty-equality-repeat-cleanup-and-restore-dependency',cleanup)
    def independent():
        require(audit['measurement_valid_evidence'] is True and not audit['errors'],
                'independent measurement audit failed')
        require(orchestration['measurement_and_audit_passed'] is True and orchestration['scripts_verified_after_audit'],
                'frozen orchestration failed')
        require(audit['reconstructed_assertions']==measurement['assertions'],'independent assertion reconstruction differs')
        require(len(measurement['repaired_required_cases'])==6 and all(
            case['operation']=='supported' and all(v is True for v in case['previously_failed_checks'].values())
            for case in measurement['repaired_required_cases']),'a required direction was reclassified or remains failed')
        require(sum(len(v['previously_failed_checks']) for v in measurement['repaired_required_cases'])==16,
                'original sixteen failures are not covered')
    check('independent-measurements-and-six-supported-cases-sixteen-assertions',independent)
    def outer_activity():
        activity=read(Path(launch['outer_activity']))
        require(activity['passed'] is True,'outer activity control failed')
        require(activity['container'] not in outer['owned_containers'],'outer activity reused an isolated runner container')
        require(activity['label_key']=='hexalith.p1r.outer-control' and activity['label_value']==launch['outer_control_invocation'],
                'outer control ownership differs from launch')
        label=activity['label_key']+'='+activity['label_value']
        start=datetime.datetime.fromisoformat(execution['started_utc'])
        end=datetime.datetime.fromisoformat(execution['finished_utc'])
        require(start<=datetime.datetime.fromisoformat(activity['started_utc'])<datetime.datetime.fromisoformat(activity['finished_utc'])<=end,
                'outer activity did not overlap private operational execution')
        require(activity['outer_daemon_id']==outer['outer_daemon_id'],'outer activity used another daemon')
        create=[];starts=[];stops=[];removes=[];running=[];stopped=[]
        for row in activity['commands']:
            require(p.digest(row['output'].encode())==row['output_sha256'],'outer activity command output differs')
            require(row['argv'][:3]==['docker','--host',launch['outer_host']],'outer activity endpoint differs')
            argv=row['argv'][3:]
            if argv[:1]==['create']:
                require(row['exit_code']==0 and row['output'].strip().splitlines()[-1]==activity['container']
                        and '--name' in argv and argv[argv.index('--name')+1]==activity['name']
                        and '--label' in argv and argv[argv.index('--label')+1]==label
                        and 'redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf' in argv,
                        'outer control creation/ownership/image was not measured')
                create.append(row)
            elif argv==['start',activity['container']]:
                require(row['exit_code']==0,'outer control start failed');starts.append(row)
            elif argv==['stop','-t','1',activity['container']]:
                require(row['exit_code']==0,'outer control stop failed');stops.append(row)
            elif argv==['rm','-f',activity['container']]:
                require(row['exit_code']==0,'outer control removal failed');removes.append(row)
            elif argv[:1]==['inspect'] and row['exit_code']==0:
                observed=json.loads(row['output'])
                require(observed['Id']==activity['container'] and observed['Name']=='/'+activity['name']
                        and observed['Labels'].get(activity['label_key'])==activity['label_value'],
                        'outer control observation names an unowned identity')
                (running if observed['State']['Running'] else stopped).append(row)
        require(len(create)==len(starts)==len(stops)==len(removes)==1 and running and stopped,
                'missing actual outer creation/start/state-change/removal observations')
        require(create[0]['started_utc']<=starts[0]['started_utc']<=running[0]['started_utc']
                <=stops[0]['started_utc']<=stopped[0]['started_utc']<=removes[0]['started_utc'],
                'outer control lifecycle observations are reordered')
        docker(['inspect','--format','{{.Id}}',activity['container']],absence=True)
    check('unrelated-owned-outer-activity-overlaps-and-is-removed',outer_activity)
    def gates():
        require(not measurement['p1r_usable'] and measurement['story_6_1']=='blocked'
                and all(measurement[k]=='pending' for k in ('four_role_acceptance','conformance','support_disposition')),
                'repair advanced pending gates')
        inputs=read(repo/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-published-run/owner-inputs.json')
        require(inputs['rollback'] is None and inputs['operational_profile']['runtime_version']=='1.18.2'
                and inputs['operational_profile']['backend_image']=='postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636',
                'retained approved profile changed')
    check('roles-usability-story-and-pinned-profile-remain-pending',gates)
except (Exception,KeyboardInterrupt) as error:
    report['errors'].append(type(error).__name__+': '+str(error))
finally:
    for sig,handler in previous_signals.items():signal.signal(sig,handler)
    complete=[row['id'] for row in report['checks']]==list(EXPECTED_CHECKS)
    if not complete:report['errors'].append('final audit did not complete the exact required check inventory')
    report['valid_evidence']=complete and not report['errors'] and all(row['passed'] for row in report['checks'])
    report['correction_verified']=report['valid_evidence']
    report['finished_utc']=p.stamp()
    report['audit_script_sha256']=p.digest(p.regular(Path(__file__)))
    for name,path in [('outer',isolation/'isolation.json'),('private',isolation/'private-execution.json'),
                      ('measurement',attempt/'repair-measurement-summary.json'),
                      ('independent_measurement',attempt/'independent-verification/independent-validation.json')]:
        if path.is_file():report[name+'_sha256']=p.digest(p.regular(path))
    save();print(json.dumps({'out':str(args.out),'valid_evidence':report['valid_evidence'],'errors':report['errors'],'p1r_usable':False}),flush=True)
raise SystemExit(0 if report['valid_evidence'] else 2)
