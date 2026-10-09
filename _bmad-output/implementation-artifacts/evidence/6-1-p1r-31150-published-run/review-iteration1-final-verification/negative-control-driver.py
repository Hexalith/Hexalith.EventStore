from pathlib import Path
import sys,json,copy,tempfile,shutil,datetime
sys.path.insert(0,'/home/administrator/projects/hexalith/projects/references/Hexalith.EventStore/tools')
import p1r_published_qualification as q
import p1r_qualification as p
control=Path('/tmp/p1r-correction-full-iteration5-control');directory=Path('/home/administrator/projects/hexalith/projects/references/Hexalith.EventStore/_bmad-output/implementation-artifacts/evidence/6-1-p1r-31150-published-run/packet-c497b88cb2e5436ebddb37e33f1fcb64')
packet=json.loads((directory/'packet.json').read_bytes())
inputs=q.validate_document(directory,packet['inputs'],'inputs.json',q.validate_inputs)
decisions=q.validate_document(directory,packet['decisions'],'decisions.json',q.validate_decisions)
start,end=q.validate_times(packet)
context={'inputs':inputs,'source_binding':json.loads((directory/'source-binding.json').read_bytes()),'verified':{}}
for role,row in zip(q.package_roles(inputs['value']),packet['lanes']['packages']):
 if q.role_selection(inputs['value'],role) is None:continue
 receipt,outcome,_=q.validate_package_receipt(directory,packet['invocation'],role,row['receipts'][0],inputs,start,end)
 context['verified'][role]=q.verified_identities(receipt,outcome)
def find(lane):
 row=next(row for group in ('scenarios','additions') for row in packet['lanes'][group] if row['id']==lane)
 return row['receipts'][0],json.loads((directory/row['receipts'][0]).read_bytes())
def case_for(receipt,field):
 row=next(row for row in receipt['cases'] if any(check['id']==field and check['passed'] is False for check in row['checks']))
 case=next(case for case in receipt['execution_evidence']['cases'] if case['id']==row['id'])
 return row,case
def flip(receipt,field,operand=False):
 row,case=case_for(receipt,field)
 next(check for check in row['checks'] if check['id']==field)['passed']=True
 if operand:
  witness=next(witness for witness in case['check_witnesses'] if witness['id']==field)
  witness['predicate']['operands'][0]=witness['predicate']['operands'][1]
def inventory(receipt):
 receipt['cases'][0]['inventory']={'before_sha256':'a'*64,'after_sha256':'a'*64}
def configurations(receipt):receipt['execution_evidence']['cases'][0]['configurations']=[]
def source(receipt):
 binding=receipt['execution_evidence']['executor_source'];binding['main']['files'][0]['sha256']='b'*64
 receipt['execution_evidence']['executor_source_sha256']=q.digest(json.dumps(binding,indent=2,sort_keys=True).encode()+b'\n')
def source_hash(receipt):receipt['execution_evidence']['executor_source_sha256']='c'*64
def config_bytes(receipt):
 configuration=receipt['execution_evidence']['cases'][0]['configurations'][0]
 file=configuration['files'][0];file['content']+='\n';file['sha256']=q.digest(file['content'].encode())
def direction(receipt):receipt['cases'].pop();receipt['execution_evidence']['cases'].pop()
def logical(receipt):
 row,case=case_for(receipt,'registered-logical-alias-evolution-executed')
 flip(receipt,'registered-logical-alias-evolution-executed',True)
 case['observations']['domain_registration_observation']['manifest_registered']=True
 case['observations']['registered_logical_alias_evolution']['executed']=True
 row['disposition']='compatible'
def recompute(receipt):
 for row in receipt['cases']:row['assertions']=p.checks_counter(row['checks'])
 receipt['assertions']={key:sum(row['assertions'][key] for row in receipt['cases']) for key in ('attempted','passed','failed')}
 passed=receipt['assertions']['attempted']>0 and receipt['assertions']['failed']==0 and all(row['expected']==row['outcome'] and (row['outcome']!='refusal' or row['inventory']['before_sha256']==row['inventory']['after_sha256']) for row in receipt['cases'])
 receipt['execution']='passed' if passed else 'failed'
 incompatible=any(row['disposition']=='incompatible' or row['operation']=='unsupported' for row in receipt['cases'])
 receipt['compatibility']='incompatible' if incompatible else 'compatible' if passed else 'unverified'
 receipt['output_sha256']=q.digest(p.canonical(receipt['execution_evidence']))
started=p.stamp();checks=[]
mutations=[('flipped-failed-metadata-check','metadata-read',lambda r:flip(r,'retained-floor-read')),
 ('flipped-metadata-check-and-operands','metadata-read',lambda r:flip(r,'retained-floor-read',True)),
 ('invented-equal-inventory-hashes','invalid-evidence',inventory),('removed-runtime-configurations','full-replay',configurations),
 ('substituted-executor-source-recomputed-hash','provenance',source),('substituted-executor-file-hash','provenance',source_hash),
 ('substituted-rendered-config-bytes-recomputed-file-hash','full-replay',config_bytes),('omitted-required-direction','metadata-write',direction),
 ('flipped-query-proof-and-operands','query-wire',lambda r:flip(r,'preserved:identityAdmissionProof',True)),
 ('flipped-projection-position-and-operands','projection-wire',lambda r:flip(r,'preserved:globalPosition',True)),
 ('forged-logical-registration-and-operands','logical-event-evolution',logical)]
for identity,lane,mutate in mutations:
 name,receipt=find(lane);mutate(receipt);recompute(receipt)
 with tempfile.TemporaryDirectory(prefix='p1r-corrected-negative-') as scratch:
  trial=Path(scratch)/'packet';shutil.copytree(directory,trial)
  q.write_json(trial/name,receipt)
  modified=copy.deepcopy(packet)
  group='additions' if lane in q.ADDITIONS else 'scenarios'
  outcome={key:receipt[key] for key in ('execution','compatibility','scope','assertions')}
  modified['lanes'][group][q.INVENTORIES[group].index(lane)]=q.executed_row(lane,outcome,name)
  q.mirror_families(modified['lanes']);modified['evaluation']=q.evaluate(modified['lanes'],inputs,decisions)
  q.write_json(trial/'packet.json',modified);p.seal(trial)
  import_directory=Path(scratch)/'import';(import_directory/'receipts').mkdir(parents=True)
  import_refusal=validation_refusal=None
  try:q.import_receipt(trial/name,import_directory,q.initial_lanes(inputs['value']),context)
  except q.InvalidPacket as error:import_refusal=str(error)
  try:q.validate_packet(trial)
  except q.InvalidPacket as error:validation_refusal=str(error)
  row={'id':identity,'import_refusal':import_refusal,'independent_validation_refusal':validation_refusal,
       'passed':bool(import_refusal and validation_refusal and import_refusal == validation_refusal),'substituted_receipt_sha256':q.digest((trial/name).read_bytes()),
       'resealed_trial_index_sha256':q.digest((trial/'SHA256SUMS').read_bytes())}
  checks.append(row);print(json.dumps(row),flush=True)
  assert row['passed'],identity
result={'schema':'hexalith.p1r.tooling-negative-controls.v1','scope':'tooling-synthetic','fixture':q.SYNTHETIC,
 'qualification_authority':False,'started_utc':started,'finished_utc':p.stamp(),'executor_source_sha256':packet['source_binding_sha256'],
 'checks':checks,'assertions':p.checks_counter([{'id':row['id'],'passed':row['passed']} for row in checks]),
 'final_packet_revalidated':q.validate_packet(directory)['valid']}
q.write_json(control/'retained-negative-controls.json',result)
