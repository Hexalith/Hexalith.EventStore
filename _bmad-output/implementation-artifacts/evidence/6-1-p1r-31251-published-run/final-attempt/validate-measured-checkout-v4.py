import json,sys
from pathlib import Path
root=Path(__file__).resolve().parents[4]
sys.path.insert(0,str(root/'tools'))
import p1r_check_witnesses as witnesses
base=Path(__file__).resolve().parent
index=json.loads((base/'canonical-v4-index.json').read_bytes())
run=Path(index['execution'])
execution=json.loads((run/'execution.json').read_bytes())
for name in execution['receipts']:
 receipt=json.loads(Path(name).read_bytes())
 if receipt.get('lane')!='checkout':continue
 evidence=receipt['execution_evidence']
 for row,case in zip(receipt['cases'],evidence['cases']):
  witnesses.validate_case(row,case,evidence['executor_source'],'checkout',evidence['predicate_sources'],candidate_version='3.125.1')
 print(json.dumps({'scope':'read-only pre-finalization canonical witness consistency; no qualification','cases':len(receipt['cases']),'assertions':receipt['assertions'],'failed_ids':{row['id']:[check['id'] for check in row['checks'] if not check['passed']] for row in receipt['cases'] if row['assertions']['failed']},'qualified':False,'p1r_usable':False}))
 break
else:raise RuntimeError('checkout receipt absent')
