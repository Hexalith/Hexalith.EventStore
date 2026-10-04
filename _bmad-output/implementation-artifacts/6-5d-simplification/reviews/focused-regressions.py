#!/usr/bin/env python3
"""Two bounded regressions demonstrated by the focused verification review."""
import json
import subprocess
import tempfile
from pathlib import Path

HERE=Path(__file__).resolve().parents[1]
CHANGES={
    'signed foreign held identity':(' and f[3]==held_identity(d)',''),
    'legacy recovery owner':("need(legacy['owner']==owner,'legacy-owner');old=legacy['state']","old=legacy['state']"),
}

def main():
    source=(HERE/'verify.py').read_text();rows=[]
    with tempfile.TemporaryDirectory(prefix='6-5d-focused-regressions-') as directory:
        target=Path(directory)
        for name in ('known-answers.json','obligations.md'):
            (target/name).write_bytes((HERE/name).read_bytes())
        path=target/'verify.py';path.write_text(source)
        control=subprocess.run(['python3',str(path)],capture_output=True,text=True)
        assert control.returncode==0,control.stdout+control.stderr
        for label,(old,new) in CHANGES.items():
            assert source.count(old)==1,(label,'anchor changed')
            path.write_text(source.replace(old,new,1))
            result=subprocess.run(['python3',str(path)],capture_output=True,text=True)
            assert result.returncode!=0,(label,'demonstrated removal still passes')
            assert 'SyntaxError' not in result.stderr,result.stderr
            assert 'AssertionError: expected owning refusal' in result.stderr,result.stderr
            rows.append({'case':label,'exitCode':result.returncode,'owningFailure':result.stderr.strip().splitlines()[-1]})
    print(json.dumps({'result':'passed','rejectedRegressions':len(rows),'cases':rows},indent=2))

if __name__=='__main__':main()
