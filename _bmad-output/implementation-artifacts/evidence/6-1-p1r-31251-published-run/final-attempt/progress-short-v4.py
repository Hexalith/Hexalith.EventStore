import json
from pathlib import Path
b=Path(__file__).resolve().parent
i=json.loads((b/'canonical-v4-index.json').read_bytes())
r={'driver_finished':i['finished_utc'],'driver_errors':i['errors'],'execution':i.get('execution'),'audit':i.get('audit')}
if i.get('execution'):
    p=Path(i['execution'])
    if (p/'execution.json').exists():
        e=json.loads((p/'execution.json').read_bytes())
        rows=[json.loads(Path(name).read_bytes()) for name in e['receipts']]
        main=[row for row in rows if 'lane' in row]
        r.update(executor_finished=e['finished_utc'],executor_errors=e['errors'],lanes=[row['lane'] for row in main],
                 cases=sum(len(row['cases']) for row in main),
                 assertions={key:sum(row['assertions'][key] for row in main) for key in ('attempted','passed','failed')})
        r['recovery']={row.get('role','container-cleanup'):row['assertions'] for row in rows if 'lane' not in row}
    if (p/'commands.json').exists():
        commands=json.loads((p/'commands.json').read_bytes())
        r['commands']=len(commands)
        if commands:
            c=commands[-1]
            r['last']={key:c.get(key) for key in ('id','exit_code','finished_utc')}
            r['last']['argv_prefix']=c.get('argv',[])[:3]
if i.get('audit') and (Path(i['audit'])/'audit.json').exists():
    a=json.loads((Path(i['audit'])/'audit.json').read_bytes())
    r['audit_status']={key:a.get(key) for key in ('finished_utc','valid_evidence','errors')}
    r['audit_status'].update(packages=list(a['packages']),lanes=list(a['lanes']),recovery=list(a['recovery']))
print(json.dumps(r,sort_keys=True))
