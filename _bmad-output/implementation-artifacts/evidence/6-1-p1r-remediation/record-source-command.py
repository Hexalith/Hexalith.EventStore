#!/usr/bin/env python3
"""Record a source command from the repository root; preserve existing receipts."""
import sys,subprocess,json,time,hashlib,datetime,pathlib
name=sys.argv[1]; args=sys.argv[2:]; root=pathlib.Path.cwd(); out=root/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation/receipts';out.mkdir(parents=True,exist_ok=True)
if (out/(name+'.log')).exists() or (out/(name+'.json')).exists():
    raise SystemExit('Receipt name already exists; choose a new name.')
start=datetime.datetime.now(datetime.timezone.utc).isoformat(); t=time.monotonic(); result=subprocess.run(args,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
log=out/(name+'.log');log.write_text(result.stdout)
receipt={'argv':args,'cwd':str(root),'started_utc':start,'finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'elapsed_seconds':round(time.monotonic()-t,3),'exit_code':result.returncode,'output_sha256':hashlib.sha256(log.read_bytes()).hexdigest()}
(out/(name+'.json')).write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt));print('\n'.join(result.stdout.splitlines()[-14:]));sys.exit(result.returncode)
