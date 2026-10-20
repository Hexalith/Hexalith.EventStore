import json
from pathlib import Path
base=Path(__file__).resolve().parent
index=base/'canonical-v4-index.json'
if not index.exists():
    print('Canonical driver not started')
    raise SystemExit()
r=json.loads(index.read_bytes())
print({key:r.get(key) for key in ('started_utc','finished_utc','errors','observation','execution','audit')})
if r.get('execution'):
    run=Path(r['execution'])
    if (run/'execution.json').exists():
        execution=json.loads((run/'execution.json').read_bytes())
        print('executor',{'finished_utc':execution.get('finished_utc'),'errors':execution.get('errors'),
                          'commands':execution.get('command_count'),'receipts':len(execution.get('receipts',[]))})
        for name in execution.get('receipts',[]):
            receipt=json.loads(Path(name).read_bytes())
            lane=receipt.get('lane')
            if lane is None:
                lane=(receipt['role']+'-restore') if 'role' in receipt else 'container-cleanup'
            print(lane,receipt.get('assertions'),receipt.get('execution'),receipt.get('compatibility'))
    if (run/'commands.json').exists():
        commands=json.loads((run/'commands.json').read_bytes())
        if commands:
            command=commands[-1]
            print('last retained command',{key:command.get(key) for key in ('id','exit_code','started_utc','finished_utc')},
                  command.get('argv',[])[:3])
if r.get('audit') and (Path(r['audit'])/'audit.json').exists():
    audit=json.loads((Path(r['audit'])/'audit.json').read_bytes())
    print('audit',{key:audit.get(key) for key in ('finished_utc','valid_evidence','errors')},
          'packages',list(audit['packages']),'lanes',list(audit['lanes']),'recovery',list(audit['recovery']))
