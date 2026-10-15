#!/usr/bin/env python3
"""Measure fixture repair against signed 3.121.0; never qualify a successor or bypass run admission."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import uuid

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--attempt', type=Path, required=True)
parser.add_argument('--repo', type=Path, required=True)
args = parser.parse_args()
REPO = args.repo.resolve()
sys.path.insert(0, str(REPO / 'tools'))
import p1r_published_executor as e
import p1r_qualification_runtime as runtime
import p1r_isolation as isolation

OUT = args.attempt.absolute()
e.require(OUT == OUT.resolve() and OUT.is_relative_to(REPO), 'measurement attempt must be inside the mounted owner repository')
private = isolation.private_proof(os.environ.get(isolation.OUTER_MARKER, ''))
binding_path = OUT / 'scripts-binding.json'
binding_data = e.regular(binding_path)
binding = json.loads(binding_data)
e.require(set(binding['scripts']) == {'verify-repair.py','independent-repair-audit.py','verify-and-audit.py'}, 'executed script inventory differs')
for name,row in binding['scripts'].items():
    e.require(e.digest(e.regular(Path(row['executed_path']))) == row['sha256']
              and e.digest(e.regular(Path(row['source_path']))) == row['sha256'], 'executed script changed: ' + name)
e.require(binding['scripts']['verify-repair.py']['executed_path'] == str(Path(__file__).resolve()), 'unfrozen repair driver refused')
with (OUT / 'measurement-started.json').open('x') as marker:
    json.dump({'started_utc':e.stamp(),'private_proof':private,'driver_sha256':e.digest(Path(__file__).read_bytes()),
               'scripts_binding_sha256':e.digest(binding_data)}, marker, sort_keys=True)
OLD = REPO / '_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-published-run'
INPUTS = OLD / 'owner-inputs.json'
RUN = OUT / 'measurement'
LANES = ('metadata-write', 'retained-covered', 'retained-uncovered', 'missing-event',
         'invalid-evidence', 'checkout', 'post-upgrade-restore', 'failure-cleanup',
         'reminder-recovery', 'logical-event-evolution')


def cmd(argv, check=True):
    result = subprocess.run([str(a) for a in argv], cwd=REPO, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, check=False)
    e.write_json(OUT / ('control-command-' + uuid.uuid4().hex + '.json'),
                 {'argv':[str(a) for a in argv], 'exit_code':result.returncode,
                  'output':result.stdout.decode(errors='replace'), 'output_sha256':e.digest(result.stdout)})
    e.require(not check or result.returncode == 0, 'repair control command failed')
    return result.stdout


def production_guard():
    tag = cmd(['git','rev-parse','v3.121.0^{commit}']).decode().strip()
    e.require(tag == '21b34c01ec919769cfba9cfe0d47e50886b57ded', 'published tag changed')
    delta = cmd(['git','diff','--binary','v3.121.0','--','src'])
    untracked = cmd(['git','ls-files','--others','--exclude-standard','--','src'])
    e.require(not delta and not untracked, 'repair comparison has production changes')
    return {'tag_commit':tag, 'current_head':cmd(['git','rev-parse','HEAD']).decode().strip(),
            'production_diff_sha256':e.digest(delta), 'new_production_files':[],
            'claim':'current production bytes equal published source; tooling/tests differ; no immutable qualification claim'}


class SerializedExecutor(e.Executor):
    def run(self, argv, *args, **kwargs):
        if [str(a) for a in argv[:2]] == ['dotnet','restore'] and 'consumers/source/' in str(argv[2]):
            argv = list(argv) + ['--disable-parallel','-p:RestoreDisableParallel=true','-m:1']
        return super().run(argv, *args, **kwargs)


# Ignore prior local consumer build outputs; each copied consumer is independently built.
copytree = shutil.copytree
def private_copy(source, destination, *args, **kwargs):
    if Path(source) == e.CONSUMERS:
        kwargs['ignore'] = shutil.ignore_patterns('bin','obj','packages')
    return copytree(source,destination,*args,**kwargs)
shutil.copytree = private_copy

selection, data, scope = e.qualification.load_document(INPUTS, e.qualification.validate_inputs)
e.require(scope == 'owner-selected', 'repair needs retained exact package selection')
proof = production_guard()
# Preserve and demonstrate the official run's exact-tag admission. This script is
# separate operational repair verification, not an invocation of Executor.execute.
try:
    e.validate_execution_inputs(selection)
    admission = {'accepted':True,'claim':'official run admission unchanged'}
except e.InvalidPacket as error:
    admission = {'accepted':False,'reason':str(error),'claim':'official run admission unchanged; separate repair measurements only'}
    e.require(str(error) == 'successor tag, exact source, or tag Builds identity substituted',
              'unexpected selected-input refusal')
e.write_json(OUT/'repair-plan.json', {'scope':'nonqualifying-existing-package-fixture-repair',
    'run':str(RUN),'inputs_sha256':e.digest(data),'scripts_binding_sha256':e.digest(binding_data),'production':proof,'official_run_admission':admission,
    'lanes':LANES,'p1r_usable':False,'story_6_1':'blocked'})

sentinel_id = None
sentinel_name = 'p1r-repair-sentinel-' + uuid.uuid4().hex
sentinel_label = 'hexalith.p1r.repair=' + uuid.uuid4().hex
worker = None
report = {'scope':'nonqualifying-existing-package-fixture-repair','run':str(RUN),
          'started_utc':e.stamp(),'errors':[],'p1r_usable':False,'story_6_1':'blocked',
          'four_role_acceptance':'pending','support_disposition':'pending','conformance':'pending'}
try:
    sentinel_id = cmd(['docker','create','--name',sentinel_name,'--label',sentinel_label,
                       '--network','none',e.REDIS]).decode().strip()
    cmd(['docker','start',sentinel_id])
    # A private resource change must still fail the original exact equality predicate.
    negative = e.Executor(OUT/('negative-isolation-' + uuid.uuid4().hex))
    e.require(sentinel_id in negative.shared_before, 'negative control lacks private sentinel')
    cmd(['docker','stop','-t','1',sentinel_id])
    negative_receipt = negative.cleanup()
    e.require([c['id'] for c in negative_receipt['checks'] if not c['passed']] == ['shared-preserved'],
              'private sentinel mutation did not fail strict equality alone')
    observed = e.docker_observations(cmd(e.safe_inspect_argv(sentinel_id)))[0]
    e.require(observed['Id'] == sentinel_id and not observed['State']['Running'],
              'negative cleanup removed or changed unowned sentinel')
    cmd(['docker','start',sentinel_id])
    report['negative_isolation_control'] = {'receipt':negative.result['consumer_cleanup'],
        'failed_checks':['shared-preserved'],'sentinel_preserved':True}

    worker = SerializedExecutor(RUN, selection)
    worker.inputs_sha256 = e.digest(data)
    worker.result.update(inputs_sha256=worker.inputs_sha256,
        repair_verification_scope='existing-3.121.0-packages-with-repaired-fixture; no immutable successor qualification',
        repair_driver_sha256=e.digest(Path(__file__).read_bytes()), repair_scripts_binding_sha256=e.digest(binding_data),
        repair_attempt=str(OUT), production_guard=proof)
    worker.support_rules = e.provisional_successor_execution_policy(selection)
    worker.save()
    for version in worker.versions:
        worker.build_consumers(version)
    worker.build_consumers(worker.candidate, source=True)
    worker.verify_selected_candidate_archives()
    worker.topology()
    e.require(sentinel_id in worker.shared_before and worker.shared_before[sentinel_id]['running'],
              'positive equality must include a live nonowned sentinel')
    e.write_json(OUT/'private-live-ready.json',{'run':str(RUN),'sentinel':sentinel_id,
        'private_daemon_id':json.loads(cmd(['docker','info','--format','{{json .ID}}']))})
    for lane in LANES:
        e.preparation.check_current_source(worker.source_binding)
        if lane == 'checkout': action = worker.checkout_case
        elif lane == 'invalid-evidence': action = worker.invalid_case
        elif lane == 'post-upgrade-restore': action = worker.restore_case
        elif lane == 'failure-cleanup': action = worker.failure_case
        elif lane == 'reminder-recovery': action = worker.reminder_case
        elif lane == 'logical-event-evolution': action = worker.evolution_case
        else: action = lambda identifier, checks, lane=lane:worker.live_case(lane,identifier,checks)
        worker.lane(lane,action,True)
except (Exception,KeyboardInterrupt) as error:
    report['errors'].append(type(error).__name__ + ': ' + str(error))
    if worker:
        worker.errors.append(report['errors'][-1])
finally:
    if worker:
        cleanup = worker.cleanup()
        if worker.pending_restore is not None:
            receipt = worker.pending_restore
            receipt['cleanup'] = cleanup
            commands = runtime.validate_commands(receipt['commands'])
            rows = {name:value['rows'] for name,value in receipt['inventories'].items()}
            receipt['checks'] = runtime.restore_observations(receipt,commands,rows)
            receipt['assertions'] = e.checks_counter(receipt['checks'])
            receipt['execution'],receipt['compatibility'] = runtime.disposition('operational',receipt['assertions']['failed']==0)
            runtime.restore_outcome(receipt)
            path=worker.output/'receipts'/(receipt['id']+'.json')
            e.write_json(path,receipt)
            worker.receipts.append(str(path))
        worker.save()
        report['cleanup'] = {'assertions':cleanup['assertions'],'shared':cleanup['shared']}
        report['executor_errors'] = worker.errors
        receipts = [json.loads(Path(p).read_bytes()) for p in worker.receipts]
        lanes = [r for r in receipts if 'lane' in r]
        before = json.loads((OLD/'review-round-1/measurement-summary.json').read_bytes())['nonpassing_cases']
        current = {(r['lane'],c['id']):c for r in lanes for c in r['cases']}
        repaired=[]
        for case in before:
            now=current.get((case['lane'],case['id']),{})
            checks={c['id']:c['passed'] for c in now.get('checks',[])}
            repaired.append({'lane':case['lane'],'case':case['id'],'operation':now.get('operation'),
                'previously_failed_checks':{name:checks.get(name) for name in case['failed_checks']},
                'assertions':now.get('assertions')})
        report['repaired_required_cases']=repaired
        report['lanes']=[{'lane':r['lane'],'assertions':r['assertions']} for r in lanes]
        report['assertions']=e.checks_counter([{'id':r['lane']+'/'+c['id']+'/'+k['id'],'passed':k['passed']}
                                              for r in lanes for c in r['cases'] for k in c['checks']])
        report['restore']=[{'assertions':r['assertions']} for r in receipts if r.get('schema')==runtime.RESTORE_SCHEMA]
        report['verified']=not report['errors'] and not worker.errors and report['assertions']['failed']==0 \
            and len(lanes)==len(LANES) and cleanup['assertions']['failed']==0 \
            and len(report['restore'])==1 and report['restore'][0]['assertions']['failed']==0 \
            and len(repaired)==6 and all(r['operation']=='supported' and all(v is True for v in r['previously_failed_checks'].values()) for r in repaired)
    if sentinel_id:
        try:
            observed=e.docker_observations(cmd(e.safe_inspect_argv(sentinel_id)))[0]
            e.require(observed['Id']==sentinel_id and observed['Name']=='/'+sentinel_name,
                      'owned repair sentinel identity substituted')
            # Read only the one known label; never remove an arbitrary/private unowned resource.
            actual=cmd(['docker','inspect','--format','{{index .Config.Labels "hexalith.p1r.repair"}}',sentinel_id]).decode().strip()
            e.require('hexalith.p1r.repair='+actual==sentinel_label,'owned repair sentinel label substituted')
            cmd(['docker','rm','-f',sentinel_id])
            absent=cmd(e.safe_inspect_argv(sentinel_id),check=False)
            e.require(b'No such' in absent or b'no such' in absent,'owned repair sentinel absence unmeasured')
            report['sentinel_cleanup']='removed-only-after-executor-preserved-it'
        except Exception as error:
            report['errors'].append('sentinel cleanup: '+str(error))
            report['verified']=False
    report['finished_utc']=e.stamp()
    e.write_json(OUT/'repair-measurement-summary.json',report)
    print(json.dumps(report),flush=True)
raise SystemExit(0 if report.get('verified') else 2)
