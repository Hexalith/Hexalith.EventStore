#!/usr/bin/env python3
"""Retain complete actual outer identities without inspecting environment fields."""
import argparse,datetime,hashlib,json,subprocess
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('--out',type=Path,required=True)
p.add_argument('--before',type=Path,action='append',default=[])
a=p.parse_args();assert not a.out.exists()
commands=[]
def call(argv):
 result=subprocess.run(argv,cwd=Path.cwd(),capture_output=True,text=True)
 commands.append({'argv':argv,'cwd':str(Path.cwd()),'exit_code':result.returncode,'stdout':result.stdout,'stderr':result.stderr})
 if result.returncode:raise RuntimeError('identity observation failed')
 return result.stdout
started=datetime.datetime.now(datetime.timezone.utc).isoformat()
ids=call(['docker','ps','-aq','--no-trunc']).split()
fmt='{"id":{{json .Id}},"image":{{json .Image}},"started":{{json .State.StartedAt}},"running":{{json .State.Running}}}'
rows=sorted([json.loads(call(['docker','inspect','--format',fmt,item])) for item in ids],key=lambda row:row['id'])
result={'started_utc':started,'finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'script':str(Path(__file__).resolve()),'script_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'commands':commands,'containers':rows,'comparisons':[]}
b={row['id']:row for row in rows}
for source in a.before:
 old=json.loads(source.read_bytes());baseline={row['id']:row for row in old['containers']}
 result['comparisons'].append({'before':str(source),'before_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'entire_set_unchanged':baseline==b,'original_identities_preserved':all(b.get(k)==v for k,v in baseline.items()),'before_count':len(baseline),'after_count':len(b),'added':[b[k] for k in sorted(b.keys()-baseline.keys())],'removed':[baseline[k] for k in sorted(baseline.keys()-b.keys())],'changed':[{'before':baseline[k],'after':b[k]} for k in sorted(baseline.keys()&b.keys()) if baseline[k]!=b[k]]})
a.out.write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'out':str(a.out),'count':len(rows),'comparisons':[{'before':c['before'],'entire_set_unchanged':c['entire_set_unchanged'],'original_identities_preserved':c['original_identities_preserved'],'added':len(c['added']),'removed':len(c['removed']),'changed':len(c['changed'])} for c in result['comparisons']]}))
