"""Seal a review index; preserve original physical evidence locations."""
import hashlib,json,sys
from pathlib import Path
root=Path(__file__).resolve().parents[4]
base=Path(__file__).resolve().parent
sys.path.insert(0,str(root/'tools'))
import p1r_qualification as preparation
import p1r_published_qualification as qualification
source=Path('/tmp/p1r-31251-tagged-source-import-da30571ce90c4296ba51b1d98b3c1775')
target=base/'handoff-v4.json'
preparation.require(not target.exists(),'handoff already exists')
index=json.loads((base/'canonical-v4-index.json').read_bytes())
run=Path(index['execution'])
audit_path=Path(index['audit'])
audit=json.loads((audit_path/'audit.json').read_bytes())
execution=json.loads((run/'execution.json').read_bytes())
freeze=json.loads((base/'verification-import/final-source-freeze-v4.json').read_bytes())
binding=preparation.source_binding(root,source)
preparation.require(preparation.digest(preparation.canonical(binding))==freeze['canonical_binding_sha256'],
                    'final frozen verifier/source closure changed')
inputs=json.loads(Path(index['inputs']['path']).read_bytes())
qualification.validate_candidate_source(binding,inputs)
preparation.require(execution['errors']==[] and audit['errors']==[] and audit['valid_evidence'] is True
                    and audit['case_count']==163 and index['errors']==[] and index['finished_utc'],
                    'canonical execution or independent audit incomplete')
producer=json.loads((run/'executor-source.json').read_bytes())
preparation.check_current_source(producer,check_python=False)
wrapper_path=run.with_name(run.name+'-isolation')
wrapper=json.loads((wrapper_path/'isolation.json').read_bytes())
private=json.loads((wrapper_path/'private-execution.json').read_bytes())
audit_wrapper_path=audit_path.with_name(audit_path.name+'-isolation')
audit_wrapper=json.loads((audit_wrapper_path/'isolation.json').read_bytes())
preparation.require(wrapper['passed'] is True and audit_wrapper['passed'] is True
                    and wrapper['command_exit_code']==audit_wrapper['command_exit_code']==0,
                    'final wrapper failed')
preparation.require(private['proof']['dotnet_version']=='10.0.401'
                    and private['proof']['python_version']==[3,12,12], 'private toolchain differs')
commands={row['label']:row for row in audit['commands']}
preparation.require(commands['measure-boundary']['exit_code']==0
                    and commands['strict-prepare']['exit_code']==commands['strict-validate']['exit_code']==2,
                    'measurement or strict refusal differs')
outer=json.loads((base/'outer-shared-after-canonical-v4.json').read_bytes())
receipts=[]
for name in execution['receipts']:
    path=Path(name)
    value=json.loads(path.read_bytes())
    lane=value.get('lane',value.get('role','container-cleanup'))
    receipts.append({'lane':lane,'path':str(path),'sha256':preparation.digest(path.read_bytes()),
                     'assertions':value['assertions'],
                     'failed_case_checks':{row['id']:[check['id'] for check in row['checks'] if not check['passed']]
                                           for row in value.get('cases',[]) if row['assertions']['failed']}})
names=('README.md','HANDOFF.md','canonical-v4-index.json','canonical-v4-driver-command.json',
       'planning-observations.json','fresh-observation-summary-v4.json',
       'verification-import/final-source-freeze-v4.json','verification-import/final-source-binding-host-v4.json',
       'verification-import/seven-suites.json','verification-import/grammar-repair-with-readme.patch',
       'verification-import/repair-source-manifest.json','verification-import/development-attempt-notes.json',
       'diagnostic-7d9c2147a13a4903b5b6193859d7cbb4/diagnostic.json',
       'snapshot-smoke-v4-index.json','first-snapshot-control-setup-failure.json','snapshot-smoke-v2-lineage.json',
       'smoke-input-copy/copy-manifest.json','snapshot-smoke-v4-index-v2.json','snapshot-get-smoke-v4-final-proof.json',
       'source-tenant-b-failure-observation-v4.json','restore-failure-observation-v4.json',
       'finalized-failed-receipt-index-v4.json','measured-checkout-witness-command-v4.json',
       'final-source-consumer-independent-proof-v4.json','outer-shared-before-canonical-v4.json',
       'outer-shared-after-canonical-v4.json','spec-final-notes-preservation-v4.json')
