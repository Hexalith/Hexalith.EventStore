#!/usr/bin/env python3
"""Read-only progress summary; receipt/audit validation remains authoritative."""
import json
from pathlib import Path
base=Path(__file__).resolve().parent
index=json.loads((base/'canonical-final-index.json').read_bytes())
result={'phases':[{k:r.get(k) for k in ('label','exit_code')} for r in index['commands']],'driver_errors':index['errors'],'lanes':[],'recovery':[]}
root=Path(index['execution']) if index.get('execution') else None
if root and (root/'execution.json').exists():
 execution=json.loads((root/'execution.json').read_bytes());result.update(started_utc=execution.get('started_utc'),finished_utc=execution.get('finished_utc'),executor_errors=execution['errors'])
 for name in execution['receipts']:
  receipt=json.loads(Path(name).read_bytes());lane=receipt['lane'];row={'lane':lane,'assertions':receipt['assertions'],'cases':len(receipt.get('cases',[]))}
  row['failed_checks']=[{'case':case['id'],'checks':[c['id'] for c in case['checks'] if not c['passed']]} for case in receipt.get('cases',[]) if case['assertions']['failed']]
  result['recovery' if lane in ('candidate-restore','container-cleanup') else 'lanes'].append(row)
 commands=json.loads((root/'commands.json').read_bytes());result['retained_commands']=len(commands)
 if commands:result['last_finished_command']={k:commands[-1].get(k) for k in ('id','finished_utc','exit_code')}
 result['lane_assertions']={k:sum(r['assertions'][k] for r in result['lanes']) for k in ('attempted','passed','failed')}
 result['case_count']=sum(row['cases'] for row in result['lanes'])
print(json.dumps(result,sort_keys=True))