paths=[base/name for name in names]
paths += [run/'execution.json',run/'executor-source.json',run/'commands.json',
          wrapper_path/'isolation.json',wrapper_path/'private-execution.json',
          audit_path/'audit.json',audit_path/'source-binding.json',audit_path/'global.json',
          audit_path/'successor-support-boundary.json',audit_wrapper_path/'isolation.json',
          audit_wrapper_path/'private-execution.json',Path(__file__).resolve(),
          root/'_bmad-output/implementation-artifacts/spec-6-1-p1r-31251-published-qualification.md']
references=[{'path':str(path),'sha256':preparation.digest(preparation.regular(path))} for path in paths]
result={'schema':'hexalith.p1r.implementation-handoff.v1','completed_utc':preparation.stamp(),
        'scope':'completed implementation and actual measurement; qualification remains false',
        'verifier_root':str(root),'source_root':str(source),'verifier_head':binding['main']['head'],
        'tag_commit':binding['candidate_source']['main']['head'],
        'canonical_host_binding_sha256':freeze['canonical_binding_sha256'],
        'actual_private_producer_binding_sha256':preparation.digest((run/'executor-source.json').read_bytes()),
        'execution_builds_commit':'1dfc39e3a3e6ab1c8ce95138490b39d76e7feb2c',
        'tag_builds_commit':'4629ff2d3bd66376b29a553663c241d3a7b87853',
        'tests':370,'skips':0,'main_lanes':19,'main_cases':audit['case_count'],
        'main_assertions':audit['lane_assertions'],
        'recovery':{name:row['assertions'] for name,row in audit['recovery'].items()},
        'package_roles':{name:row['assertions'] for name,row in audit['packages'].items()},
        'process_control_assertions':audit['process_control_assertions'],
        'executor_errors':execution['errors'],'audit_errors':audit['errors'],
        'audit_valid_evidence':audit['valid_evidence'],'wrapper_passed':wrapper['passed'],
        'audit_wrapper_passed':audit_wrapper['passed'],
        'strict_commands':{name:{'exit_code':row['exit_code'],'stderr':row['stderr']}
                           for name,row in commands.items()},
        'decisions':audit['decisions'],'boundary_status':'measured-pending-disposition',
        'outer_identity_comparisons':[{key:row[key] for key in ('before','before_sha256','before_count','after_count',
                                      'original_identities_preserved','entire_set_unchanged','added','removed','changed')}
                                     for row in outer['comparisons']],
        'frozen_source_rechecked_after_all_work':True,'host_producer_source_check_python':False,
        'host_producer_python_check_reason':'host reader differs; the independent private audit rechecked the actual producer interpreter',
        'no_original_workspace_writes_by_implementation_agent':True,'do_not_copy_v4_tooling_over_parent_merge':True,
        'retained_earlier_verifiers':['/tmp/p1r-31251-verifier-38d60cf41afc4f2d99b4b377c016f95c',
                                    '/tmp/p1r-31251-verifier-final-78617511ec9c4208b1267e1cde6e21bf'],
        'retained_earlier_sources':['/tmp/p1r-31251-tagged-source-fe336e1f52b0487c8bb6a41229063d28',
                                   '/tmp/p1r-31251-tagged-source-final-60e77a9db287420b8a913e014956015f'],
        'copy_semantics':'copies are indexed copies only; original physical paths/CWDs remain required',
        'production_root_cause_claimed':False,'production_repair_performed':False,
        'qualified':False,'p1r_usable':False,'receipts':receipts,'artifact_references':references}
preparation.write_json(target,result)
print(json.dumps({key:result[key] for key in ('completed_utc','canonical_host_binding_sha256','main_assertions',
                                           'audit_valid_evidence','qualified','p1r_usable')},sort_keys=True))
